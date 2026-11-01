using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 체력 회복. 매개변수 1개: Amount(int)
    // - 체력이 가득 차 있으면 쓰지 않고 쿨타임도 돌지 않는다
    // - 회복하면 SkillChip.tsv의 PrefabKey로 불러온 이펙트를 플레이어 발밑에 띄운다. 플레이어 자식으로 붙여 움직여도 따라다닌다
    //   이펙트를 지우는 건 프리팹이 한다 (파티클 Stop Action = Destroy, 또는 DestroyObject 컴포넌트)
    public class HealSkill : SuperpowerSkill
    {
        private readonly int amount;
        private readonly GameObject effect; // SkillChip.tsv의 PrefabKey로 불러온 이펙트 프리팹

        public HealSkill(string name, float cooldown, int amount, GameObject effect) : base(name, cooldown)
        {
            this.amount = amount;
            this.effect = effect;
        }

        public override string Describe() => $"회복량 {amount}";

        protected override bool CanExecute(SkillContext context)
        {
            if (context.Owner.Hp < context.Owner.CurrentStats.MaxHp) return true;

            Debug.Log($"[{Name}] 체력이 가득 차 있습니다.");
            return false;
        }

        // 피해와 같이 CombatManager에 회복 이벤트로 넘긴다. 보낸 쪽도 받는 쪽도 플레이어 자신
        protected override void Execute(SkillContext context)
        {
            CombatManager.Instance.AddCombatEvent(new HealEvent(context.Owner, context.Owner, amount));
            Object.Instantiate(effect, context.Caster);
        }
    }
}
