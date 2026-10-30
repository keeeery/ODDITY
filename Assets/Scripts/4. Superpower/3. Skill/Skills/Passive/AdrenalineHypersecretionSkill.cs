using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 아드레날린 과분비. 매개변수 1개: Amount(float, 줄일 쿨타임 초)
    // - 적에게 피해를 줄 때마다 쿨타임이 돌고 있는 스킬 슬롯의 액티브 스킬 하나를 무작위로 골라 쿨타임을 Amount초 줄인다
    // - 패시브는 고르지 않는다. 두 번째 심장처럼 한 번만 쓰라고 둔 긴 쿨타임까지 줄어들기 때문이다
    // - 쿨타임이 도는 스킬이 없으면 발동하지 않고 내부 쿨타임도 돌지 않는다
    public class AdrenalineHypersecretionSkill : PassiveSkill
    {
        private readonly float amount;

        private readonly List<SuperpowerSkill> candidates = new(); // 고를 수 있는 스킬. 매번 새로 채운다

        public AdrenalineHypersecretionSkill(string name, float cooldown, float amount) : base(name, cooldown)
        {
            this.amount = amount;
        }

        public override PassiveTrigger Trigger => PassiveTrigger.DealtDamage;

        public override string Describe() => $"쿨타임 감소 {amount}초";

        protected override bool CanExecute(SkillContext context)
        {
            FindCandidates(context);
            return candidates.Count > 0;
        }

        protected override void Execute(SkillContext context)
        {
            SuperpowerSkill target = candidates[Random.Range(0, candidates.Count)];
            target.ReduceCooldown(amount);
        }

        private void FindCandidates(SkillContext context)
        {
            candidates.Clear();
            AugmentationChipInventory inventory = context.Caster.GetComponent<AugmentationChipInventory>();
            foreach (OwnedChip chip in inventory.SkillSlots)
            {
                if (chip == null || chip.Skill is PassiveSkill || chip.Skill.IsReady)
                    continue;

                candidates.Add(chip.Skill);
            }
        }
    }
}
