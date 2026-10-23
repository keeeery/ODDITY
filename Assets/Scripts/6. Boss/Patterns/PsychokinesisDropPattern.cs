using System;
using System.Collections;
using System.Collections.Generic;
using TeamOdd.Arena;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 플레이어 위치를 중심으로 한 원형 범위 안에 염동력 오브젝트를 무작위로 투하하는 패턴
    public sealed class PsychokinesisDropPattern : MonoBehaviour
    {
        private const int MaxPositionAttempts = 12;

        [Header("References")]
        [SerializeField] private WallFragmentSpawner fragmentSpawner;
        [SerializeField] private Transform target;

        [Tooltip("바닥, 벽, 천장 판정에 사용할 레이어. Rigidbody가 붙은 오브젝트와 대상은 판정에서 제외한다.")]
        [SerializeField] private LayerMask environmentLayers = Physics.DefaultRaycastLayers;

        [Header("Count")]
        [SerializeField, Min(0)] private int largeCount = 3;
        [SerializeField, Min(0)] private int mediumCount = 7;
        [SerializeField, Min(0)] private int smallCount = 10;

        [Header("Area")]
        [SerializeField, Min(0f)] private float radius = 6f;
        [Tooltip("투하 지점끼리 유지할 최소 수평 거리(m), 자리를 찾지 못하면 무시하고 배치한다.")]
        [SerializeField, Min(0f)] private float minSpacing = 1.2f;
        [Tooltip("대상 위치 기준 투하 높이(m)")]
        [SerializeField, Min(0f)] private float dropHeight = 8f;
        [Tooltip("투하 높이에 더할 무작위 편차(m)")]
        [SerializeField, Min(0f)] private float heightJitter = 2f;

        [Header("Validation")]
        [Tooltip("대상 위치 기준으로 벽 가림과 바닥 탐색을 시작할 높이(m)")]
        [SerializeField, Min(0f)] private float probeHeight = 1.5f;
        [Tooltip("탐색 시작 높이에서 아래로 바닥을 찾을 거리(m)")]
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 10f;
        [Tooltip("천장이 있으면 천장 아래로 띄울 여유 거리(m)")]
        [SerializeField, Min(0f)] private float ceilingClearance = 0.5f;
        [Tooltip("천장 때문에 낮아진 투하 높이가 이 값보다 낮으면 해당 지점을 쓰지 않는다(m)")]
        [SerializeField, Min(0f)] private float minDropHeight = 2f;
        [Tooltip("생성 위치가 지형과 겹치는지 검사할 반경(m)")]
        [SerializeField, Min(0f)] private float spawnCheckRadius = 0.6f;

        [Header("Timing")]
        [Tooltip("패턴 시작 후 첫 투하까지의 대기 시간(초)")]
        [SerializeField, Min(0f)] private float startDelay = 0.5f;
        [Tooltip("모든 오브젝트를 나누어 투하하는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float dropDuration = 1f;
        [Tooltip("마지막 투하 후 패턴 종료까지의 대기 시간(초)")]
        [SerializeField, Min(0f)] private float recoveryDuration = 1f;
        [Tooltip("켜면 첫 투하 직전까지 대상 위치를 따라가고, 끄면 패턴 시작 시점의 위치를 사용한다.")]
        [SerializeField] private bool trackTargetUntilDrop = true;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float initialFallSpeed = 0f;
        [SerializeField, Min(0f)] private float spinSpeed = 4f;

        private readonly List<FragmentSize> dropOrder = new List<FragmentSize>();
        private readonly List<Vector3> dropPositions = new List<Vector3>();
        private readonly RaycastHit[] probeHits = new RaycastHit[32];
        private readonly Collider[] spawnOverlaps = new Collider[32];
        private Coroutine routine;
        private Vector3 dropCenter;

        public event Action Completed;
        public bool IsRunning => routine != null;

        private void OnEnable()
        {
            routine = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            // 비활성화되면 코루틴은 자동으로 멈추므로 상태만 정리
            routine = null;
        }

        private IEnumerator Run()
        {
            dropCenter = target.position;
            yield return new WaitForSeconds(startDelay);

            if (trackTargetUntilDrop) dropCenter = target.position;

            BuildDropOrder();
            dropPositions.Clear();
            float interval = dropDuration / Mathf.Max(1, dropOrder.Count - 1);

            for (int index = 0; index < dropOrder.Count; index++)
            {
                Drop(dropOrder[index]);

                if (index < dropOrder.Count - 1 && interval > 0f)
                    yield return new WaitForSeconds(interval);
            }

            yield return new WaitForSeconds(recoveryDuration);
            routine = null;
            Completed?.Invoke();
        }

        private void BuildDropOrder()
        {
            dropOrder.Clear();

            for (int index = 0; index < largeCount; index++) dropOrder.Add(FragmentSize.Large);
            for (int index = 0; index < mediumCount; index++) dropOrder.Add(FragmentSize.Medium);
            for (int index = 0; index < smallCount; index++) dropOrder.Add(FragmentSize.Small);

            // 크기가 섞여 떨어지도록 순서 셔플
            for (int index = dropOrder.Count - 1; index > 0; index--)
            {
                int swap = Random.Range(0, index + 1);
                (dropOrder[index], dropOrder[swap]) = (dropOrder[swap], dropOrder[index]);
            }
        }

        private void Drop(FragmentSize size)
        {
            // 유효한 지점을 찾지 못하면 벽 속이나 맵 밖에 떨어뜨리지 않고 건너뜀
            if (TryPickDropPosition(out Vector3 position) == false) return;

            WallFragment fragment = fragmentSpawner.SpawnFragment(size, position, Random.rotation);
            Rigidbody body = fragment.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.down * initialFallSpeed;
            body.angularVelocity = Random.onUnitSphere * spinSpeed;
        }

        // 간격 조건까지 만족하는 지점을 우선 찾고, 없으면 마지막으로 찾은 유효 지점을 사용한다.
        private bool TryPickDropPosition(out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 ground = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < MaxPositionAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = dropCenter + new Vector3(offset.x, 0f, offset.y);

                if (TryGetSpawnPosition(candidate, out Vector3 candidateGround, out Vector3 spawn) == false) continue;

                found = true;
                position = spawn;
                ground = candidateGround;

                if (IsTooClose(ground) == false) break;
            }

            if (found) dropPositions.Add(ground);
            return found;
        }

        private bool TryGetSpawnPosition(Vector3 candidate, out Vector3 ground, out Vector3 spawn)
        {
            ground = Vector3.zero;
            spawn = Vector3.zero;

            // 대상과 후보 지점 사이가 벽으로 막혀 있으면 벽 너머로 판단
            Vector3 probeOrigin = dropCenter + Vector3.up * probeHeight;
            Vector3 probeTop = candidate + Vector3.up * probeHeight;
            Vector3 toCandidate = probeTop - probeOrigin;

            if (TryGetEnvironmentHit(probeOrigin, toCandidate.normalized, toCandidate.magnitude, out _)) return false;

            // 바닥이 없으면 맵 밖으로 판단
            if (TryGetEnvironmentHit(probeTop, Vector3.down, groundProbeDistance, out RaycastHit groundHit) == false)
                return false;

            ground = groundHit.point;
            float height = dropHeight + Random.Range(0f, heightJitter);

            // 천장이 있으면 천장 아래로 낮추고, 너무 낮아지면 사용하지 않음
            if (TryGetEnvironmentHit(ground + Vector3.up * 0.05f, Vector3.up, height + ceilingClearance, out RaycastHit ceilingHit))
                height = ceilingHit.distance - ceilingClearance;

            if (height < Mathf.Min(minDropHeight, dropHeight)) return false;

            spawn = ground + Vector3.up * height;
            return IsSpawnBlocked(spawn) == false;
        }

        private bool TryGetEnvironmentHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, probeHits, distance,
                environmentLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            bool found = false;

            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = probeHits[index];

                if (IsEnvironment(hit.collider) == false || (found && hit.distance >= nearest.distance)) continue;

                nearest = hit;
                found = true;
            }

            return found;
        }

        private bool IsSpawnBlocked(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, spawnCheckRadius, spawnOverlaps,
                environmentLayers, QueryTriggerInteraction.Ignore);

            for (int index = 0; index < count; index++)
                if (IsEnvironment(spawnOverlaps[index])) return true;

            return false;
        }

        // 떨어진 염동력 오브젝트나 대상 자신은 지형으로 취급하지 않는다.
        private bool IsEnvironment(Collider collider)
        {
            return collider.attachedRigidbody == null && collider.transform.IsChildOf(target) == false;
        }

        private bool IsTooClose(Vector3 position)
        {
            float sqrSpacing = minSpacing * minSpacing;

            for (int index = 0; index < dropPositions.Count; index++)
            {
                Vector3 offset = dropPositions[index] - position;
                offset.y = 0f;

                if (offset.sqrMagnitude < sqrSpacing) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = IsRunning ? dropCenter : target.position;
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);
            UnityEditor.Handles.DrawWireDisc(center + Vector3.up * dropHeight, Vector3.up, radius);
        }
#endif
    }
}
