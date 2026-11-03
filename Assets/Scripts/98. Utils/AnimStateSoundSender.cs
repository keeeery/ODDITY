using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd
{
    // 애니메이터 상태 진행률(0~1)에 맞춰 SoundManager에 사운드 재생을 요청함
    // 상태가 중간에 끊기면 남은 사운드는 재생하지 않음 (끊긴 공격 모션 뒤에 타격음이 나오지 않도록 OnStateExit에서 마저 보내지 않음)
    // 진행률이 앞으로 흐를 때만 재생함 (시간 역행처럼 Play로 뒤로 옮기거나 멈춘 동안에는 지금 자리에 맞추기만 하고 소리는 내지 않음)
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

        // 오프셋 없이 들어와도 첫 프레임에 진행률이 조금 지나 있으므로, 이보다 앞선 소리만 놓친 것으로 봄
        private const float ENTRY_TOLERANCE = 0.05f;

        [Title("Settings")]
        [SerializeField] private AnimStateSound[] sounds;

        [Tooltip("다른 상태로 넘어가는 전환 중에는 재생하지 않음 (걷기→달리기처럼 두 상태 발소리가 겹치지 않게)")]
        [SerializeField] private bool skipWhileExiting = false;

        [Tooltip("같은 애니메이터에서 이 옵션을 쓰는 소리끼리 최소 간격(초), 0이면 제한 없음")]
        [SerializeField, Min(0f)] private float minInterval = 0f;

        // 애니메이터별 마지막 재생 시각, 걷기·달리기처럼 다른 상태의 Behaviour끼리 간격을 공유함
        private static readonly Dictionary<int, float> lastPlayTimes = new Dictionary<int, float>();

        private int currLoop = 0;
        private float prevNormalizedTime = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearPlayTimes() => lastPlayTimes.Clear();

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 오프셋을 두고 들어오면 이미 지난 시점의 소리를 첫 프레임에 몰아서 내지 않도록 건너뜀
            Sync(stateInfo, -ENTRY_TOLERANCE);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float normalizedTime = stateInfo.normalizedTime;
            bool movedBack = normalizedTime < prevNormalizedTime;
            prevNormalizedTime = normalizedTime;

            // 멈췄거나 뒤로 옮겨졌으면 지금 자리까지 낸 것으로 맞춤 (되감기가 루프 경계를 넘을 때 소리가 몰려 나오지 않게)
            if (movedBack || animator.speed <= 0f)
            {
                Sync(stateInfo, 0f);
                return;
            }

            bool mute = skipWhileExiting && IsExiting(animator, stateInfo, layerIndex);

            // 루프가 아닌 상태도 끝난 뒤 머물러 있으면 normalizedTime이 1을 넘어가므로, 루프 상태일 때만 바퀴를 셈
            if (stateInfo.loop)
            {
                int loop = Mathf.FloorToInt(normalizedTime);

                if (loop != currLoop)
                {
                    // 프레임이 끊겨 지난 바퀴 끝부분(time = 1 등)을 건너뛰었으면 바퀴를 넘기기 전에 마저 재생
                    PlayRemaining(animator, mute);

                    currLoop = loop;
                    ResetSounds();
                }

                normalizedTime -= currLoop;
            }

            for (int i = 0; i < sounds.Length; i++)
            {
                AnimStateSound sound = sounds[i];
                if (sound.NormalizedTime > normalizedTime) continue;

                TryPlay(animator, sound, mute);
            }
        }

        // 지금 진행률(여유 포함)보다 앞선 소리는 낸 것으로, 나머지는 아직 안 낸 것으로 맞춤
        private void Sync(AnimatorStateInfo stateInfo, float tolerance)
        {
            float normalizedTime = stateInfo.normalizedTime;
            prevNormalizedTime = normalizedTime;
            currLoop = stateInfo.loop ? Mathf.FloorToInt(normalizedTime) : 0;

            float progress = normalizedTime - currLoop + tolerance;
            for (int i = 0; i < sounds.Length; i++)
                sounds[i].IsComplete = sounds[i].NormalizedTime <= progress;
        }

        // 이 상태에서 다른 상태로 넘어가는 중인지 (자기 자신으로의 전환은 제외)
        private static bool IsExiting(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (animator.IsInTransition(layerIndex) == false) return false;

            return animator.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash == stateInfo.fullPathHash
                   && animator.GetNextAnimatorStateInfo(layerIndex).fullPathHash != stateInfo.fullPathHash;
        }

        private void ResetSounds()
        {
            for (int i = 0; i < sounds.Length; i++)
                sounds[i].IsComplete = false;
        }

        private void PlayRemaining(Animator animator, bool mute)
        {
            for (int i = 0; i < sounds.Length; i++)
                TryPlay(animator, sounds[i], mute);
        }

        // 막힌 경우에도 낸 것으로 처리해 나중에 몰아서 나오지 않게 함
        private void TryPlay(Animator animator, AnimStateSound sound, bool mute)
        {
            if (sound.IsComplete) return;

            sound.IsComplete = true;
            if (mute || IsInCooldown(animator)) return;

            sound.Sound.Play();
        }

        private bool IsInCooldown(Animator animator)
        {
            if (minInterval <= 0f) return false;

            int id = animator.GetInstanceID();
            float now = Time.time;
            if (lastPlayTimes.TryGetValue(id, out float last) && now >= last && now - last < minInterval) return true;

            lastPlayTimes[id] = now;
            return false;
        }
    }
}
