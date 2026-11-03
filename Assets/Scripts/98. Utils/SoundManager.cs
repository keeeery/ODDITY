using System.Collections;
using System.Collections.Generic;
using Jay;
using UnityEngine;

namespace TeamOdd
{
    public class SoundManager : SingletonBase<SoundManager>
    {
        // Resources 기준 사운드 폴더, AnimStateSoundSender의 클립 목록도 이 폴더에서 찾음
        public const string AUDIO_ROOT = "Audios";

        // SetLowPass(1)일 때의 저주파 필터 차단 주파수(Hz), 이보다 높은 소리가 깎여 먹먹하게 들림
        private const float LOW_PASS_CUTOFF = 600f;
        private const float NO_LOW_PASS_CUTOFF = 22000f;

        private readonly Dictionary<string, AudioClip> clipMap = new();

        private AudioSource source;
        private AudioLowPassFilter lowPass;

        // 씬에 없어 자동 생성될 때는 AddComponent의 Awake와 Instance 쪽에서 두 번 불리므로, 이미 붙어 있으면 그대로 씀
        protected override void OnInitialize()
        {
            if (TryGetComponent(out source) == false) source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
        }

        /// <summary>
        /// 클립을 반복 재생하고, 멈출 때 넘길 AudioSource를 돌려줌 (클립이 없으면 null)
        /// 받은 쪽이 pitch 등을 바꿔도 되고, 다 쓰면 반드시 StopLoop로 멈춰야 함
        /// </summary>
        public AudioSource PlayLoop(string clipName, float volume = 1f)
        {
            if (TryGetClip(clipName, out AudioClip clip) == false) return null;

            AudioSource loop = gameObject.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.clip = clip;
            loop.volume = volume;
            loop.loop = true;
            loop.Play();

            return loop;
        }

        /// <summary>
        /// PlayLoop로 튼 소리를 fadeTime초 동안 줄인 뒤 멈춤 (0이면 바로)
        /// 소리를 튼 오브젝트가 먼저 사라져도 SoundManager가 끝까지 줄임
        /// </summary>
        public void StopLoop(AudioSource loop, float fadeTime = 0f)
        {
            if (fadeTime <= 0f) Destroy(loop);
            else StartCoroutine(FadeOutRoutine(loop, fadeTime));
        }

        /// <summary>
        /// 모든 소리에 저주파 필터를 씌움 (0이면 원래 소리, 1이면 가장 먹먹하게)
        /// AudioListener에 붙인 필터는 그 리스너가 듣는 모든 소리에 걸리므로, SoundManager 밖에서 튼 소리도 함께 먹먹해짐
        /// </summary>
        public void SetLowPass(float amount)
        {
            // 씬이 바뀌면 카메라와 함께 리스너도 바뀌므로 그때 다시 찾음, 끄려는 것이면 찾을 필요 없음
            if (lowPass == null)
            {
                if (amount <= 0f) return;

                AudioListener listener = FindAnyObjectByType<AudioListener>();
                if (listener == null) return;

                if (listener.TryGetComponent(out lowPass) == false) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }

            lowPass.enabled = amount > 0f;
            lowPass.cutoffFrequency = Mathf.Lerp(NO_LOW_PASS_CUTOFF, LOW_PASS_CUTOFF, amount);
        }

        // 히트스톱·슬로모션과 상관없이 실제 시간으로 줄임
        private IEnumerator FadeOutRoutine(AudioSource loop, float fadeTime)
        {
            float startVolume = loop.volume;

            for (float elapsed = 0f; elapsed < fadeTime; elapsed += Time.unscaledDeltaTime)
            {
                loop.volume = startVolume * (1f - elapsed / fadeTime);
                yield return null;
            }

            Destroy(loop);
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
