using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 생명 착취. 매개변수 1개: Amount(int, 최대 체력의 %)
    // - 적에게 피해를 줄 때마다 최대 체력의 Amount%를 회복한다. 적어도 1은 회복한다
    // - 체력이 가득 차 있으면 발동하지 않고 내부 쿨타임도 돌지 않는다
    public class LifeStealSkill : PassiveSkill
    {
        private readonly int amount;

        public LifeStealSkill(string name, float cooldown, int amount) : base(name, cooldown)
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
            int heal = Mathf.Max(1, context.Owner.CurrentStats.MaxHp * amount / 100);
            CombatManager.Instance.AddCombatEvent(new HealEvent(context.Owner, context.Owner, heal));
        }
    }
}
