using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jay;
using Sirenix.OdinInspector;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamOdd
{
    // 씬 이동 연출. 사망 화면(DeathScreen)처럼 화면이 지직거리다 가로 선 → 점으로 꺼지고,
    // 새 씬에서는 거꾸로 점 → 선 → 화면으로 켜진 뒤 잔 글리치가 사그라든다.
    // 화면 효과는 ScreenGlitch(PC_Renderer의 Screen Glitch 렌더러 기능), 다 꺼진 동안은 fadeScreen 으로 UI 까지 덮는다.
    public class SceneLoadManager : SingletonBase<SceneLoadManager>
    {
        [Title("Components")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image fadeScreen;

        [Title("Fade Out (초, 시간 정지에도 흐름)")]
        [SerializeField, Min(0.01f)] private float glitchDuration = 0.5f;
        // 화면이 가로 선으로 접히는 시간
        [SerializeField, Min(0.01f)] private float foldDuration = 0.5f;
        // 선이 점으로 줄어드는 시간
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float dotFadeDuration = 0.4f;

        [Title("Fade In (초, 시간 정지에도 흐름)")]
        [SerializeField, Min(0.01f)] private float dotAppearDuration = 0.3f;
        // 점이 가로 선으로 늘어나는 시간
        [SerializeField, Min(0.01f)] private float expandDuration = 0.4f;
        // 선이 위아래로 펼쳐지는 시간
        [SerializeField, Min(0.01f)] private float unfoldDuration = 0.5f;
        // 켜진 뒤 남은 글리치가 사그라드는 시간과 세기
        [SerializeField, Min(0.01f)] private float settleDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float settleGlitch = 0.5f;

        private Sequence sequence;
        private bool isLoading;

        public event Action<string> OnFadeOutStarted;
        public event Action<string> OnFadeOutCompleted;
        public event Action<string> OnSceneLoaded;
        public event Action<string> OnFadeInCompleted;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            sequence?.Kill();
            if (isLoading) ScreenGlitch.Clear();
        }

        public async UniTask LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoadManager] Scene name is null or whitespace.");
                return;
            }
            if (isLoading) return;

            isLoading = true;
            canvas.enabled = true;
            SetFadeAlpha(0f);
            ScreenGlitch.Clear();

            OnFadeOutStarted?.Invoke(sceneName);

            await PlayFadeOut();
            OnFadeOutCompleted?.Invoke(sceneName);

            await SceneManager.LoadSceneAsync(sceneName);
            OnSceneLoaded?.Invoke(sceneName);

            await PlayFadeIn();
            OnFadeInCompleted?.Invoke(sceneName);

            canvas.enabled = false;
            isLoading = false;
        }

        [Button]
        public void LoadScene(string sceneName)
        {
            _ = LoadSceneAsync(sceneName);
        }

        // 테스트용: 씬은 옮기지 않고 꺼짐 → 잠시 검은 화면 → 켜짐 연출만 재생한다 (플레이 모드에서)
        [Button("Preview Transition")]
        public void PreviewTransition(float blackHold = 0.5f)
        {
            _ = PreviewTransitionAsync(blackHold);
        }

        public async UniTask PreviewTransitionAsync(float blackHold = 0.5f)
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("[SceneLoadManager] 연출 미리보기는 플레이 모드에서만 됩니다.");
                return;
            }
            if (isLoading) return;

            isLoading = true;
            canvas.enabled = true;
            SetFadeAlpha(0f);
            ScreenGlitch.Clear();

            await PlayFadeOut();
            await UniTask.Delay(TimeSpan.FromSeconds(blackHold), DelayType.Realtime);
            await PlayFadeIn();

            canvas.enabled = false;
            isLoading = false;
        }

        // 글리치가 세지다 끊김 → 가로 선으로 접힘 → 점으로 줄어듦 → 점이 사라짐 → 검은 화면으로 UI 까지 덮음
        private UniTask PlayFadeOut()
        {
            sequence?.Kill();
            sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            ScreenOffEffect.Insert(sequence, 0f, glitchDuration, foldDuration, shrinkDuration, dotFadeDuration);
            sequence.OnComplete(() => SetFadeAlpha(1f));
            return sequence.ToUniTask();
        }

        // 꺼진 화면에서 점이 켜짐 → 가로 선으로 늘어남 → 위아래로 펼쳐짐 → 남은 글리치가 사그라듦
        private UniTask PlayFadeIn()
        {
            float dotEnd = dotAppearDuration;
            float expandEnd = dotEnd + expandDuration;
            float unfoldEnd = expandEnd + unfoldDuration;

            // 검은 화면을 걷는 같은 프레임에 꺼진 상태로 맞춰 새 씬이 한 번에 보이지 않게 한다
            ScreenGlitch.PowerOff = 3f;
            SetFadeAlpha(0f);

            sequence?.Kill();
            sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            sequence.Insert(0f, DOTween.To(() => ScreenGlitch.PowerOff, SetPowerOff, 2f, dotAppearDuration).SetEase(Ease.OutQuad));
            sequence.Insert(dotEnd, DOTween.To(() => ScreenGlitch.PowerOff, SetPowerOff, 1f, expandDuration).SetEase(Ease.OutCubic));
            sequence.Insert(expandEnd, DOTween.To(() => ScreenGlitch.PowerOff, SetPowerOff, 0f, unfoldDuration).SetEase(Ease.OutCubic));
            sequence.InsertCallback(expandEnd + unfoldDuration * 0.5f, () => ScreenGlitch.Glitch = settleGlitch);
            sequence.Insert(unfoldEnd, DOTween.To(() => ScreenGlitch.Glitch, SetGlitch, 0f, settleDuration).SetEase(Ease.OutQuad));
            sequence.OnComplete(ScreenGlitch.Clear);
            return sequence.ToUniTask();
        }

        private void SetFadeAlpha(float alpha)
        {
            Color color = fadeScreen.color;
            color.a = alpha;
            fadeScreen.color = color;
        }

        private static void SetGlitch(float value) => ScreenGlitch.Glitch = value;
        private static void SetPowerOff(float value) => ScreenGlitch.PowerOff = value;
    }
}
