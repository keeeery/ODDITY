using System;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 피격 대상을 밀어내는 거리·높이·시간과 방향, 공격(판정)마다 데미지·반응(HitReaction)과 함께 정함
    // 반응(넉백/넉다운)과 별개로 넉백·넉다운 모두에 적용, 거리와 높이가 모두 0이면 밀지 않고 피격 모션만 재생
    [Serializable]
    public struct HitPush
    {
        public enum DirectionMode
        {
            FromAttacker, // 공격자 위치에서 대상 쪽으로
            FromHitCenter, // 판정 중심(충격파·착지 지점, 날아온 물체 등)에서 대상 쪽으로
            AttackerForward // 대상 위치와 관계없이 공격자가 바라보는 방향으로
        }

        [Tooltip("밀려나는 수평 거리(m), 0이면 제자리에서 피격 모션만 재생한다.")]
        [Min(0f)] public float distance;
        [Tooltip("밀려나는 동안 떠오르는 최고 높이(m)")]
        [Min(0f)] public float height;
        [Tooltip("밀려나는 데 걸리는 시간(초)")]
        [Min(0.01f)] public float duration;
        [Tooltip("밀어내는 방향의 기준\nFromAttacker: 공격자 → 대상\nFromHitCenter: 판정 중심(충격파·착지 지점, 날아온 물체) → 대상\nAttackerForward: 공격자가 바라보는 방향")]
        public DirectionMode direction;
        [Tooltip("기준 방향에서 더 돌릴 각도(도), 위에서 볼 때 양수는 시계 방향(오른쪽)")]
        [Range(-180f, 180f)] public float angleOffset;

        public bool HasMovement => distance > 0f || height > 0f;

        public static HitPush Create(float distance, float height, float duration, DirectionMode direction = DirectionMode.FromAttacker) =>
            new HitPush { distance = distance, height = height, duration = duration, direction = direction };

        // 수평 단위 방향, 대상이 기준점과 겹쳐 방향이 없으면 공격자 정면으로 밀어냄
        public Vector3 GetDirection(Transform attacker, Vector3 hitCenter, Vector3 targetPosition)
        {
            Vector3 attackerForward = Vector3.ProjectOnPlane(attacker.forward, Vector3.up);

            Vector3 baseDirection = direction switch
            {
                DirectionMode.FromHitCenter => targetPosition - hitCenter,
                DirectionMode.AttackerForward => attackerForward,
                _ => targetPosition - attacker.position
            };
            baseDirection.y = 0f;

            if (baseDirection.sqrMagnitude < 0.0001f) baseDirection = attackerForward;
            if (baseDirection.sqrMagnitude < 0.0001f) baseDirection = Vector3.forward;

            return Quaternion.Euler(0f, angleOffset, 0f) * baseDirection.normalized;
        }
    }
}
