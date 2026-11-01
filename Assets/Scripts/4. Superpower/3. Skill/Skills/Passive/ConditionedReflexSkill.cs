using TeamOdd.Player;

namespace TeamOdd.Superpower
{
    // 조건 반사. 매개변수 없음
    // - 피해를 받기 직전에 그 피해를 없애고 기본 구르기를 그대로 실행한다. 스태미나는 쓰지 않는다
    // - 직접 구를 수 없을 때(피격 중, 염동 내려찍기 등을 시전하는 중)는 발동하지 않는다. 구르기가 그 동작을 끊어 버리기 때문이다
    // - 자동 회피(DodgeType.Auto)로 구르고 자동 회피 피드백을 띄운다. 이 구르기 도중에 피한 공격도 자동 회피로 보인다
    public class ConditionedReflexSkill : PassiveSkill
    {
        public ConditionedReflexSkill(string name, float cooldown) : base(name, cooldown) { }

        public override PassiveTrigger Trigger => PassiveTrigger.BeforeTakeDamage;

        public override string Describe() => "피해를 받기 직전에 자동으로 구른다";

        protected override bool CanExecute(SkillContext context)
        {
            PlayerController controller = context.Caster.GetComponent<PlayerController>();
            return controller.IsHit == false && controller.IsActionLocked == false;
        }

        // 잔상이 구르기 전 자세로 남도록 피드백을 먼저 띄운다
        protected override void Execute(SkillContext context)
        {
            DodgeEffect.Play(context.Caster, DodgeType.Auto);
            context.Caster.GetComponent<PlayerController>().InputRolling(DodgeType.Auto);
        }
    }
}
