using UnityEngine;

namespace TeamOdd.Superpower
{
    // Duration초 전의 자리·체력·스태미나로 되돌아간다. 매개변수 1개: Duration(float)
    // - 지나온 모습을 거꾸로 되짚으며 돌아간다 (되감는 동안 무적)
    // - 기록이 Duration초보다 짧으면(막 되감은 직후 등) 가장 오래된 기록까지만 간다
    // - 기록하고 되감는 일은 TimeReverseRecorder, 되감는 동안의 화면 효과는 TimeReverseEffect가 한다 (둘 다 플레이어 프리팹에 붙어 있다)
    public class TimeReverseSkill : SuperpowerSkill
    {
        private const float RewindSoundFadeTime = 0.1f; // 되감기가 끝날 때 소리가 잦아드는 시간 (초)

        private readonly float duration; // 되돌아갈 시간 (초)

        private TimeReverseRecorder recorder; // 지금 되감는 플레이어의 기록. 되감기가 끝나면 소리를 멈추려고 기억한다
        private AudioSource rewindSound;

        public TimeReverseSkill(string name, float cooldown, float duration) : base(name, cooldown)
        {
            this.duration = duration;
        }

        public override string Describe() => $"{duration}초 전으로 역행";

        // 되감는 중이거나 되돌아갈 기록이 없으면 쓰지 않는다
        protected override bool CanExecute(SkillContext context)
        {
            TimeReverseRecorder recorder = context.Caster.GetComponent<TimeReverseRecorder>();
            return recorder.IsRewinding == false && recorder.HasHistory;
        }

        protected override void Execute(SkillContext context)
        {
            recorder = context.Caster.GetComponent<TimeReverseRecorder>();

            // 테이프 되감는 소리(SkillChip.tsv의 Rewind)를 반복해서 틀다가 되감기가 끝나면 멈춘다. 그래서 되감는 길이가 달라도 소리가 맞는다
            rewindSound = Sounds.PlayLoop("Rewind");
            recorder.RewindFinished += OnRewindFinished;

            recorder.Rewind(duration);
        }

        private void OnRewindFinished()
        {
            recorder.RewindFinished -= OnRewindFinished;
            SkillSounds.StopLoop(rewindSound, RewindSoundFadeTime);
        }
    }
}
