using System;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 체력 회복. 매개변수 1개: Amount(int)
    // - 체력이 가득 차 있으면 쓰지 않고 쿨타임도 돌지 않는다
    // - 이펙트 프리팹 없이 OnHealed만 알린다. PlayerHUD가 받아서 화면 가장자리를 초록색으로 물들인다 (피격 비네트의 반대)
    public class HealSkill : SuperpowerSkill
    {
        // 회복 스킬을 쓴 순간. 흡혈 같은 다른 회복까지 깜빡이면 화면이 시끄러워서 이 스킬만 따로 알린다
        public static event Action OnHealed;

        private readonly int amount;

        public HealSkill(string name, float cooldown, int amount) : base(name, cooldown)
        {
            this.amount = amount;
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
            OnHealed?.Invoke();
        }
    }
}
