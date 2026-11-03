using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Arena
{
    // 연습장처럼 맵에 배치된 염동력 오브젝트가 부서지거나 사라지면 처음 놓였던 자리에 다시 만든다.
    // 이 오브젝트의 자식을 시작할 때 비활성 원본으로 복제해 두고, 자식이 없어지면 respawnDelay초 뒤 원본을 복제해 되살림
    // 원본은 염동력 오브젝트의 Awake(피격 트리거 생성 등)가 돌기 전 상태여야 하므로 가장 먼저 실행되도록 순서를 앞당김
    // 처음 자리에 플레이어나 다른 물체가 있으면 그 주변에서 가장 가까운 빈 자리를 찾아 되살리고, 빈 자리가 없으면 잠시 뒤 다시 찾음
    [DefaultExecutionOrder(-1000)]
    public sealed class PsychokinesisPropRespawner : MonoBehaviour
    {
        [Tooltip("오브젝트가 없어진 뒤 다시 만들어지기까지의 시간(초)")]
        [SerializeField, Min(0f)] private float respawnDelay = 3f;

        [Tooltip("끄면 처음 자리에 무엇이 있든 그대로 되살린다.")]
        [SerializeField] private bool findFreeSpot = true;
        [Tooltip("자리를 막는 것으로 볼 레이어, 바닥도 포함해야 빈 자리 아래에 바닥이 있는지 확인할 수 있다.")]
        [SerializeField] private LayerMask blockingLayers = ~0;
        [Tooltip("처음 자리에서 빈 자리를 찾을 최대 거리")]
        [SerializeField, Min(0f)] private float searchRadius = 4f;
        [Tooltip("빈 자리가 없을 때 다시 찾기까지의 시간(초)")]
        [SerializeField, Min(0.05f)] private float retryDelay = 0.5f;

        // 바닥에 닿아 있는 것은 막힌 것으로 보지 않도록 검사 상자를 이만큼 줄임
        private const float Skin = 0.05f;
        // 빈 자리 아래 이 거리 안에 바닥이 있어야 함 (난간 밖에 떨어지지 않도록)
        private const float GroundTolerance = 0.5f;
        // 같은 거리의 둘레에서 살펴볼 방향 수
        private const int DirectionsPerRing = 12;

        private sealed class Slot
        {
            public GameObject Template;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 BoundsOffset;  // 오브젝트 위치에서 콜라이더 영역 중심까지 (월드 축)
            public Vector3 BoundsExtents; // 콜라이더 영역 절반 크기 (월드 축)
            public GameObject Current;
            public float RespawnTime = float.PositiveInfinity;
        }

        private readonly List<Slot> slots = new();

        private void Awake()
        {
            // 원본 보관용 비활성 부모 아래에 복제하면 원본의 Awake가 불리지 않음
            Transform templateRoot = new GameObject("Respawn Templates").transform;
            templateRoot.SetParent(transform, false);
            templateRoot.gameObject.SetActive(false);

            List<Transform> children = new();
            foreach (Transform child in transform)
                if (child != templateRoot) children.Add(child);

            foreach (Transform child in children)
            {
                Bounds bounds = GetBounds(child.gameObject);
                slots.Add(new Slot
                {
                    Template = Instantiate(child.gameObject, templateRoot),
                    LocalPosition = child.localPosition,
                    LocalRotation = child.localRotation,
                    BoundsOffset = bounds.center - child.position,
                    BoundsExtents = bounds.extents,
                    Current = child.gameObject
                });
            }
        }

        private void Update()
        {
            foreach (Slot slot in slots)
            {
                if (slot.Current != null) continue;

                // 없어진 것을 처음 알아챈 순간부터 대기
                if (float.IsPositiveInfinity(slot.RespawnTime))
                {
                    slot.RespawnTime = Time.time + respawnDelay;
                    continue;
                }

                if (Time.time < slot.RespawnTime) continue;

                Vector3 position = transform.TransformPoint(slot.LocalPosition);
                if (findFreeSpot && TryFindFreeSpot(slot, position, out position) == false)
                {
                    slot.RespawnTime = Time.time + retryDelay;
                    continue;
                }

                slot.Current = Instantiate(slot.Template, transform);
                slot.Current.name = slot.Template.name.Replace("(Clone)", string.Empty);
                slot.Current.transform.SetPositionAndRotation(position, transform.rotation * slot.LocalRotation);
                slot.Current.SetActive(true);
                slot.RespawnTime = float.PositiveInfinity;
            }
        }

        // 처음 자리부터 둘레를 넓혀 가며 가장 가까운 빈 자리를 찾음
        private bool TryFindFreeSpot(Slot slot, Vector3 origin, out Vector3 spot)
        {
            spot = origin;
            if (IsFree(slot, origin, false)) return true;

            // 둘레 간격은 오브젝트 크기만큼, 너무 작은 물체는 촘촘하게 살피지 않도록 최소값을 둠
            float step = Mathf.Max(0.25f, Mathf.Max(slot.BoundsExtents.x, slot.BoundsExtents.z) * 2f);
            for (float radius = step; radius <= searchRadius + 0.001f; radius += step)
            {
                for (int i = 0; i < DirectionsPerRing; i++)
                {
                    float angle = i * Mathf.PI * 2f / DirectionsPerRing;
                    Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsFree(slot, candidate, true) == false) continue;

                    spot = candidate;
                    return true;
                }
            }

            return false;
        }

        // 그 자리에 놓았을 때 콜라이더 영역이 다른 것과 겹치지 않는지, 옮긴 자리라면 아래에 바닥이 있는지
        private bool IsFree(Slot slot, Vector3 position, bool needsGround)
        {
            Vector3 center = position + slot.BoundsOffset;
            Vector3 halfExtents = Vector3.Max(slot.BoundsExtents - Vector3.one * Skin, Vector3.one * 0.01f);
            if (Physics.CheckBox(center, halfExtents, Quaternion.identity, blockingLayers, QueryTriggerInteraction.Ignore)) return false;
            if (needsGround == false) return true;

            return Physics.Raycast(center, Vector3.down, slot.BoundsExtents.y + GroundTolerance, blockingLayers, QueryTriggerInteraction.Ignore);
        }

        // 트리거가 아닌 콜라이더를 모두 감싼 영역, 콜라이더가 없으면 렌더러 영역
        private static Bounds GetBounds(GameObject target)
        {
            bool found = false;
            Bounds bounds = new(target.transform.position, Vector3.zero);
            foreach (Collider collider in target.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger) continue;
                if (found) bounds.Encapsulate(collider.bounds);
                else bounds = collider.bounds;
                found = true;
            }

            if (found) return bounds;

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }

            return bounds;
        }
    }
}
