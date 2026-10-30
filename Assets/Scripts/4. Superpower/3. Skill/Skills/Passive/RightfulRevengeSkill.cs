using TeamOdd.Combat;

namespace TeamOdd.Superpower
{
    // 정당한 응보. 매개변수 1개: Amount(int, 한 번에 오르는 공격력 %)
    // - 피해를 받을 때마다 기본 공격력(칩 보너스를 뺀 값)의 Amount%씩 공격력이 쌓인다
    // - 공격력은 정수라 1%씩 바로 더하면 소수점이 버려진다. 그래서 몇 번 쌓였는지 세어 두고 보너스를 매번 다시 계산한다
    // - 마을과 아레나 사이를 오갈 때 쌓인 보너스를 모두 뺀다
    public class RightfulRevengeSkill : PassiveSkill
    {
        private readonly int amount;

        private CombatEntity owner; // 보너스를 넣은 곳. 초기화할 때 여기서 뺀다
        private int stacks;         // 지금까지 쌓인 횟수
        private int appliedBonus;   // 지금 owner에 들어가 있는 공격력 보너스

        public RightfulRevengeSkill(string name, float cooldown, int amount) : base(name, cooldown)
        {
            this.amount = amount;
        }

        public override PassiveTrigger Trigger => PassiveTrigger.TookDamage;

        public override string Describe() => $"공격력 증가 {amount}% (누적)";

        protected override void Execute(SkillContext context)
        {
            owner = context.Owner;
            stacks++;
            SetBonus(owner.Stats.Atk * amount * stacks / 100);
        }

        public override void ResetStacks()
        {
            if (owner == null)
                return;

            stacks = 0;
            SetBonus(0);
        }

        // 넣어 둔 보너스를 빼고 새 보너스를 넣는다
        private void SetBonus(int bonus)
        {
            owner.CurrentStats.Remove(StatType.Atk, appliedBonus);
            owner.CurrentStats.Add(StatType.Atk, bonus);
            appliedBonus = bonus;
        }
    }
}
