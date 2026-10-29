using System;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd
{
    public class HumanoidIdleState : BossIdleState, IHumanoidPhaseListener
    {
        [Title("Strafe")]
        [SerializeField, Range(0f, 1f)] private float strafeChance = 0.4f;

        [Title("Turn Start")]
        [SerializeField] private float turnStartMinAngle = 45f;
        [SerializeField] private float turnStartMaxAngle = 180f; // 135°를 넘으면 애니메이터가 90° 대신 180° 반전 출발 클립을 고름

        // 페이즈 2: 패턴 추가, 스트레이프로 간 보는 시간을 줄이고 후딜을 짧게 해 더 몰아붙임
        [Title("Phase 2")]
        [SerializeField] private BossMeleePattern[] phase2Melees;
        [SerializeField, Range(0f, 1f)] private float phase2StrafeChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float phase2RecoveryScale = 0.75f;

        private bool isStrafeReserved;
        private float recoveryScale = 1f;

        public void EnterPhase2()
        {
            AddMelees(phase2Melees);
            strafeChance = phase2StrafeChance;
            recoveryScale = phase2RecoveryScale;
        }

        // 진입 중(OnEnable)에는 상태를 바꿀 수 없으므로 첫 Update에서 스트레이프로 넘어감
        protected override void OnUpdate()
        {
            if (isStrafeReserved)
            {
                isStrafeReserved = false;
                ChangeState(typeof(HumanoidStrafeState));
                return;
            }

            base.OnUpdate();
        }

        protected override void OnExit()
        {
            isStrafeReserved = false;
            base.OnExit();
        }

        protected override void OnRecoveryStarted(float recovery)
        {
            isStrafeReserved = Random.value < strafeChance;
        }

        protected override float GetRecovery(BossMeleePattern pattern) => base.GetRecovery(pattern) * recoveryScale;

        // 다가가려고 제자리에서 돌 상황이면 멈춰 돌지 않고 돌면서 바로 출발
        protected override void ChangeState(Type nextState)
        {
            float startAngle = 0f;

            if (nextState == typeof(BossTurnState) && TryGetTurnStartAngle(out startAngle))
                nextState = typeof(BossChaseState);

            Animator.SetFloat(BossAnimParams.START_ANGLE, startAngle);
            base.ChangeState(nextState);
        }

        private bool TryGetTurnStartAngle(out float angle)
        {
            angle = root.SignedAngleToSteer;
            if (IsApproaching() == false) return false;

            float absAngle = Mathf.Abs(angle);
            return absAngle >= turnStartMinAngle && absAngle <= turnStartMaxAngle;
        }

        // BossTurnState와 같은 기준, 사거리 밖인 공격을 하러 가거나 가려진 대상을 찾으러 가는 중
        private bool IsApproaching()
        {
            BossMeleePattern intent = root.Intent;

            if (intent == null) return root.HasLineOfSight == false;

            return intent.IsDistanceInRange == false;
        }
    }
}
