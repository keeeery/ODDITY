using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TeamOdd.Arena;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 플레이어 위치를 중심으로 한 원형 범위 안에 염동력 오브젝트를 무작위로 투하하는 패턴
    // 투하가 시작되면 큰/중간 오브젝트마다 착지 지점을 낙하 시간 동안 바닥에 예고
    public sealed class BossPsychokinesisDropPattern : BossCooldownPatternBase
    {
        private const int MaxPositionAttempts = 12;

        [Title("References")]
        [SerializeField] private WallFragmentSpawner fragmentSpawner;

        [Tooltip("바닥, 벽, 천장 판정에 사용할 레이어. Rigidbody가 붙은 오브젝트와 대상은 판정에서 제외한다.")]
        [SerializeField] private LayerMask environmentLayers = Physics.DefaultRaycastLayers;

        [Title("Count")]
        [SerializeField, Min(0)] private int largeCount = 3;
        [SerializeField, Min(0)] private int mediumCount = 7;
        [SerializeField, Min(0)] private int smallCount = 10;

        [Title("Area")]
        [SerializeField, Min(0f)] private float radius = 6f;
        [Tooltip("투하 지점끼리 유지할 최소 수평 거리(m), 자리를 찾지 못하면 무시하고 배치한다.")]
        [SerializeField, Min(0f)] private float minSpacing = 1.2f;
        [Tooltip("대상 위치 기준 투하 높이(m)")]
        [SerializeField, Min(0f)] private float dropHeight = 8f;
        [Tooltip("투하 높이에 더할 무작위 편차(m)")]
        [SerializeField, Min(0f)] private float heightJitter = 2f;

        [Title("Validation")]
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

        [Title("Timing")]
        // 투하 시작: Roar 애니메이션의 OnRoarDrop 신호, 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)
        [Tooltip("모든 오브젝트를 나누어 투하하는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float dropDuration = 1f;
        [Tooltip("켜면 OnRoarDrop 신호를 받는 순간까지 대상 위치를 따라갈지 여부")]
        [SerializeField] private bool trackTargetUntilDrop = true;

        [Title("Motion")]
        [SerializeField, Min(0f)] private float initialFallSpeed = 0f;
        [SerializeField, Min(0f)] private float spinSpeed = 4f;

        [Title("Telegraph")]
        [Tooltip("착지 지점 표시 반경에 곱할 배율, 1이면 오브젝트의 수평 크기와 같다.")]
        [SerializeField, Min(0f)] private float markerRadiusScale = 1f;
        [Tooltip("착지 지점 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float telegraphFadeDuration = 0.3f;

        private readonly List<FragmentSize> dropOrder = new List<FragmentSize>();
        private readonly List<Vector3> dropPositions = new List<Vector3>();
        private readonly RaycastHit[] probeHits = new RaycastHit[32];
        private readonly Collider[] spawnOverlaps = new Collider[32];
        private Vector3 dropCenter;

        protected override void OnEnter()
        {
            StartCooldown();
            dropCenter = Target.position;
            root.AnimEventListener.OnOccursAnimStateEvent += OnAnimEvent;
            root.Animator.SetTrigger(BossAnimParams.ROAR);
        }

        // 비활성화되면 투하 코루틴은 자동으로 멈춤, 이미 떨어지는 오브젝트의 착지 표시는 끝까지 둠
        protected override void OnExit()
        {
            root.AnimEventListener.OnOccursAnimStateEvent -= OnAnimEvent;
            root.Animator.ResetTrigger(BossAnimParams.ROAR);
        }

        private void OnAnimEvent(string eventName, string eventParam)
        {
            if (eventName == "OnRoarDrop") StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (trackTargetUntilDrop) dropCenter = Target.position;

            BuildDropOrder();
            dropPositions.Clear();
            float interval = dropDuration / Mathf.Max(1, dropOrder.Count - 1);

            for (int index = 0; index < dropOrder.Count; index++)
            {
                Drop(dropOrder[index]);

                if (index < dropOrder.Count - 1 && interval > 0f)
                    yield return new WaitForSeconds(interval);
            }
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
            if (TryPickDropPosition(out Vector3 position, out Vector3 ground) == false) return;

            WallFragment fragment = fragmentSpawner.SpawnFragment(size, position, Random.rotation);
            Rigidbody body = fragment.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.down * initialFallSpeed;
            body.angularVelocity = Random.onUnitSphere * spinSpeed;

            // 작은 파편은 연출용이라 착지 지점을 예고하지 않음
            if (size != FragmentSize.Small) ShowLandingMarker(fragment, position, ground);
        }

        // 착지 시점은 낙하 시간으로 추정, 표시는 다 차오르면 스스로 사라지므로 참조를 들고 있지 않음
        // 콜라이더 bounds는 물리 동기화 전이라 이전 위치/크기일 수 있어 Renderer bounds로 크기를 잼
        private void ShowLandingMarker(WallFragment fragment, Vector3 position, Vector3 ground)
        {
            Bounds bounds = fragment.GetComponent<Renderer>().bounds;
            float markerRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) * markerRadiusScale;
            float fallTime = GetFallTime(position.y - ground.y);

            AttackTelegraph marker = root.ShowTelegraph(TelegraphShape.Circle(markerRadius), new Pose(ground, Quaternion.identity), fallTime);
            if (marker != null) marker.ResolveWhenFilled(fallTime, telegraphFadeDuration);
        }

        // 초기 낙하 속도와 중력만으로 height만큼 떨어지는 시간(공기 저항 무시)
        private float GetFallTime(float height)
        {
            float gravity = -Physics.gravity.y;
            if (gravity <= 0f) return height / Mathf.Max(initialFallSpeed, 0.01f);

            return (Mathf.Sqrt(initialFallSpeed * initialFallSpeed + 2f * gravity * height) - initialFallSpeed) / gravity;
        }

        // 간격 조건까지 만족하는 지점을 우선 찾고, 없으면 마지막으로 찾은 유효 지점 사용
        private bool TryPickDropPosition(out Vector3 position, out Vector3 ground)
        {
            position = Vector3.zero;
            ground = Vector3.zero;
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
            return collider.attachedRigidbody == null && collider.transform.IsChildOf(Target) == false;
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
            if (Application.isPlaying == false) return;

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(dropCenter, Vector3.up, radius);
            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);
            UnityEditor.Handles.DrawWireDisc(dropCenter + Vector3.up * dropHeight, Vector3.up, radius);
        }
#endif
    }
}
