namespace TeamOdd.Superpower
{
    // 피해를 막는 보호막. 매개변수 2개: Shield(int), Duration(float)
    // - Duration초 동안 받는 피해를 Shield까지 대신 받는다. 실제로 막는 계산은 CombatEntity.TakeDamage가 한다
    // - 보호막이 남아 있을 때 다시 쓰면 새 보호막으로 바뀐다 (쌓이지 않음)
    public class BarrierSkill : SuperpowerSkill
    {
        private readonly int shield;     // 막아 주는 피해량
        private readonly float duration; // 유지 시간 (초)

        public BarrierSkill(string name, float cooldown, int shield, float duration) : base(name, cooldown)
        {
            this.shield = shield;
            this.duration = duration;
        }

        public override string Describe() => $"보호막 {shield} · {duration}초";

        protected override void Execute(SkillContext context)
        {
            context.Owner.SetShield(shield, duration);
        }
    }
}
