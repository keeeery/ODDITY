using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jay;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        // 볼륨 설정(GameSettings)을 곱하기 전의 값, 실제 소스 볼륨 = 이 값 × 배경음/효과음 설정
        private float bgmLevel;
        private readonly Dictionary<AudioSource, float> loopLevels = new();
        // SoundManager 밖에서 씬에 놓인 소리(환경음 등)는 효과음으로 보고, 씬을 불러올 때 원래 볼륨을 기억해 둠
        private readonly Dictionary<AudioSource, float> sceneLevels = new();

        private CancellationTokenSource bgmCts;

        // 씬에 없어 자동 생성될 때는 AddComponent의 Awake와 Instance 쪽에서 두 번 불리므로, 이미 만들었으면 그대로 씀
        // TryGetComponent로 찾으면 같은 오브젝트에 붙은 bgmSource를 집어 효과음이 BGM 페이드에 같이 묻힐 수 있어 따로 만듦
        protected override void OnInitialize()
        {
            if (source != null) return;

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (bgmSource != null) bgmLevel = bgmSource.volume;

            GameSettings.VolumeChanged += ApplyVolumes;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RegisterSceneSources();
            ApplyVolumes();
        }

        protected override void OnDispose()
        {
            GameSettings.VolumeChanged -= ApplyVolumes;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            bgmCts?.Cancel();
            bgmCts?.Dispose();
        }

        // 새 BGM 데이터를 아직 메모리에 올리는 중인지, SceneLoadManager가 화면을 켜기 전에 기다림
        public bool IsBGMLoading => bgmSource != null && bgmSource.clip != null && bgmSource.clip.loadState == AudioDataLoadState.Loading;

        // 0에서부터 fadeTime초 동안 키우며 틂, 지금 나오는 곡은 StopBGM으로 먼저 줄여 둬야 끊기지 않음
        public void PlayBGM(string clipName, float volume = 1f, float fadeTime = 0.5f)
        {
            if (TryGetClip(clipName, out AudioClip clip) == false) return;

            PlayBGMAsync(clip, volume, fadeTime, ResetBGMToken()).Forget();
        }

        public void StopBGM(float fadeTime = 0.5f) => FadeBGMAsync(0f, fadeTime, ResetBGMToken()).Forget();

        // 클립 데이터가 아직 안 올라왔으면(백그라운드 로드·미리 로드 꺼짐) 다 올라온 뒤에 틂.
        // 그대로 Play하면 소리가 늦게 나는 동안 볼륨 페이드만 흘러, 페이드 중간부터 갑자기 들림
        private async UniTaskVoid PlayBGMAsync(AudioClip clip, float volume, float fadeTime, CancellationToken token)
        {
            SetBGMLevel(0f);
            bgmSource.clip = clip;

            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Loading)
            {
                bgmSource.Stop();
                try
                {
                    await UniTask.WaitWhile(() => clip.loadState == AudioDataLoadState.Loading, PlayerLoopTiming.Update, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning($"[SoundManager] BGM({clip.name}) 데이터를 불러오지 못했습니다.");
                return;
            }

            bgmSource.Play();
            await FadeBGMAsync(volume, fadeTime, token);
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
            loop.volume = volume * GameSettings.SfxOutput;
            loop.loop = true;
            loopLevels[loop] = volume;
            loop.Play();

            return loop;
        }

        /// <summary>
        /// PlayLoop로 튼 소리를 fadeTime초 동안 줄인 뒤 멈춤 (0이면 바로)
        /// 소리를 튼 오브젝트가 먼저 사라져도 SoundManager가 끝까지 줄임
        /// </summary>
        public void StopLoop(AudioSource loop, float fadeTime = 0f)
        {
            loopLevels.Remove(loop);

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
        private async UniTask FadeBGMAsync(float endVolume, float duration, CancellationToken token)
        {
            try
            {
                await DOTween
                    .To(() => bgmLevel, SetBGMLevel, endVolume, duration)
                    .SetTarget(bgmSource)
                    .SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.Kill, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (endVolume <= 0f) bgmSource.Stop();
        }

        #region VOLUME

        private void SetBGMLevel(float level)
        {
            bgmLevel = level;
            if (bgmSource != null) bgmSource.volume = bgmLevel * GameSettings.BgmOutput;
        }

        // 설정 창에서 슬라이더를 움직이면 지금 나오는 소리에도 바로 반영
        // 한 번 재생(PlayOneShot)은 source.volume이 곱해지므로 소스 볼륨만 맞추면 됨
        private void ApplyVolumes()
        {
            float sfx = GameSettings.SfxOutput;

            SetBGMLevel(bgmLevel);
            source.volume = sfx;

            foreach (KeyValuePair<AudioSource, float> pair in loopLevels)
                if (pair.Key != null) pair.Key.volume = pair.Value * sfx;

            foreach (KeyValuePair<AudioSource, float> pair in sceneLevels)
                if (pair.Key != null) pair.Key.volume = pair.Value * sfx;
        }

        // 보스 씬 등을 바로 실행해도 맵 환경음이 처음부터 효과음 볼륨을 따르도록, 첫 씬을 불러온 직후 SoundManager를 깨움
        // 씬에 놓인 SoundManager가 있으면 그것을 쓰고, 없을 때만 새로 만듦
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance() => _ = Instance;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterSceneSources();
            ApplyVolumes();
        }

        // 씬에 놓인 소스의 원래 볼륨을 기억함, SoundManager 자신의 소스(배경음·효과음·루프)는 따로 다룸
        // 이미 기억한 소스는 건드리지 않아야 설정 볼륨이 두 번 곱해지지 않음
        private void RegisterSceneSources()
        {
            List<AudioSource> destroyed = null;
            foreach (AudioSource key in sceneLevels.Keys)
                if (key == null) (destroyed ??= new List<AudioSource>()).Add(key);
            if (destroyed != null)
                foreach (AudioSource key in destroyed) sceneLevels.Remove(key);

            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (AudioSource sceneSource in sources)
            {
                if (sceneSource.transform.IsChildOf(transform)) continue;

                sceneLevels.TryAdd(sceneSource, sceneSource.volume);
            }
        }

        #endregion

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
        public bool TryGetClip(string clipName, out AudioClip result)
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
