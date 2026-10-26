using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;
using TeamOdd.Combat;

namespace TeamOdd.Boss
{
    // 포효 후 대상을 바라보며 돌진을 준비하고, 방향을 고정해 정해진 거리만큼 돌진하는 패턴
    // - 포효: 진입 트리거(Roar), 끝나면(OnAttackEnded) 준비 시작
    // - 준비: aimDuration 동안 제자리 Run을 느리게 재생하며 대상 쪽으로 회전
    // - 돌진: 방향을 고정하고 chargeDistance만큼 달림, 대상은 통과하며 한 번만 피해
    // - 벽에 정면으로 부딪히면 GetHitFront 후 stunDuration 동안 행동불능, 끝나면 후딜 없이 Idle
    // - 끝까지 달리면 대상 위치로 점프스매시 후 포효, 마지막 포효의 OnAttackEnded로 종료(후딜 적용)
    public class BossCharge : BossMeleePattern
    {
        private enum ChargePhase
        {
            Roar,
            Aim,
            Charge,
            Stun,
            JumpSmash,
            EndRoar,
        }

        [Title("Usage")]
        [SerializeField, Range(0f, 1f)] private float useChance = 0.75f; // 임시 사용 확률, 세부 사용 조건은 팀 논의 후 CanUse에서 수정

        [Title("Aim")]
        [SerializeField] private float aimDuration = 1.5f;
        [SerializeField] private float aimTurnSpeed = 180f;
        [SerializeField] private float aimRunAnimSpeed = 0.3f; // 준비 중 제자리 Run 재생 속도

        [Title("Charge")]
        [SerializeField] private float chargeDistance = 25f;
        [SerializeField] private float chargeSpeed = 20f;
        [SerializeField] private float chargeRunAnimSpeed = 1.5f;
        [SerializeField] private LayerMask passThroughMask; // 돌진 중 막히지 않고 통과할 레이어 (플레이어)

        [Title("Charge Hit")]
        [SerializeField] private float hitRadius = 5f; // 보스 위치 기준, 이 안에 대상이 들어오면 피해
        [SerializeField] private int chargeDamage = 20;
        [SerializeField] private HitReaction chargeReaction = HitReaction.Knockdown;

        [Title("Wall")]
        [SerializeField] private LayerMask wallMask; // 부딪히면 행동불능이 되는 레이어
        [SerializeField] private float headOnAngle = 60f; // 진행 방향과 벽 사이 각이 이 이하일 때만 행동불능, 넘으면 벽을 따라 미끄러지며 계속 돌진
        [SerializeField] private float stunDuration = 3f; // 벽에 부딪힌 순간부터 잼, GetHitFront 재생 시간 포함

        [SerializeField, HideLabel] private BossJumpSmashMotion jump = new();

        [Title("States")]
        [ShowInInspector, ReadOnly] private ChargePhase phase;
        [ShowInInspector, ReadOnly] private float chargedDistance;

        private float phaseEndTime;
        private Vector3 chargeDir;
        private bool hasHitTarget;
        private bool wasStunned;

        private int rollFrame = -1;
        private bool isRollSuccess;

        // 임시 사용 조건: 쿨타임 + 확률
        // Idle의 한 번 판단 안에서 여러 번 불려도 결과가 같도록 프레임마다 한 번만 굴림
        public override bool CanUse
        {
            get
            {
                if (base.CanUse == false) return false;

                if (rollFrame != Time.frameCount)
                {
                    rollFrame = Time.frameCount;
                    isRollSuccess = Random.value < useChance;
                }

                return isRollSuccess;
            }
        }

        // 벽에 부딪혀 행동불능이 끝난 경우엔 이미 충분히 멈춰 있었으므로 후딜을 주지 않음
        public override float AppliedRecovery => wasStunned ? 0f : Recovery;

        protected override bool UsesMeleeHits => false;

        // 첫 포효와 점프스매시의 종료 신호는 다음 단계로 넘기는 데 쓰고, 마지막 포효에서만 끝냄
        protected override bool CanEndByAnimation => phase == ChargePhase.EndRoar;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            jump.Initialize(root);
        }

        protected override void OnPatternEnter()
        {
            phase = ChargePhase.Roar;
            chargedDistance = 0f;
            hasHitTarget = false;
            wasStunned = false;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            switch (phase)
            {
                case ChargePhase.Aim:
                    UpdateAim();
                    break;

                case ChargePhase.Charge:
                    UpdateCharge();
                    break;

                case ChargePhase.Stun:
                    UpdateStun();
                    break;

                case ChargePhase.JumpSmash:
                    jump.Update();
                    break;
            }
        }

