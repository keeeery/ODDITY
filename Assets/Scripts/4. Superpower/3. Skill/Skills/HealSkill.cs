using UnityEngine;

namespace TeamOdd.Superpower
{
    // 체력 회복. 매개변수 1개: Amount(int)
    public class HealSkill : SuperpowerSkill
    {
        private readonly int amount;

        public HealSkill(string name, float cooldown, int amount) : base(name, cooldown)
        {
            this.amount = amount;
        }

        public override string Describe() => $"회복량 {amount}";

        protected override void Execute(SkillContext context)
        {
            Debug.Log($"[{Name}] 체력을 {amount} 회복한다. (지금 체력 {context.Owner.Hp}/{context.Owner.CurrentStats.MaxHp})");
        }
    }
}
