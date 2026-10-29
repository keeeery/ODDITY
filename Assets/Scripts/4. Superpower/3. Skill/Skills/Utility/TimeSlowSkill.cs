using TeamOdd.TimeControl;

namespace TeamOdd.Superpower
{
    // 플레이어를 제외한 모든 TimeFreezable의 시간을 Multiplier배 빠르기로 느리게 흐르게 한다. 매개변수 2개: Duration(float), Multiplier(float)
    public class TimeSlowSkill : SuperpowerSkill
    {
        private readonly float duration;   // 느려지는 시간 (초)
        private readonly float multiplier; // 느려진 동안의 빠르기 배율 (0.5면 절반 빠르기)

        public TimeSlowSkill(string name, float cooldown, float duration, float multiplier) : base(name, cooldown)
        {
            this.duration = duration;
            this.multiplier = multiplier;
        }

        public override string Describe() => $"{duration}초 동안 {multiplier}배 만큼 감속";

        protected override void Execute(SkillContext context)
        {
            TimeFreezeManager.Instance.FreezeWorld(duration, multiplier);
        }
    }
}
