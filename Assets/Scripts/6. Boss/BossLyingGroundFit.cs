using System.Collections.Generic;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 쓰러진 몸(그로기·사망)이 단차 위 바닥(레일·높은 바닥)에 파묻히지 않도록 보스를 들어 올림
    // 몸체는 발밑 바닥 높이(BossMovement.GroundHeight)에 서는데, 누운 몸은 옆으로 길어 더 높은 바닥에 걸치면 그대로 파묻힌다.
    // 몸 부위 콜라이더(뼈를 따라 움직임)마다 바로 아래 바닥 윗면을 재서 가장 많이 파묻힌 만큼 올리고, 끝나면 원래 높이로 내림
    // 높이 차가 maxLift보다 큰 곳(벽·기둥 윗면)은 걸칠 바닥으로 보지 않는다.
    // 그로기·사망 상태가 Begin/End를 부르며, 이 컴포넌트가 없는 보스는 지금처럼 바닥 높이에 머문다.
    [RequireComponent(typeof(BossMovement))]
    public sealed class BossLyingGroundFit : MonoBehaviour
    {
        private const float RAY_MARGIN = 0.1f;

        [Tooltip("몸이 걸칠 수 있는 바닥 레이어, 레일처럼 Wall 레이어인 단차도 포함")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("들어 올릴 수 있는 최대 높이(m), 이보다 높은 곳은 벽으로 보고 무시")]
        [SerializeField, Min(0f)] private float maxLift = 1.2f;
        [Tooltip("높이를 따라가는 속도(m/s)")]
        [SerializeField, Min(0.01f)] private float liftSpeed = 4f;

        [Title("States")]
        [ShowInInspector, ReadOnly] private bool isFitting;
        [ShowInInspector, ReadOnly] private float targetLift;
        [ShowInInspector, ReadOnly] private float lift;

        private BossMovement movement;
        private TimeFreezable clock;
        private Collider ownCollider;
        private readonly List<Collider> parts = new();
        private bool isActive;

        private void Awake()
        {
            movement = GetComponent<BossMovement>();
            clock = TimeFreezable.GetOrAdd(gameObject);
            ownCollider = GetComponent<ICombatable>()?.Collider;
        }

        #region PUBLIC_METHODS

        public void Begin()
        {
            if (isActive == false) lift = 0f;

            // 부위 콜라이더는 실행할 때 뼈에 붙으므로(HumanoidBossHitboxes) 쓸 때마다 다시 모음
            // 몸과 같은 레이어(Monster)만 써서 공격 판정 같은 다른 콜라이더는 뺌
            parts.Clear();
            foreach (Collider part in GetComponentsInChildren<Collider>(true))
                if (part != ownCollider && part.gameObject.layer == gameObject.layer) parts.Add(part);

            isFitting = true;
            isActive = true;
        }

        // 원래 높이까지 천천히 내린 뒤 높이를 놓아 줌
        public void End() => isFitting = false;

        #endregion

        private void LateUpdate()
        {
            if (isActive == false) return;

            if (isFitting) targetLift = MeasureLift();
            else targetLift = 0f;

            lift = Mathf.MoveTowards(lift, targetLift, liftSpeed * clock.LocalDeltaTime);

            if (isFitting == false && lift <= 0f)
            {
                isActive = false;
                movement.SetLift(0f);
                return;
            }

            movement.SetLift(lift);
        }

        // 부위마다 들어 올리지 않았을 때의 가장 낮은 점과 그 아래 바닥 윗면의 차이 중 가장 큰 값
        // 서 있던 바닥보다 낮게 내려간 부위(바닥에 살짝 묻힌 부위 콜라이더)는 서 있던 바닥에 닿은 것으로 봐, 평평한 바닥에서는 들지 않음
        private float MeasureLift()
        {
            Physics.SyncTransforms(); // 애니메이션이 옮긴 뼈 콜라이더의 bounds를 이번 프레임 자세로 맞춤

            float baseHeight = movement.GroundHeight;
            float applied = transform.position.y - baseHeight;
            float result = 0f;
            bool hasPart = false;

            foreach (Collider part in parts)
            {
                if (part == null || part.enabled == false || part.gameObject.activeInHierarchy == false) continue;

                Bounds bounds = part.bounds;
                float lowest = bounds.min.y - applied;
                Vector3 origin = new(bounds.center.x, lowest + maxLift + RAY_MARGIN, bounds.center.z);
                hasPart = true;

                // 시작점이 높은 벽 안이면 그 벽은 맞지 않으므로 벽 윗면에 올라타지 않음
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxLift + RAY_MARGIN * 2f, groundMask, QueryTriggerInteraction.Ignore))
                    result = Mathf.Max(result, hit.point.y - Mathf.Max(lowest, baseHeight));
            }

            return hasPart ? Mathf.Min(result, maxLift) : targetLift;
        }
    }
}
