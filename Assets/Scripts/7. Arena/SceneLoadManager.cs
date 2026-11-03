using System;
using System.Collections.Generic;
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
    // 오버레이 UI는 렌더러 기능 뒤에 그려져 꺼지지 않으므로, 전환하는 동안 최상위 캔버스의 자식들을 화면 가운데로 셰이더와 같은 비율만큼 눌러
    // 월드와 함께 접히게 하고, 남은 범위 밖은 검은 띠(maskBars)로 덮는다. 끝나면 UI 위치·크기를 되돌린다.
    public class SceneLoadManager : SingletonBase<SceneLoadManager>
    {
        [Title("Components")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image fadeScreen;
        // 보상 창(200)·사망 화면(300) 등 다른 캔버스보다 위에 그리도록 전환하는 동안 쓰는 정렬 순서
        [SerializeField] private int sortingOrder = 1000;

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

        [Title("SFX Settings")]
        [SerializeField] private SoundData fadeInSound;

        private Sequence sequence;
        private bool isLoading;
        // 위·아래·왼쪽·오른쪽 띠, 처음 전환할 때 만든다
        private RectTransform[] maskBars;
        // 전환하는 동안 누르는 UI와 원래 위치·크기
        private readonly List<(RectTransform rect, Vector3 position, Vector3 scale)> squashedUI = new();

        public event Action<string> OnFadeOutStarted;
        public event Action<string> OnFadeOutCompleted;
        public event Action<string> OnSceneLoaded;
        public event Action<string> OnFadeInCompleted;

        private float FadeOutDuration => glitchDuration + foldDuration + shrinkDuration + dotFadeDuration;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            sequence?.Kill();
            if (isLoading) ScreenGlitch.Clear();
            RestoreUI();
        }

        // DOTween이 Update에서 PowerOff를 바꾼 뒤에 UI를 맞춘다
        private void LateUpdate()
        {
            if (isLoading) ApplyPowerOffToUI(ScreenGlitch.PowerOff);
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
            ShowCanvas();
            SetFadeAlpha(0f);
            ScreenGlitch.Clear();

            OnFadeOutStarted?.Invoke(sceneName);

            // 화면이 꺼지는 동안 지금 BGM을 줄여 다 꺼졌을 때는 조용하게 함, 새 BGM은 OnSceneLoaded에서 BGMPlayer가 키우며 틂
            SoundManager.Instance.StopBGM(FadeOutDuration);

            // 이전 씬 UI는 씬이 내려갈 때까지 점으로 눌린 채 둔다. 씬을 넘어 남는 UI는 PlayFadeIn 이 같은 프레임에 되돌렸다 다시 누른다
            await PlayFadeOut();
            OnFadeOutCompleted?.Invoke(sceneName);

            await SceneManager.LoadSceneAsync(sceneName);
            // 구독한 쪽(BGM 등)에서 예외가 나도 화면을 켜지 않으면 꺼진 채로 남으므로 로그만 남기고 이어 간다
            try
            {
                OnSceneLoaded?.Invoke(sceneName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            // 새 씬 UI가 Start에서 자리를 잡은 뒤의 위치를 기억하도록 한 프레임 기다린다 (그동안은 fadeScreen 이 덮고 있음)
            await UniTask.NextFrame();
            await PlayFadeIn();
            OnFadeInCompleted?.Invoke(sceneName);

            RestoreUI();
            canvas.enabled = false;
            isLoading = false;
        }

        [Title("Function Test"), Button]
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
            ShowCanvas();
            SetFadeAlpha(0f);
            ScreenGlitch.Clear();

            await PlayFadeOut();
            await UniTask.Delay(TimeSpan.FromSeconds(blackHold), DelayType.Realtime);
            await PlayFadeIn();

            RestoreUI();
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
            fadeInSound.Play();
            
            float dotEnd = dotAppearDuration;
            float expandEnd = dotEnd + expandDuration;
            float unfoldEnd = expandEnd + unfoldDuration;

            // 검은 화면을 걷는 같은 프레임에 꺼진 상태로 맞춰 새 씬이 한 번에 보이지 않게 한다. 새 씬의 UI도 점으로 눌러 둔다
            ScreenGlitch.PowerOff = 3f;
            CaptureUI();
            ApplyPowerOffToUI(ScreenGlitch.PowerOff);
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

        private void ShowCanvas()
        {
            canvas.sortingOrder = sortingOrder;
            canvas.enabled = true;
            CaptureUI();
            ApplyPowerOffToUI(0f);
        }

        // 지금 켜진 최상위 오버레이 캔버스의 자식들을 기억한다. 카메라 공간 캔버스(사망 화면 등)는 셰이더가 같이 접으므로 뺀다
        private void CaptureUI()
        {
            RestoreUI();
            foreach (Canvas other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (other == canvas || other.isRootCanvas == false || other.enabled == false) continue;
                if (other.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                foreach (Transform child in other.transform)
                    if (child is RectTransform rect)
                        squashedUI.Add((rect, rect.localPosition, rect.localScale));
            }
        }

        private void RestoreUI()
        {
            foreach ((RectTransform rect, Vector3 position, Vector3 scale) in squashedUI)
            {
                if (rect == null) continue;
                rect.localPosition = position;
                rect.localScale = scale;
            }
            squashedUI.Clear();
        }

        // 셰이더(ScreenGlitch.shader)처럼 화면 전체를 보이는 범위 안으로 눌러 담고, 그 밖은 검게 덮는다.
        // 0~1 위아래로 접혀 가로 선, 1~2 선이 점으로 줄어듦, 2~3 점이 사라짐
        private void ApplyPowerOffToUI(float powerOff)
        {
            EnsureMaskBars();

            // 캔버스를 막 켠 프레임에는 canvas.pixelRect가 0이라 화면 크기를 직접 읽는다
            float pixelX = 1f / Mathf.Max(1, Screen.width);
            float pixelY = 1f / Mathf.Max(1, Screen.height);
            float fold = Mathf.Clamp01(powerOff);
            float shrink = Mathf.Clamp01(powerOff - 1f);
            float extentX = Mathf.Lerp(0.5f, 1.5f * pixelX, shrink);
            float extentY = Mathf.Lerp(0.5f, 0.75f * pixelY, fold);

            // 최상위 캔버스의 원점은 화면 가운데라 위치와 크기에 같은 비율을 곱하면 가운데로 눌린다
            Vector3 squash = new Vector3(extentX * 2f, extentY * 2f, 1f);
            foreach ((RectTransform rect, Vector3 position, Vector3 scale) in squashedUI)
            {
                if (rect == null) continue;
                rect.localPosition = Vector3.Scale(position, squash);
                rect.localScale = Vector3.Scale(scale, squash);
            }

            // 선·점이 띠 경계의 픽셀 반올림에 잘리지 않게 1픽셀 여유를 둔다
            float halfWidth = Mathf.Min(0.5f, extentX + pixelX);
            float halfHeight = Mathf.Min(0.5f, extentY + pixelY);
            float bottom = 0.5f - halfHeight;
            float top = 0.5f + halfHeight;

            SetAnchors(maskBars[0], new Vector2(0f, top), Vector2.one);
            SetAnchors(maskBars[1], Vector2.zero, new Vector2(1f, bottom));
            SetAnchors(maskBars[2], new Vector2(0f, bottom), new Vector2(0.5f - halfWidth, top));
            SetAnchors(maskBars[3], new Vector2(0.5f + halfWidth, bottom), new Vector2(1f, top));
        }

        private void EnsureMaskBars()
        {
            if (maskBars != null) return;

            maskBars = new RectTransform[4];
            for (int i = 0; i < maskBars.Length; i++)
            {
                GameObject bar = new GameObject($"UI Mask {i}", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(canvas.transform, false);
                // 다 꺼진 뒤 덮는 fadeScreen 이 맨 위에 오도록 그 아래에 둔다
                if (fadeScreen.transform.parent == canvas.transform)
                    bar.transform.SetSiblingIndex(fadeScreen.transform.GetSiblingIndex());

                Image image = bar.GetComponent<Image>();
                image.color = Color.black;
                image.raycastTarget = false;
                maskBars[i] = (RectTransform)bar.transform;
            }
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
