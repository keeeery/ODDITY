using FantasyGarden.FSM;
using TeamOdd.Boss;

namespace TeamOdd
{
    // 포효로 밀쳐내는 동작은 BossRoarPattern을 그대로 쓰고, 연출 동안만 무적
    // Idle의 패턴 목록에는 넣지 않고 HumanoidPhaseController가 직접 끼워 넣음
    public class HumanoidPhaseTransition : BossRoarPattern
    {
        private HumanoidBossCombatEntity combatEntity;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            combatEntity = root.GetComponent<HumanoidBossCombatEntity>();
        }

        // 공격·추적을 끊고 들어오므로 이동 플래그를 꺼야 연출이 끝난 뒤 Idle에서 다시 출발하지 않음
        protected override void OnPatternEnter()
        {
            Animator.SetBool(BossAnimParams.IS_CHASING, false);
            Animator.SetBool(BossAnimParams.IS_STRAFING, false);
            Animator.SetBool(BossAnimParams.IS_TURNING, false);

            if (combatEntity != null) combatEntity.IsInvincible = true;
            base.OnPatternEnter();
        }

        protected override void OnPatternExit()
        {
            if (combatEntity != null) combatEntity.IsInvincible = false;
            base.OnPatternExit();
        }
    }
}
