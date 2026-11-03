using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Mission
{
    // 로비에서 훈련장과 나뉜 정비소 공간. 플레이어가 이 상자 안에 있으면 기본 공격·스킬을 막고, 우클릭은 염동력 대신 NPC 상호작용에 쓴다.
    // 트리거 콜라이더를 두면 다른 레이캐스트에 걸릴 수 있어 위치만으로 판정한다.
    public sealed class RepairShopZone : MonoBehaviour
    {
        public const string AttackBlockedMessage = "정비소에서는 기본 공격을 사용할 수 없습니다.";
        public const string SkillBlockedMessage = "정비소에서는 스킬을 사용할 수 없습니다.";

        private static readonly List<RepairShopZone> zones = new();

        [Tooltip("이 오브젝트 기준 상자의 중심과 크기")]
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = new(10f, 6f, 10f);

        // 정비소가 없는 씬에서는 항상 false
        public static bool Contains(Vector3 position)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].ContainsPoint(position))
                    return true;
            }

            return false;
        }

        private void OnEnable() => zones.Add(this);

        private void OnDisable() => zones.Remove(this);

        private bool ContainsPoint(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position) - center;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Color originalColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = originalMatrix;
            Gizmos.color = originalColor;
        }
#endif
    }
}
