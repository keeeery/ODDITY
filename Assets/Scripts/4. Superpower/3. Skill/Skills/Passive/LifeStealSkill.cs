using TeamOdd.Combat;

namespace TeamOdd.Superpower
{
    // 생명 착취. 매개변수 1개: Amount(float, 최대 체력의 %. 0.5처럼 소수도 된다)
    // - 적에게 피해를 줄 때마다 최대 체력의 Amount%를 회복한다
    // - 체력은 정수라서 소수점 회복량은 storedHeal에 모아 두고, 1 이상 모인 만큼만 회복한다
    //   예) 최대 체력 100, Amount 0.5 → 한 번에 0.5씩 모여서 두 번 때릴 때마다 1 회복
    // - 체력이 가득 차 있으면 발동하지 않고 내부 쿨타임도 돌지 않는다
    public class LifeStealSkill : PassiveSkill
    {
        private readonly float amount;
        private float storedHeal; // 아직 회복하지 못한 소수점 회복량

        public LifeStealSkill(string name, float cooldown, float amount) : base(name, cooldown)
        {
            this.amount = amount;
        }

        public override PassiveTrigger Trigger => PassiveTrigger.DealtDamage;

        public override string Describe() => $"회복량 최대 체력의 {amount}%";

        protected override bool CanExecute(SkillContext context)
        {
            return context.Owner.Hp < context.Owner.CurrentStats.MaxHp;
        }

        // 회복 스킬과 같이 CombatManager에 회복 이벤트로 넘긴다
        protected override void Execute(SkillContext context)
        {
            storedHeal += context.Owner.CurrentStats.MaxHp * amount / 100f;

            int heal = (int)storedHeal; // 정수 부분만 회복
            if (heal == 0) return;

            storedHeal -= heal;
            CombatManager.Instance.AddCombatEvent(new HealEvent(context.Owner, context.Owner, heal));
        }
    }
}
