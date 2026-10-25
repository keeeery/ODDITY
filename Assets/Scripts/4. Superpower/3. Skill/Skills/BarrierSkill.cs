using UnityEngine;

namespace TeamOdd.Superpower
{
    // 피해를 막는 보호막. 매개변수 2개: Shield(int), Duration(float)
    public class BarrierSkill : SuperpowerSkill
    {
        private readonly int shield;     // 막아 주는 피해량
        private readonly float duration; // 유지 시간 (초)

        public BarrierSkill(string name, float cooldown, int shield, float duration) : base(name, cooldown)
        {
            this.shield = shield;
            this.duration = duration;
        }

        public override string Describe() => $"보호막 {shield} · {duration}초";

        protected override void Execute(SkillContext context)
        {
            Debug.Log($"[{Name}] 피해 {shield}까지 막는 보호막을 {duration}초 동안 두른다.");
        }
    }
}
