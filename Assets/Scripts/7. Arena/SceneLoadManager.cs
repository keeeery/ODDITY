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

        [Title("Loading Grace (초, 실제 시간)")]
        // 새 씬을 불러온 뒤 다 꺼진 화면을 적어도 이만큼은 유지한다
        [SerializeField, Min(0f)] private float minBlackHold = 0.2f;
        // 첫 몇 프레임은 셰이더 준비·Start 처리로 길게 걸려, 그대로 켜면 켜짐 연출과 BGM 페이드가 한 번에 건너뛰어진다.
        // 이 길이보다 짧은 프레임이 stableFrameCount 번 연달아 나오고 BGM 데이터가 준비되면 켠다
        [SerializeField, Min(0.001f)] private float stableFrameTime = 0.05f;
        [SerializeField, Min(1)] private int stableFrameCount = 5;
        // 느린 PC에서 안정되지 않아도 이 시간이 지나면 켠다 (화면이 꺼진 채 멈추지 않게)
        [SerializeField, Min(0f)] private float maxSettleWait = 2.5f;

        [Title("Loading Icon (다 꺼진 동안 우하단 점 세 개, 일시정지 아이콘과 같은 자리)")]
        // 캔버스 기준 해상도(1920x1080) 우하단에서의 위치, 아이콘 오른쪽 아래 모서리 기준
        [SerializeField] private Vector2 loadingIconPosition = new(-64f, 56f);
        [SerializeField, Min(1f)] private float loadingDotSize = 10f;
        [SerializeField, Min(0f)] private float loadingDotSpacing = 10f;
        // 나타날 때 오른쪽에서 밀려 들어오는 거리와 나타나고 사라지는 시간
        [SerializeField] private float loadingIconSlide = 32f;
        [SerializeField, Min(0.01f)] private float loadingIconFade = 0.2f;
        // 점이 하나씩 차례로 밝아지며 살짝 떠오르는 물결
        [SerializeField, Min(0.01f)] private float loadingDotPulse = 0.3f;
        [SerializeField, Min(0f)] private float loadingDotStagger = 0.15f;
        [SerializeField, Min(0f)] private float loadingDotRest = 0.2f;
        [SerializeField] private float loadingDotRise = 4f;
        [SerializeField, Range(0f, 1f)] private float loadingDotDimAlpha = 0.25f;

        [Title("UI Fade In (실제 초, 보스 조우 연출과 같은 방식)")]
        // 새 씬 UI는 숨겨 두었다가 화면이 다 켜진 뒤 이만큼 기다렸다 서서히 나타나게 한다
        [SerializeField, Min(0f)] private float uiFadeDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float uiFadeDuration = 0.6f;

        [Title("Sound (실제 초)")]
        // 소리 전체는 화면이 꺼지는 시간 동안 줄이고, 새 씬을 불러온 뒤 이 시간 동안 원래 마스터 볼륨으로 되돌린다
        [SerializeField, Min(0f)] private float soundFadeInDuration = 0.5f;

        private Sequence sequence;
        private bool isLoading;
        // 위·아래·왼쪽·오른쪽 띠, 처음 전환할 때 만든다
        private RectTransform[] maskBars;
        // 전환하는 동안 누르는 UI와 원래 위치·크기
        private readonly List<(RectTransform rect, Vector3 position, Vector3 scale)> squashedUI = new();
        // 로딩 아이콘, 처음 쓸 때 만든다
        private CanvasGroup loadingIcon;
        private RectTransform[] loadingDots;
        private Sequence loadingIconFadeMotion;
        private Sequence loadingDotsMotion;
        // 화면이 켜질 때까지 숨겨 둔 새 씬 UI와 원래 알파
        private readonly List<(CanvasGroup group, float alpha)> hiddenUI = new();
        private Sequence uiMotion;
        private float uiReveal = 1f;
        private Tween masterVolumeFade;

        public event Action<string> OnFadeOutStarted;
        public event Action<string> OnFadeOutCompleted;
        public event Action<string> OnSceneLoaded;
        public event Action<string> OnFadeInCompleted;

        // 씬 이동 연출 중인지 (꺼지기 시작 ~ 다 켜질 때까지)
        public bool IsLoading => isLoading;

        // 새 씬 UI가 드러난 정도(0~1). 새 씬을 불러오면 0이 되고, 화면이 다 켜진 뒤 다른 UI와 같은 박자로 1까지 오른다.
        // 알파를 스스로 매 프레임 정하는 UI(ChipPanelHotkey 등)는 자기 알파에 이 값을 곱해 다른 UI와 함께 나타난다
        public static float UIRevealAlpha => IsInitialized ? Instance.uiReveal : 1f;

        private float FadeOutDuration => glitchDuration + foldDuration + shrinkDuration + dotFadeDuration;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            sequence?.Kill();
            loadingIconFadeMotion?.Kill();
            loadingDotsMotion?.Kill();
            uiMotion?.Kill();
            masterVolumeFade?.Kill();
            // 전환 도중 사라지면 소리가 꺼진 채 남지 않게 설정 볼륨으로 되돌린다
            if (isLoading) AudioListener.volume = GameSettings.MasterVolume;
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

            // 화면이 꺼지는 동안 들리는 소리 전체(마스터 볼륨)를 줄인다. BGM만 줄이면 씬에 놓인 환경음 루프 등은
            // 원래 볼륨으로 나다가 씬이 내려가는 순간 뚝 끊긴다. 다 꺼진 뒤 이전 BGM은 멈추고, 새 BGM은 OnSceneLoaded에서 BGMPlayer가 키우며 튼다
            FadeMasterVolume(0f, FadeOutDuration);

            // 이전 씬 UI는 씬이 내려갈 때까지 점으로 눌린 채 둔다. 씬을 넘어 남는 UI는 PlayFadeIn 이 같은 프레임에 되돌렸다 다시 누른다
            await PlayFadeOut();
            SoundManager.Instance.StopBGM(0f);
            ShowLoadingIcon();
            OnFadeOutCompleted?.Invoke(sceneName);

            await SceneManager.LoadSceneAsync(sceneName);
            // 새 씬의 환경음 등은 검은 화면 뒤에서 짧게 키워 둔다 (새 BGM은 제 페이드로 0에서부터 커짐)
            FadeMasterVolume(GameSettings.MasterVolume, soundFadeInDuration);
            HideSceneUI();
            // 구독한 쪽(BGM 등)에서 예외가 나도 화면을 켜지 않으면 꺼진 채로 남으므로 로그만 남기고 이어 간다
            try
            {
                OnSceneLoaded?.Invoke(sceneName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            // 새 씬이 자리를 잡을 때까지 꺼진 채 기다린다 (그동안은 fadeScreen 이 덮고 있음).
            // 적어도 몇 프레임은 지나므로 새 씬 UI도 Start에서 자리를 잡은 뒤의 위치로 기억된다
            await WaitUntilSettled(sceneName);
            HideLoadingIcon();
            await PlayFadeIn();
            FadeInSceneUI();
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
            ShowLoadingIcon();
            await UniTask.Delay(TimeSpan.FromSeconds(blackHold), DelayType.Realtime);
            HideLoadingIcon();
            await PlayFadeIn();

            RestoreUI();
            canvas.enabled = false;
            isLoading = false;
        }

        // 프레임 시간이 고르게 짧아지고 새 BGM 데이터가 메모리에 올라올 때까지 기다린다. 최소·최대 시간은 실제 시간 기준
        private async UniTask WaitUntilSettled(string sceneName)
        {
            float startTime = Time.realtimeSinceStartup;
            int stableFrames = 0;

            while (true)
            {
                await UniTask.NextFrame();

                float elapsed = Time.realtimeSinceStartup - startTime;
                stableFrames = Time.unscaledDeltaTime <= stableFrameTime ? stableFrames + 1 : 0;
                bool bgmReady = SoundManager.IsInitialized == false || SoundManager.Instance.IsBGMLoading == false;

                if (elapsed >= minBlackHold && stableFrames >= stableFrameCount && bgmReady) return;
                if (elapsed >= maxSettleWait)
                {
                    Debug.LogWarning($"[SceneLoadManager] {sceneName} 로딩 후 {maxSettleWait}초 안에 안정되지 않아 그대로 화면을 켭니다. (안정 프레임 {stableFrames}/{stableFrameCount}, BGM 준비 {bgmReady})", this);
                    return;
                }
            }
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
            // 씬에 꺼진 채 놓여 있으면 다 꺼진 동안 새 씬 UI가 검은 화면 위로 비치므로 여기서 켠다.
            // (이전 씬 연출이 ScreenGlitch.Clear 로 PowerOff 를 0으로 되돌리면 검은 띠도 열려 버린다)
            fadeScreen.gameObject.SetActive(true);
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

        // 리스너 볼륨(GameSettings의 마스터 볼륨이 들어가는 곳)을 실제 시간으로 바꾼다
        private void FadeMasterVolume(float target, float duration)
        {
            masterVolumeFade?.Kill();
            masterVolumeFade = DOTween.To(() => AudioListener.volume, value => AudioListener.volume = value, target, duration)
                .SetUpdate(true);
        }

        // 새 씬의 화면 UI(최상위 오버레이·카메라 공간 캔버스)를 모두 숨긴다. 화면이 다 켜진 뒤 FadeInSceneUI 가 되살린다.
        // 월드 공간 캔버스는 월드와 함께 켜지므로 두고, 원래 알파가 0인 캔버스(닫힌 창 등)는 제 스크립트가 열도록 둔다.
        // 보스 씬의 HUD(UI Canvas)는 BossIntroCinematic 이 숨겼다가 카메라가 돌아올 때 직접 되살리므로, 알파가 엇갈리지 않게 건드리지 않는다
        private void HideSceneUI()
        {
            uiMotion?.Kill();
            hiddenUI.Clear();
            uiReveal = 0f;

            Boss.BossIntroCinematic intro = FindAnyObjectByType<Boss.BossIntroCinematic>();
            bool hasIntro = intro != null && intro.isActiveAndEnabled;

            Scene scene = SceneManager.GetActiveScene();
            foreach (Canvas other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (other == canvas || other.isRootCanvas == false) continue;
                if (other.renderMode == RenderMode.WorldSpace) continue;
                // 씬을 넘어 남는 UI(일시정지 메뉴 등)는 이번 씬 것이 아니므로 뺀다
                if (other.gameObject.scene != scene) continue;
                if (hasIntro && IsIntroHud(other)) continue;

                if (other.TryGetComponent(out CanvasGroup group) == false) group = other.gameObject.AddComponent<CanvasGroup>();
                if (group.alpha <= 0f) continue;

                hiddenUI.Add((group, group.alpha));
                group.alpha = 0f;
            }
        }

        // BossIntroCinematic 이 숨기는 캔버스: 스킬 슬롯 HUD나 보스 이름 UI(BossIntroTitle)가 든 최상위 캔버스
        private static bool IsIntroHud(Canvas root)
        {
            return root.GetComponentInChildren<SkillSlotHUD>(true) != null
                   || root.GetComponentInChildren<BossIntroTitle>(true) != null;
        }

        // 숨기기 전 알파로 서서히 되돌린다. 그사이 다른 스크립트가 알파를 바꿨으면(창을 열었다 등) 그쪽에 맡긴다
        private void FadeInSceneUI()
        {
            uiMotion?.Kill();
            uiMotion = DOTween.Sequence().SetUpdate(true).SetDelay(uiFadeDelay)
                // 끊겨도 UI가 숨은 채 남지 않게 끝까지 드러낸다
                .OnKill(() => uiReveal = 1f);
            uiMotion.Join(DOTween.To(() => uiReveal, value => uiReveal = value, 1f, uiFadeDuration).SetEase(Ease.OutSine));
            foreach ((CanvasGroup group, float alpha) in hiddenUI)
                if (group != null && group.alpha <= 0f)
                    uiMotion.Join(group.DOFade(alpha, uiFadeDuration).SetEase(Ease.OutSine));
            hiddenUI.Clear();
        }

        // 다 꺼진 화면 우하단에 점 세 개를 밀어 넣고, 점이 차례로 밝아지며 떠오르는 물결을 되풀이한다
        private void ShowLoadingIcon()
        {
            EnsureLoadingIcon();
            loadingIconFadeMotion?.Kill();
            loadingDotsMotion?.Kill();

            RectTransform iconRect = (RectTransform)loadingIcon.transform;
            // fadeScreen 보다 위에 그린다
            iconRect.SetAsLastSibling();
            loadingIcon.gameObject.SetActive(true);
            loadingIcon.alpha = 0f;
            iconRect.anchoredPosition = loadingIconPosition + Vector2.right * loadingIconSlide;

            loadingIconFadeMotion = DOTween.Sequence().SetUpdate(true)
                .Join(loadingIcon.DOFade(1f, loadingIconFade).SetEase(Ease.OutQuad))
                .Join(iconRect.DOAnchorPos(loadingIconPosition, loadingIconFade * 1.5f).SetEase(Ease.OutCubic));

            // 모든 점이 한 바퀴를 같은 길이로 돌도록 한 시퀀스에 넣는다
            float cycle = (loadingDots.Length - 1) * loadingDotStagger + loadingDotPulse * 2f + loadingDotRest;
            loadingDotsMotion = DOTween.Sequence().SetUpdate(true).SetLoops(-1);
            for (int i = 0; i < loadingDots.Length; i++)
            {
                RectTransform dot = loadingDots[i];
                Image image = dot.GetComponent<Image>();
                SetDotAlpha(image, loadingDotDimAlpha);
                dot.anchoredPosition = new Vector2(dot.anchoredPosition.x, 0f);

                float start = i * loadingDotStagger;
                loadingDotsMotion.Insert(start, image.DOFade(1f, loadingDotPulse).SetEase(Ease.OutQuad));
                loadingDotsMotion.Insert(start, dot.DOAnchorPosY(loadingDotRise, loadingDotPulse).SetEase(Ease.OutQuad));
                loadingDotsMotion.Insert(start + loadingDotPulse, image.DOFade(loadingDotDimAlpha, loadingDotPulse).SetEase(Ease.InQuad));
                loadingDotsMotion.Insert(start + loadingDotPulse, dot.DOAnchorPosY(0f, loadingDotPulse).SetEase(Ease.InQuad));
            }
            loadingDotsMotion.InsertCallback(cycle, () => { });
        }

        // 화면이 켜지기 시작할 때 옅게 사라진다. 그동안 주변은 아직 검은 띠가 덮고 있다
        private void HideLoadingIcon()
        {
            if (loadingIcon == null || loadingIcon.gameObject.activeSelf == false) return;

            loadingIconFadeMotion?.Kill();
            loadingIconFadeMotion = DOTween.Sequence().SetUpdate(true)
                .Join(loadingIcon.DOFade(0f, loadingIconFade).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    loadingDotsMotion?.Kill();
                    loadingIcon.gameObject.SetActive(false);
                });
        }

        private void EnsureLoadingIcon()
        {
            if (loadingIcon != null) return;

            GameObject icon = new GameObject("Loading Icon", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(canvas.transform, false);
            // 일시정지 아이콘처럼 오른쪽 아래 모서리를 기준으로 둔다
            iconRect.anchorMin = new Vector2(1f, 0f);
            iconRect.anchorMax = new Vector2(1f, 0f);
            iconRect.pivot = new Vector2(1f, 0f);
            iconRect.sizeDelta = new Vector2(loadingDotSize * 3f + loadingDotSpacing * 2f, 38f);

            loadingIcon = icon.GetComponent<CanvasGroup>();
            loadingIcon.blocksRaycasts = false;
            loadingIcon.interactable = false;

            loadingDots = new RectTransform[3];
            for (int i = 0; i < loadingDots.Length; i++)
            {
                GameObject dot = new GameObject($"Dot {i}", typeof(RectTransform), typeof(Image));
                RectTransform dotRect = (RectTransform)dot.transform;
                dotRect.SetParent(iconRect, false);
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = Vector2.one * loadingDotSize;
                dotRect.anchoredPosition = new Vector2((i - 1) * (loadingDotSize + loadingDotSpacing), 0f);

                Image image = dot.GetComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;
                loadingDots[i] = dotRect;
            }

            icon.SetActive(false);
        }

        private static void SetDotAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
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
