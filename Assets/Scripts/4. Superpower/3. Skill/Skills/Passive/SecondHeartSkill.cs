namespace TeamOdd.Superpower
{
    // 두 번째 심장. 매개변수 1개: Amount(int, 최대 체력의 %)
    // - 죽을 피해를 받으면 죽지 않고 체력을 최대 체력의 Amount%로 맞춘다
    // - 쿨타임 999999는 사실상 한 번이라는 뜻. 마을과 아레나 사이를 오갈 때 쿨타임이 초기화되므로 아레나 한 판에 한 번이다
    public class SecondHeartSkill : PassiveSkill
    {
        private readonly int amount;

        public SecondHeartSkill(string name, float cooldown, int amount) : base(name, cooldown)
        {
            this.amount = amount;
        }

        public override PassiveTrigger Trigger => PassiveTrigger.BeforeDeath;

        public override string Describe() => $"부활 체력 최대 체력의 {amount}%";

        protected override void Execute(SkillContext context)
        {
            context.Owner.SetHp(context.Owner.CurrentStats.MaxHp * amount / 100);
        }
    }
}
