using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Arena
{
    // 연습장처럼 맵에 배치된 염동력 오브젝트가 부서지거나 사라지면 처음 놓였던 자리에 다시 만든다.
    // 이 오브젝트의 자식을 시작할 때 비활성 원본으로 복제해 두고, 자식이 없어지면 respawnDelay초 뒤 원본을 복제해 되살림
    // 원본은 염동력 오브젝트의 Awake(피격 트리거 생성 등)가 돌기 전 상태여야 하므로 가장 먼저 실행되도록 순서를 앞당김
    [DefaultExecutionOrder(-1000)]
    public sealed class PsychokinesisPropRespawner : MonoBehaviour
    {
        [Tooltip("오브젝트가 없어진 뒤 다시 만들어지기까지의 시간(초)")]
        [SerializeField, Min(0f)] private float respawnDelay = 3f;

        private sealed class Slot
        {
            public GameObject Template;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
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
                slots.Add(new Slot
                {
                    Template = Instantiate(child.gameObject, templateRoot),
                    LocalPosition = child.localPosition,
                    LocalRotation = child.localRotation,
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

                slot.Current = Instantiate(slot.Template, transform);
                slot.Current.name = slot.Template.name.Replace("(Clone)", string.Empty);
                slot.Current.transform.SetLocalPositionAndRotation(slot.LocalPosition, slot.LocalRotation);
                slot.Current.SetActive(true);
                slot.RespawnTime = float.PositiveInfinity;
            }
        }
    }
}
