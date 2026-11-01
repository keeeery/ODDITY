using System.Collections.Generic;
using FantasyGarden.Utility;
using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Boss
{
    // 너무 가깝거나 많이 돌아야 할 때 뒤(대각선)로 뛰어 자리를 잡고, 착지 후 Forward 공격으로 이어가는 상태
    public class BossHopState : BossStateBase
    {
        [Title("Condition")]
        [SerializeField] private float tooCloseDistance = 3f;
        [SerializeField] private float wideAngle = 60f; // 이 각도보다 많이 돌아야 하면 점프 후보
        [SerializeField, Range(0f, 1f)] private float chance = 0.4f;
        [SerializeField] private float cooldown = 8f;

        [Title("Jump")]
        [SerializeField] private float airDuration = 0.6f; // OnHopStart ~ OnHopLand 사이 시간
        [SerializeField] private float maxHopDistance = 10f;
        [SerializeField] private float sideAngle = 45f; // 정뒤에서 좌우로 틀 수 있는 최대 각도
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float timeout = 2f;

        [Title("Patterns")]
        [SerializeField] private BossMeleePattern[] followPatterns; // 착지 후 이어질 공격 후보

        private readonly List<BossMeleePattern> candidates = new();
        private readonly List<float> weights = new();

        private float readyTime;
        private float enterTime;
        private bool isJumping;
        private float jumpStartTime;
        private Vector3 jumpStart;
        private Vector3 landing;

        private BossMeleePattern follow;

        protected override void OnEnter()
        {
            enterTime = Clock.LocalTime;
            readyTime = Clock.LocalTime + cooldown;

            root.AnimEventListener.OnOccursAnimStateEvent += OnOccursAnimEvent;

            Animator.applyRootMotion = false;
            Animator.SetTrigger(BossAnimParams.HOP_BACK);
        }

        // 클립이 제자리 모션이라 수평 이동은 직접 처리, 공중에서도 대상 쪽으로 계속 회전
        protected override void OnUpdate()
        {
            if (Clock.LocalTime - enterTime >= timeout)
            {
                root.ChangeBossState(typeof(BossIdleState));
                return;
            }

            root.RotateYaw(root.SignedAngleToTarget, turnSpeed * Clock.LocalDeltaTime);

            if (isJumping == false) return;

            // 남은 체공 시간 안에 착지 지점에 닿도록 수평 속도를 맞춤
            float remainTime = Mathf.Max(airDuration - (Clock.LocalTime - jumpStartTime), Clock.LocalDeltaTime);
            Vector3 velocity = (landing - Movement.Position) / remainTime;
            Movement.Move(velocity * Clock.LocalDeltaTime);
        }

        protected override void OnExit()
        {
            root.AnimEventListener.OnOccursAnimStateEvent -= OnOccursAnimEvent;
            Animator.ResetTrigger(BossAnimParams.HOP_BACK);

            StopJump();
        }

        #region PUBLIC_METHODS

        // Idle에서 호출, 점프할 상황이면 이어질 공격까지 미리 골라 둠
        public bool ShouldHop()
        {
            if (Clock.LocalTime < readyTime) return false;

            bool isTooClose = root.DistanceToTarget < tooCloseDistance;
            bool isWideAngle = Mathf.Abs(root.SignedAngleToTarget) > wideAngle;

            if (isTooClose == false && isWideAngle == false) return false;
            if (Random.value >= chance) return false;

            return TryPickFollow(out follow);
        }

        public override void ResetMemory() => follow = null;

        // 착지 후 Idle에서 한 번만 꺼내 씀
        public BossMeleePattern TakeFollow()
        {
            BossMeleePattern result = follow;
            follow = null;
            return result;
        }

        #endregion

        private void OnOccursAnimEvent(string name, string param)
        {
            if (string.Equals(name, "OnHopStart"))
            {
                StartJump();
                return;
            }

            if (string.Equals(name, "OnHopLand"))
            {
                StopJump();
                return;
            }

            if (string.Equals(name, "OnHopEnded"))
            {
                root.ChangeBossState(typeof(BossIdleState));
            }
        }

        // 대상에서 보스 쪽 방향을 좌우로 살짝 틀고, 이어질 공격 사거리 중간만큼 떨어진 곳에 착지
        private void StartJump()
        {
            jumpStart = Movement.Position;

            Vector3 away = jumpStart - Target.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -root.transform.forward;

            Vector3 direction = Quaternion.Euler(0f, Random.Range(-sideAngle, sideAngle), 0f) * away.normalized;
            float distance = follow != null ? (follow.MinDistance + follow.MaxDistance) * 0.5f : maxHopDistance;

            Vector3 goal = new Vector3(Target.position.x, jumpStart.y, Target.position.z) + direction * distance;
            landing = jumpStart + Vector3.ClampMagnitude(goal - jumpStart, maxHopDistance);

            jumpStartTime = Clock.LocalTime;
            isJumping = true;
        }

        private void StopJump() => isJumping = false;

        private bool TryPickFollow(out BossMeleePattern result)
        {
            result = null;
            candidates.Clear();
            weights.Clear();

            foreach (BossMeleePattern pattern in followPatterns)
            {
                if (pattern.CanUse == false) continue;

                candidates.Add(pattern);
                weights.Add(pattern.Weight);
            }

            if (candidates.Count == 0) return false;

            result = candidates.GetWeightedRandom(weights);
            return true;
        }

        #region HOP_VISUALIZATION

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false || isJumping == false) return;

            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawLine(jumpStart, landing);
            UnityEditor.Handles.DrawWireDisc(landing, Vector3.up, 1f);

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
