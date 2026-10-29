using TeamOdd.TimeControl;

namespace TeamOdd.Superpower
{
    // 플레이어를 제외한 모든 TimeFreezable(보스, 염동력 오브젝트, 파편)의 시간을 멈춘다. 매개변수 1개: Duration(float)
    public class TimeStopSkill : SuperpowerSkill
    {
        private readonly float duration; // 멈추는 시간 (초)

        public TimeStopSkill(string name, float cooldown, float duration) : base(name, cooldown)
        {
            this.duration = duration;
        }

        public override string Describe() => $"시간 정지 {duration}초";

        protected override void Execute(SkillContext context)
        {
            TimeFreezeManager.Instance.FreezeWorld(duration);
        }
    }
}
