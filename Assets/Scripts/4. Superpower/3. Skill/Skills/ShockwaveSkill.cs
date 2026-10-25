using UnityEngine;

namespace TeamOdd.Superpower
{
    // 주변을 밀어내는 충격파. 매개변수 3개: Radius(float), Force(float), Damage(int)
    public class ShockwaveSkill : SuperpowerSkill
    {
        private readonly float radius;
        private readonly float force;
        private readonly int damage;

        public ShockwaveSkill(string name, float cooldown, float radius, float force, int damage) : base(name, cooldown)
        {
            this.radius = radius;
            this.force = force;
            this.damage = damage;
        }

        public override string Describe() => $"반경 {radius}m · 미는 힘 {force} · 피해 {damage}";

        protected override void Execute(SkillContext context)
        {
            Debug.Log($"[{Name}] {context.Caster.position}을 중심으로 반경 {radius}m 안의 적에게 피해 {damage}를 주고 {force}의 힘으로 밀어낸다.");
        }
    }
}
