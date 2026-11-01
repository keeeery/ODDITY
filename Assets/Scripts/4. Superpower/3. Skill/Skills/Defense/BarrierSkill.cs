using UnityEngine;

namespace TeamOdd.Superpower
{
    // 피해를 막는 보호막. 매개변수 2개: Shield(int), Duration(float)
    // - Duration초 동안 받는 피해를 Shield까지 대신 받는다. 실제로 막는 계산은 CombatEntity.TakeDamage가 한다
    // - 보호막이 남아 있을 때 다시 쓰면 새 보호막으로 바뀐다 (쌓이지 않음)
    // - 보호막이 있는 동안 SkillChip.tsv의 PrefabKey로 불러온 이펙트를 플레이어에게 씌운다. 깨지거나 시간이 끝나면 BarrierEffect가 지운다
    public class BarrierSkill : SuperpowerSkill
    {
        private readonly int shield;     // 막아 주는 피해량
        private readonly float duration; // 유지 시간 (초)
        private readonly GameObject effect; // SkillChip.tsv의 PrefabKey로 불러온 이펙트 프리팹

        private GameObject currentEffect; // 지금 씌워 둔 이펙트. 다시 쓰면 지우고 새로 씌운다

        public BarrierSkill(string name, float cooldown, int shield, float duration, GameObject effect) : base(name, cooldown)
        {
            this.shield = shield;
            this.duration = duration;
            this.effect = effect;
        }

        public override string Describe() => $"보호막 {shield} · {duration}초";

        protected override void Execute(SkillContext context)
        {
            context.Owner.SetShield(shield, duration);

            // 이전 이펙트는 새 보호막이 생겨도 남아 있으므로 직접 지운다. 이미 사라졌으면 Unity가 null로 알려 준다
            if (currentEffect != null)
                Object.Destroy(currentEffect);

            currentEffect = Object.Instantiate(effect, context.Caster);
            currentEffect.AddComponent<BarrierEffect>().Bind(context.Owner);
        }
    }
}