        protected override void OnPatternExit()
        {
            EndCharge();
            jump.Stop();

            Animator.SetBool(BossAnimParams.IS_CHARGING, false);
            Animator.ResetTrigger(BossAnimParams.CHARGE_STUN);
            Animator.ResetTrigger(BossAnimParams.JUMP_SMASH);
            Animator.ResetTrigger(BossAnimParams.ROAR);
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (string.Equals(name, "OnAttackEnded"))
            {
                if (phase == ChargePhase.Roar) StartAim();
                else if (phase == ChargePhase.JumpSmash) StartEndRoar();
                return;
            }

            if (phase != ChargePhase.JumpSmash) return;

            if (string.Equals(name, "OnJumpStart"))
            {
                jump.Begin();
                return;
            }

            if (string.Equals(name, "OnJumpLock"))
            {
                jump.Lock();
                return;
            }

            if (string.Equals(name, "OnJumpLand"))
            {
                TryHit(jump.ImpactShape, jump.Land(), jump.Damage, jump.Reaction);
            }
        }

        #region PHASE_METHODS

        private void StartAim()
        {
            phase = ChargePhase.Aim;
            phaseEndTime = Clock.LocalTime + aimDuration;

            Animator.SetFloat(BossAnimParams.CHARGE_RUN_SPEED, aimRunAnimSpeed);
            Animator.SetBool(BossAnimParams.IS_CHARGING, true);
        }

        private void UpdateAim()
        {
            root.RotateYaw(root.SignedAngleToTarget, aimTurnSpeed * Clock.LocalDeltaTime);

            if (Clock.LocalTime >= phaseEndTime) StartCharge();
        }

        // 준비가 끝난 순간의 정면으로 방향을 고정
        private void StartCharge()
        {
            phase = ChargePhase.Charge;
            chargedDistance = 0f;

            chargeDir = Movement.Rotation * Vector3.forward;
            chargeDir.y = 0f;
            chargeDir.Normalize();

            Movement.PassThroughMask = passThroughMask;
            Movement.OnBlocked += HandleBlocked;

            Animator.SetFloat(BossAnimParams.CHARGE_RUN_SPEED, chargeRunAnimSpeed);
        }

        // 벽에 막혀 덜 나아가도 요청한 거리 기준으로 세므로, 비스듬히 미끄러지며 막혀도 언젠가는 끝남
        private void UpdateCharge()
        {
            float step = Mathf.Min(chargeSpeed * Clock.LocalDeltaTime, chargeDistance - chargedDistance);
            chargedDistance += step;
            Movement.Move(chargeDir * step);

            TryHitTargetOnce();

            if (chargedDistance >= chargeDistance) StartJumpSmash();
        }

        private void TryHitTargetOnce()
        {
            if (hasHitTarget) return;

            Pose pose = new Pose(Movement.Position, Movement.Rotation);
            hasHitTarget = TryHit(TelegraphShape.Circle(hitRadius), pose, chargeDamage, chargeReaction);
        }

        // BossMovement의 FixedUpdate에서 불림
        private void HandleBlocked(BossBlockInfo info)
        {
            if (phase != ChargePhase.Charge) return;
            if (wallMask.Contains(info.Collider.gameObject) == false) return;
            if (info.ImpactAngle > headOnAngle) return;

            StartStun();
        }

        private void StartStun()
        {
            EndCharge();

            phase = ChargePhase.Stun;
            phaseEndTime = Clock.LocalTime + stunDuration;
            wasStunned = true;

            Animator.SetBool(BossAnimParams.IS_CHARGING, false);
            Animator.SetTrigger(BossAnimParams.CHARGE_STUN);
        }

        private void UpdateStun()
        {
            if (Clock.LocalTime < phaseEndTime) return;
            StateMachine.ChangeState(typeof(BossIdleState));
        }

        private void StartJumpSmash()
        {
            EndCharge();

            phase = ChargePhase.JumpSmash;

            Animator.SetBool(BossAnimParams.IS_CHARGING, false);
            Animator.SetTrigger(BossAnimParams.JUMP_SMASH);
        }

        private void StartEndRoar()
        {
            phase = ChargePhase.EndRoar;
            Animator.SetTrigger(BossAnimParams.ROAR);
        }

        private void EndCharge()
        {
            Movement.OnBlocked -= HandleBlocked;
            Movement.PassThroughMask = 0;
        }

        #endregion

        #region CHARGE_VISUALIZATION

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            jump.DrawHandles();

            if (Application.isPlaying == false || phase != ChargePhase.Charge) return;

            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = Color.magenta;
            Vector3 start = Movement.Position - chargeDir * chargedDistance;
            UnityEditor.Handles.DrawLine(start, start + chargeDir * chargeDistance);

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
