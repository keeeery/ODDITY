using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using TeamOdd.Combat;
using TeamOdd.Mission;
using TeamOdd.Superpower;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 인게임(로비·보스 씬)의 Esc 일시 정지 메뉴. 메인 화면 메뉴와 같은 글자 버튼(TitleMenuButton)·팝업 모양을 쓴다.
    // - Esc로 닫히는 창(증강 칩·임무·출격·X 버튼이 있는 창)이 열려 있으면 Esc는 그 창부터 닫는다
    // - 그런 창이 없고 다른 창(보상·사망 화면 등)도 열려 있지 않을 때 Esc로 열고, 다시 Esc 또는 계속하기로 닫는다
    // - 열려 있는 동안 시간과 효과음을 멈추고(배경음은 계속 흐름) 커서를 풀며, 게임 입력은 다른 창처럼 UIManager.IsUIClose로 막는다
    // - 항목: 계속하기 / 정비소로 이동(보스 씬만) / 환경설정 / 메인 메뉴 / 게임 종료, 정비소로 이동·메인 메뉴·게임 종료는 예/아니오로 한 번 더 묻는다
    // - 팝업이 떠 있을 때 Esc는 팝업만 닫는다
    // - 화면 우하단에 일시 정지 아이콘이 천천히 깜빡이고, 열 때는 Dim이 차오르며 항목이 위에서부터 차례로 나타나고 닫을 때는 함께 사그라든다 (실제 시간 기준)
    // 씬에 따로 두지 않아도 대상 씬이 열릴 때 Resources의 프리팹(Tools/Team Odd/Build Pause Menu Prefab으로 만듦)을 띄운다.
    [DefaultExecutionOrder(-200)] // Esc를 UIManager(-100)·다른 창보다 먼저 보고, 그 프레임에 다른 창이 열려 있었는지 판단
    public class PauseMenuUI : MonoBehaviour
    {
        private const string PrefabPath = "UI/Pause Menu";
        private static readonly string[] TargetScenes = { "02. Lobby", "03. Easy Boss", "04. Normal Boss", "05. Hard Boss" };

        [Header("Scenes")]
        [SerializeField] private string lobbySceneName = "02. Lobby";
        [SerializeField] private string mainMenuSceneName = "00. Title Cinematic";
        [SerializeField] private string fallbackMainMenuSceneName = "01. Title"; // 위 씬이 빌드 목록에 없을 때

        [Header("Menu")]
        [SerializeField] private GameObject content; // Dim과 메뉴 전체, 일시 정지 중에만 켬
        [SerializeField] private GameObject dim; // 메뉴 뒤 Dim, 팝업이 뜨면 메뉴 위의 팝업 Dim으로 바꿔 두 겹이 되지 않게 함
        [SerializeField] private GameObject lobbyButton; // 정비소로 이동 항목, 로비에서는 숨김
        [SerializeField] private RectTransform menuGroup;
        [SerializeField] private RectTransform pauseIcon;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float openDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float closeDuration = 0.08f;
        [SerializeField, Min(0f)] private float rowInterval = 0.02f; // 항목이 하나씩 나타나는 간격
        [SerializeField, Range(0.8f, 1f)] private float menuStartScale = 0.96f;
        [SerializeField] private float iconSlide = 32f; // 아이콘이 오른쪽에서 밀려 들어오는 거리
        [SerializeField, Min(0.01f)] private float iconPulseDuration = 0.9f;
        [SerializeField, Range(0f, 1f)] private float iconPulseAlpha = 0.35f;

        [Header("Popup")]
        [SerializeField] private GameObject popupDim;
        [SerializeField] private GameObject lobbyPopup;
        [SerializeField] private Button lobbyConfirmButton;
        [SerializeField] private Button lobbyCancelButton;
        [SerializeField] private GameObject mainMenuPopup;
        [SerializeField] private Button mainMenuConfirmButton;
        [SerializeField] private Button mainMenuCancelButton;
        [SerializeField] private GameObject quitPopup;
        [SerializeField] private Button quitConfirmButton;
        [SerializeField] private Button quitCancelButton;
        [SerializeField] private TitleSettingsPopup settingsPopup;

        private readonly List<MissionNPC> pausedNPCs = new();
        private readonly List<CanvasGroup> rows = new();
        private CanvasGroup contentGroup;
        private CanvasGroup iconGroup;
        private Vector2 iconBase;
        private Sequence motion;
        private Tween iconPulse;
        private AudioSource bgmSource;
        private bool bgmIgnoredPause;

        // SoundManager의 배경음 소스는 밖에서 꺼낼 수 없어 이름으로 찾는다 (이름이 바뀌면 배경음도 같이 멈춤)
        private static readonly FieldInfo BgmSourceField =
            typeof(SoundManager).GetField("bgmSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private int relockFrame = -1; // 이 프레임 끝에 커서를 다시 잠금 (재개·창 닫기)
        private bool isLeaving;
        private bool isTransitioning;

        public static bool IsPaused { get; private set; }

        private bool IsPopupOpen =>
            (lobbyPopup != null && lobbyPopup.activeSelf) ||
            (mainMenuPopup != null && mainMenuPopup.activeSelf) ||
            (quitPopup != null && quitPopup.activeSelf) ||
            (settingsPopup != null && settingsPopup.gameObject.activeSelf);

        private bool IsLobby => SceneManager.GetActiveScene().name == lobbySceneName;

        #region SPAWN

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsPaused = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || System.Array.IndexOf(TargetScenes, scene.name) < 0) return;
            if (FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include) != null) return;

            var prefab = Resources.Load<PauseMenuUI>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PauseMenu] Resources/{PrefabPath} 프리팹이 없습니다. Tools/Team Odd/Build Pause Menu Prefab으로 만들어 주세요.");
                return;
            }

            Instantiate(prefab).name = prefab.name;
        }

        #endregion

        private void Awake()
        {
            if (lobbyConfirmButton != null) lobbyConfirmButton.onClick.AddListener(() => LoadScene(lobbySceneName));
            if (lobbyCancelButton != null) lobbyCancelButton.onClick.AddListener(ClosePopup);
            if (mainMenuConfirmButton != null) mainMenuConfirmButton.onClick.AddListener(GoToMainMenu);
            if (mainMenuCancelButton != null) mainMenuCancelButton.onClick.AddListener(ClosePopup);
            if (quitConfirmButton != null) quitConfirmButton.onClick.AddListener(QuitGame);
            if (quitCancelButton != null) quitCancelButton.onClick.AddListener(ClosePopup);

            if (content != null && content.TryGetComponent(out contentGroup) == false) contentGroup = content.AddComponent<CanvasGroup>();
            if (pauseIcon != null)
            {
                iconBase = pauseIcon.anchoredPosition;
                if (pauseIcon.TryGetComponent(out iconGroup) == false) iconGroup = pauseIcon.gameObject.AddComponent<CanvasGroup>();
            }

            ClosePopup();
            if (content != null) content.SetActive(false);
        }

        private void OnDestroy() => KillMotion();

        private void OnEnable()
        {
            if (SceneLoadManager.IsInitialized)
            {
                SceneLoadManager.Instance.OnFadeOutStarted += OnTransitionStarted;
                SceneLoadManager.Instance.OnFadeInCompleted += OnTransitionEnded;
            }
        }

        private void OnDisable()
        {
            if (SceneLoadManager.IsInitialized)
            {
                SceneLoadManager.Instance.OnFadeOutStarted -= OnTransitionStarted;
                SceneLoadManager.Instance.OnFadeInCompleted -= OnTransitionEnded;
            }

            // 멈춘 채 씬이 바뀌거나 꺼져도 다음 씬이 멈춰 있지 않게
            if (IsPaused) RestoreTime();
        }

        private void OnTransitionStarted(string sceneName) => isTransitioning = true;
        private void OnTransitionEnded(string sceneName) => isTransitioning = false;

        private void Update()
        {
            if (isLeaving || Keyboard.current == null || Keyboard.current.escapeKey.wasPressedThisFrame == false) return;
            // 검색 칸 같은 글자 입력 칸에 치는 중이면 Esc는 입력만 끝낸다. 창을 닫거나 멈추지 않는다
            if (TextInputFocus.IsTyping) return;

            if (IsPaused)
            {
                if (IsPopupOpen) ClosePopup();
                else Resume();
            }
            else if (CloseOpenWindow() == false && CanPause())
            {
                Pause();
            }
        }

        // 열려 있는 창 하나를 닫으면 true. 창의 닫기 동작이 커서를 잠그면 같은 Esc로 UIManager가 다시 푸는 것을 LateUpdate에서 되돌림
        private bool CloseOpenWindow()
        {
            if (TryCloseWindow() == false) return false;

            if (UIManager.IsInitialized && UIManager.Instance.IsUIClose) relockFrame = Time.frameCount;
            return true;
        }

        private bool TryCloseWindow()
        {
            AugmentationChipPanel chipPanel = FindAnyObjectByType<AugmentationChipPanel>();
            if (chipPanel != null && chipPanel.IsOpen)
            {
                chipPanel.Close();
                UIManager.Instance.LockCursor(true);
                return true;
            }

            MissionUI missionUI = FindAnyObjectByType<MissionUI>();
            if (missionUI != null)
            {
                missionUI.CloseUI();
                return true;
            }

            StageMoveUI stageMoveUI = FindAnyObjectByType<StageMoveUI>();
            if (stageMoveUI != null)
            {
                stageMoveUI.CloseUI();
                return true;
            }

            // 그 밖에 X 버튼(UICloseButton)이 달린 창, 여럿이면 가장 위에 그려지는 캔버스의 것
            UICloseButton top = null;
            int topOrder = int.MinValue;
            foreach (UICloseButton close in FindObjectsByType<UICloseButton>(FindObjectsSortMode.None))
            {
                if (close.transform.IsChildOf(transform) || close.TryGetComponent(out Button button) == false || button.IsInteractable() == false)
                    continue;

                Canvas canvas = close.GetComponentInParent<Canvas>();
                int order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;
                if (order <= topOrder) continue;
                top = close;
                topOrder = order;
            }

            if (top == null) return false;
            top.GetComponent<Button>().onClick.Invoke();
            return true;
        }

        // 다른 스크립트가 같은 프레임의 Esc·클릭으로 바꾼 커서·시간을 메뉴 상태에 맞춰 되돌림
        private void LateUpdate()
        {
            if (IsPaused)
            {
                Time.timeScale = 0f;
                // 환경설정의 X는 팝업만 끄므로 남은 팝업 Dim을 걷고 메뉴 뒤 Dim으로 되돌림
                if (popupDim != null && popupDim.activeSelf && IsPopupOpen == false) ClosePopup();
                if (UIManager.Instance.IsUIClose || Cursor.lockState != CursorLockMode.None)
                    UIManager.Instance.LockCursor(false, true);
            }
            else if (relockFrame == Time.frameCount && isLeaving == false)
            {
                UIManager.Instance.LockCursor(true); // UIManager가 같은 Esc로 커서를 풀었을 수 있음
            }
        }

        private bool CanPause()
        {
            if (isTransitioning || UIManager.IsInitialized == false) return false;
            return UIManager.Instance.IsUIClose; // 임무·증강 칩·보상·사망 화면 등이 열려 있으면 그 창이 Esc를 씀
        }

        public void Select(TitleMenuButton.ButtonType type)
        {
            if (isLeaving) return;

            switch (type)
            {
                case TitleMenuButton.ButtonType.Resume:
                    Resume();
                    break;

                case TitleMenuButton.ButtonType.Lobby:
                    if (lobbyPopup != null) OpenPopup(lobbyPopup);
                    break;

                case TitleMenuButton.ButtonType.Settings:
                    if (settingsPopup != null) OpenPopup(settingsPopup.gameObject);
                    break;

                case TitleMenuButton.ButtonType.MainMenu:
                    if (mainMenuPopup != null) OpenPopup(mainMenuPopup);
                    break;

                case TitleMenuButton.ButtonType.Quit:
                    if (quitPopup != null) OpenPopup(quitPopup);
                    break;
            }
        }

        public void Pause()
        {
            if (IsPaused) return;

            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            KeepBgmPlaying();
            UIManager.Instance.LockCursor(false, true);

            // 정비소 NPC는 IsUIClose와 상관없이 F·Esc를 받으므로 멈춘 동안 꺼 둠
            pausedNPCs.Clear();
            foreach (MissionNPC npc in FindObjectsByType<MissionNPC>(FindObjectsSortMode.None))
            {
                if (npc.enabled == false) continue;
                npc.enabled = false;
                pausedNPCs.Add(npc);
            }

            if (lobbyButton != null) lobbyButton.SetActive(IsLobby == false);
            ClosePopup();
            PlayOpen();
        }

        public void Resume()
        {
            if (IsPaused == false) return;

            ClosePopup();
            PlayClose();
            RestoreTime();
            relockFrame = Time.frameCount;
            UIManager.Instance.LockCursor(true);
        }

        #region MOTION

        // Dim이 차오르고 메뉴가 살짝 작았다가 제자리로, 항목은 위에서부터 차례로, 아이콘은 오른쪽에서 밀려 들어온 뒤 깜빡임
        private void PlayOpen()
        {
            KillMotion();
            if (content == null) return;

            content.SetActive(true);
            contentGroup.alpha = 0f;
            contentGroup.interactable = true;
            contentGroup.blocksRaycasts = true;

            motion = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Join(contentGroup.DOFade(1f, openDuration).SetEase(Ease.OutQuad));

            if (menuGroup != null)
            {
                menuGroup.localScale = Vector3.one * menuStartScale;
                motion.Join(menuGroup.DOScale(1f, openDuration).SetEase(Ease.OutCubic));

                CollectRows();
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].alpha = 0f;
                    motion.Insert(i * rowInterval, rows[i].DOFade(1f, openDuration).SetEase(Ease.OutQuad));
                }
            }

            if (pauseIcon != null)
            {
                pauseIcon.anchoredPosition = iconBase + Vector2.right * iconSlide;
                iconGroup.alpha = 1f;
                motion.Join(pauseIcon.DOAnchorPos(iconBase, openDuration * 1.5f).SetEase(Ease.OutCubic));
                motion.OnComplete(() =>
                    iconPulse = iconGroup.DOFade(iconPulseAlpha, iconPulseDuration).SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetTarget(this));
            }
        }

        // 입력을 바로 놓고 메뉴·아이콘이 함께 사그라든 뒤 끔
        private void PlayClose()
        {
            KillMotion();
            if (content == null || content.activeSelf == false) return;

            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;

            motion = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Join(contentGroup.DOFade(0f, closeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => content.SetActive(false));
            if (menuGroup != null) motion.Join(menuGroup.DOScale(menuStartScale, closeDuration).SetEase(Ease.InQuad));
            if (pauseIcon != null) motion.Join(pauseIcon.DOAnchorPos(iconBase + Vector2.right * iconSlide, closeDuration).SetEase(Ease.InQuad));
        }

        // 켜져 있는 항목 줄 (로비에서 숨긴 정비소로 이동은 빠짐)
        private void CollectRows()
        {
            rows.Clear();
            foreach (Transform row in menuGroup)
            {
                if (row.gameObject.activeSelf == false) continue;
                if (row.TryGetComponent(out CanvasGroup group) == false) group = row.gameObject.AddComponent<CanvasGroup>();
                rows.Add(group);
            }
        }

        private void KillMotion()
        {
            motion?.Kill();
            motion = null;
            iconPulse?.Kill();
            iconPulse = null;
        }

        #endregion

        // 효과음은 AudioListener.pause로 한꺼번에 멈추고, 배경음 소스만 그 영향을 받지 않게 둠
        private void KeepBgmPlaying()
        {
            bgmSource = SoundManager.IsInitialized && BgmSourceField != null
                ? BgmSourceField.GetValue(SoundManager.Instance) as AudioSource
                : null;
            if (bgmSource == null) return;

            bgmIgnoredPause = bgmSource.ignoreListenerPause;
            bgmSource.ignoreListenerPause = true;
        }

        private void RestoreTime()
        {
            IsPaused = false;
            AudioListener.pause = false;
            if (bgmSource != null) bgmSource.ignoreListenerPause = bgmIgnoredPause;
            bgmSource = null;
            // 히트스탑 도중에 멈췄으면 히트스탑이 끝날 때 스스로 BaseTimeScale로 돌아옴
            Time.timeScale = HitStop.IsStopping ? 0f : HitStop.BaseTimeScale;

            foreach (MissionNPC npc in pausedNPCs)
                if (npc != null) npc.enabled = true;
            pausedNPCs.Clear();
        }

        private void OpenPopup(GameObject popup)
        {
            ClosePopup();
            if (dim != null) dim.SetActive(false);
            if (popupDim != null) popupDim.SetActive(true);
            popup.SetActive(true);
        }

        private void ClosePopup()
        {
            if (lobbyPopup != null) lobbyPopup.SetActive(false);
            if (mainMenuPopup != null) mainMenuPopup.SetActive(false);
            if (quitPopup != null) quitPopup.SetActive(false);
            if (settingsPopup != null) settingsPopup.gameObject.SetActive(false);
            if (popupDim != null) popupDim.SetActive(false);
            if (dim != null) dim.SetActive(true);
        }

        private void GoToMainMenu()
        {
            string sceneName = Application.CanStreamedLevelBeLoaded(mainMenuSceneName) ? mainMenuSceneName : fallbackMainMenuSceneName;
            LoadScene(sceneName);
        }

        // 시간을 되돌린 뒤 씬 전환 연출로 이동, 메뉴는 화면과 함께 접히며 사라짐
        private void LoadScene(string sceneName)
        {
            if (isLeaving) return;

            isLeaving = true;
            ClosePopup();
            if (SlowMotion.IsSlowed) SlowMotion.Resume();
            RestoreTime();
            Time.timeScale = HitStop.BaseTimeScale;
            if (contentGroup != null) contentGroup.interactable = false;

            SceneLoadManager.Instance.LoadScene(sceneName);
        }

        private void QuitGame()
        {
            GameSave.Save();
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
