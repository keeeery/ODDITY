using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        [SerializeField] private AudioSource bgmSource;
        private AudioLowPassFilter lowPass;

        private CancellationTokenSource bgmCts;

        // 씬에 없어 자동 생성될 때는 AddComponent의 Awake와 Instance 쪽에서 두 번 불리므로, 이미 만들었으면 그대로 씀
        // TryGetComponent로 찾으면 같은 오브젝트에 붙은 bgmSource를 집어 효과음이 BGM 페이드에 같이 묻힐 수 있어 따로 만듦
        protected override void OnInitialize()
        {
            if (source != null) return;

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            bgmCts?.Cancel();
            bgmCts?.Dispose();
        }

        // 0에서부터 fadeTime초 동안 키우며 틂, 지금 나오는 곡은 StopBGM으로 먼저 줄여 둬야 끊기지 않음
        public void PlayBGM(string clipName, float volume = 1f, float fadeTime = 0.5f)
        {
            if (TryGetClip(clipName, out AudioClip clip) == false) return;

            bgmSource.clip = clip;
            bgmSource.volume = 0f;
            bgmSource.Play();

            FadeBGMAsync(volume, fadeTime, ResetBGMToken()).Forget();
        }

        public void StopBGM(float fadeTime = 0.5f) => FadeBGMAsync(0f, fadeTime, ResetBGMToken()).Forget();

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

        // 페이드 도중 새 요청이 오면 이전 페이드를 끊고 지금 볼륨에서 다시 시작
        private CancellationToken ResetBGMToken()
        {
            bgmCts?.Cancel();
            bgmCts?.Dispose();
            bgmCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            return bgmCts.Token;
        }

        // 히트스톱·슬로모션과 상관없이 실제 시간으로 페이드, 0까지 줄였으면 멈춤
        private async UniTaskVoid FadeBGMAsync(float endVolume, float duration, CancellationToken token)
        {
            try
            {
                await bgmSource
                    .DOFade(endVolume, duration)
                    .SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.Kill, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (endVolume <= 0f) bgmSource.Stop();
        }

        // Resources/Audios 기준 경로(확장자 제외)의 클립을 한 번 재생
        public void Play(string clipName, float volume = 1f) => TryPlay(clipName, volume, out _);

        // 재생 길이만큼 기다릴 때 쓰도록 재생한 클립을 함께 돌려줌
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
