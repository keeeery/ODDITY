using FantasyGarden.FSM;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    // 분신의 기본 상태: 나타나자마자 공격하지 않도록 등장 연출 동안 플레이어를 바라보며 서 있다가 Idle로
    public class HumanoidCloneAppearState : BossStateBase
    {
        [SerializeField] private float turnSpeed = 180f;

        private HumanoidClone clone;
        private float endTime;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            clone = root.GetComponent<HumanoidClone>();
        }

        protected override void OnEnter()
        {
            endTime = Clock.LocalTime + (clone != null ? clone.AppearDuration : 0f);
        }

        protected override void OnUpdate()
        {
            root.RotateYaw(root.SignedAngleToTarget, turnSpeed * Clock.LocalDeltaTime);
            if (Clock.LocalTime < endTime) return;

            root.ChangeBossState(typeof(BossIdleState));
        }

        protected override void OnExit() { }
    }
}
