using UnityEngine;

namespace TeamOdd.Superpower
{
    // 바라보는 방향으로 순간 이동. 매개변수 2개: Distance(float), PassThrough(bool)
    public class BlinkSkill : SuperpowerSkill
    {
        private readonly float distance;
        private readonly bool passThrough; // 벽을 통과할 수 있는지

        public BlinkSkill(string name, float cooldown, float distance, bool passThrough) : base(name, cooldown)
        {
            this.distance = distance;
            this.passThrough = passThrough;
        }

        public override string Describe() => $"거리 {distance}m · 벽 통과 {(passThrough ? "O" : "X")}";

        protected override void Execute(SkillContext context)
        {
            Vector3 destination = context.Caster.position + context.AimDirection * distance;
            string wall = passThrough ? "벽을 통과해" : "벽 앞에서 멈추며";
            Debug.Log($"[{Name}] {wall} {distance}m 앞 {destination}(으)로 순간 이동한다.");
        }
    }
}
