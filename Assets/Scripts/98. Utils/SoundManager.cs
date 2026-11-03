using System.Collections.Generic;
using Jay;
using UnityEngine;

namespace TeamOdd
{
    public class SoundManager : SingletonBase<SoundManager>
    {
        // Resources 기준 사운드 폴더, AnimStateSoundSender의 클립 목록도 이 폴더에서 찾음
        public const string AUDIO_ROOT = "Audios";

        private readonly Dictionary<string, AudioClip> clipMap = new();

        private AudioSource source;

        // 씬에 없어 자동 생성될 때는 AddComponent의 Awake와 Instance 쪽에서 두 번 불리므로, 이미 붙어 있으면 그대로 씀
        protected override void OnInitialize()
        {
            if (TryGetComponent(out source) == false) source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
        }

        /// <summary>
        /// Resources/Audios 기준 경로(확장자 제외)의 클립을 한 번 재생
        /// </summary>
        public void Play(string clipName, float volume = 1f) => TryPlay(clipName, volume, out _);

        /// <summary>
        /// Play와 같고, 재생한 클립을 함께 돌려줌 (재생 길이만큼 기다릴 때 사용)
        /// </summary>
        public bool TryPlay(string clipName, float volume, out AudioClip clip)
        {
            if (TryGetClip(clipName, out clip) == false) return false;

            source.PlayOneShot(clip, volume);
            return true;
        }

        // 못 찾은 이름도 null로 남겨, 같은 이름을 다시 요청할 때 매번 Resources를 뒤지며 경고하지 않게 함
        private bool TryGetClip(string clipName, out AudioClip result)
        {
            result = null;

            if (string.IsNullOrEmpty(clipName)) return false;
            if (clipMap.TryGetValue(clipName, out result)) return result != null;

            result = Resources.Load<AudioClip>($"{AUDIO_ROOT}/{clipName}");
            clipMap.Add(clipName, result);

            if (result == null)
            {
                Debug.LogWarning($"[SoundManager] Clip({clipName}) is not found in Resources/{AUDIO_ROOT}");
                return false;
            }

            return true;
        }
    }
}
