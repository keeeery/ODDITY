using UnityEngine;

namespace TeamOdd.Superpower
{
    // 투사체를 부채꼴로 여러 발 쏜다. 매개변수 3개: Projectile(string), Count(int), Spread(float)
    public class BarrageSkill : SuperpowerSkill
    {
        private readonly string projectile; // 쏠 투사체 이름 (나중에 프리팹 키로 쓴다)
        private readonly int count;
        private readonly float spread;      // 부채꼴 전체 각도

        public BarrageSkill(string name, float cooldown, string projectile, int count, float spread) : base(name, cooldown)
        {
            this.projectile = projectile;
            this.count = count;
            this.spread = spread;
        }

        public override string Describe() => $"{projectile} {count}발 · 각도 {spread}도";

        protected override void Execute(SkillContext context)
        {
            Debug.Log($"[{Name}] {projectile} {count}발을 {context.AimDirection} 방향으로 {spread}도 부채꼴로 발사한다.");
        }
    }
}
