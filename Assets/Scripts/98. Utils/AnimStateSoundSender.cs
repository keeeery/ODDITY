using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd
{
    // 애니메이터 상태 진행률(0~1)에 맞춰 SoundManager에 사운드 재생을 요청함
    // 상태가 중간에 끊기면 남은 사운드는 재생하지 않음 (끊긴 공격 모션 뒤에 타격음이 나오지 않도록 OnStateExit에서 마저 보내지 않음)
    public class AnimStateSoundSender : StateMachineBehaviour
    {
        [Serializable]
        public class AnimStateSound
        {
            [SerializeField, Range(0f, 1f)] private float time = 0f;
            [SerializeField, InlineProperty, HideLabel] private SoundData sound;

            public float NormalizedTime => time;
            public SoundData Sound => sound;
            public bool IsComplete { get; set; }
        }

        [Title("Settings")]
        [SerializeField] private AnimStateSound[] sounds;

        private int currLoop = 0;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            currLoop = 0;
            ResetSounds();
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float normalizedTime = stateInfo.normalizedTime;

            // 루프가 아닌 상태도 끝난 뒤 머물러 있으면 normalizedTime이 1을 넘어가므로, 루프 상태일 때만 바퀴를 셈
            if (stateInfo.loop)
            {
                int loop = Mathf.FloorToInt(normalizedTime);

                if (loop != currLoop)
                {
                    // 프레임이 끊겨 지난 바퀴 끝부분(time = 1 등)을 건너뛰었으면 바퀴를 넘기기 전에 마저 재생
                    PlayRemaining();

                    currLoop = loop;
                    ResetSounds();
                }

                normalizedTime -= currLoop;
            }

            for (int i = 0; i < sounds.Length; i++)
            {
                AnimStateSound sound = sounds[i];
                if (sound.NormalizedTime > normalizedTime) continue;

                TryPlay(sound);
            }
        }

        private void ResetSounds()
        {
            for (int i = 0; i < sounds.Length; i++)
                sounds[i].IsComplete = false;
        }

        private void PlayRemaining()
        {
            for (int i = 0; i < sounds.Length; i++)
                TryPlay(sounds[i]);
        }

        private void TryPlay(AnimStateSound sound)
        {
            if (sound.IsComplete) return;

            sound.IsComplete = true;
            sound.Sound.Play();
        }
    }
}
