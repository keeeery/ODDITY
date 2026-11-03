using UnityEngine;

namespace TeamOdd.Superpower
{
    // 두 번째 심장. 매개변수 1개: Amount(int, 최대 체력의 %)
    // - 죽을 피해를 받으면 죽지 않고 체력을 최대 체력의 Amount%로 맞춘다
    // - 쿨타임 999999는 사실상 한 번이라는 뜻. 마을과 아레나 사이를 오갈 때 쿨타임이 초기화되므로 아레나 한 판에 한 번이다
    // - 살아나면 부활 연출(SecondHeartRevival)을 플레이어에 붙인다. 쓰러졌다가 일어서는 2~3초 동안 조작이 막히고 무적이다
    //   일어서는 순간 SkillChip.tsv의 PrefabKey로 불러온 심장 박동 이펙트가 터진다
    public class SecondHeartSkill : PassiveSkill
    {
        private readonly int amount;
        private readonly GameObject effect; // SkillChip.tsv의 PrefabKey로 불러온 이펙트 프리팹

        public SecondHeartSkill(string name, float cooldown, int amount, GameObject effect) : base(name, cooldown)
        {
            this.amount = amount;
            this.effect = effect;
        }

        public override PassiveTrigger Trigger => PassiveTrigger.BeforeDeath;

        public override string Describe() => $"부활 체력 최대 체력의 {amount}%";

        // 체력은 바로 채우지 않고 부활 연출에 넘긴다. 쓰러지는 동안 0에 가깝게 보이다가 일어설 때 확 찬다
        protected override void Execute(SkillContext context)
        {
            int reviveHp = context.Owner.CurrentStats.MaxHp * amount / 100;
            context.Caster.gameObject.AddComponent<SecondHeartRevival>().Play(effect, reviveHp, Sounds);
        }
    }
}
