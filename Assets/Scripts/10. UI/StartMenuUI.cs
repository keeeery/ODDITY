using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamOdd
{
    // 시작 씬의 새 게임 / 이어하기 / 환경설정 / 게임 종료
    // - 새 게임: 저장 데이터가 있으면 지우고 시작할지 묻는 팝업을 띄우고, 없으면 바로 시작
    // - 이어하기: 저장 데이터(보유 칩·수락 임무)를 불러와 시작, 저장 데이터가 없으면 회색으로 막아 둠
    // - 팝업이 떠 있는 동안 뒤에 Dim을 깔아 메뉴를 가리고, Esc로 닫음
    public class StartMenuUI : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "02. Lobby";

        [Header("Popup")]
        [SerializeField] private GameObject dim;
        [SerializeField] private GameObject newGamePopup;
        [SerializeField] private Button newGameConfirmButton;
        [SerializeField] private Button newGameCancelButton;
        [SerializeField] private TitleSettingsPopup settingsPopup;
        [SerializeField] private TitleMenuButton continueButton;

        private bool isLoading;

        private bool IsPopupOpen =>
            (newGamePopup != null && newGamePopup.activeSelf) || (settingsPopup != null && settingsPopup.gameObject.activeSelf);

        private void Awake()
        {
            if (newGameConfirmButton != null) newGameConfirmButton.onClick.AddListener(StartNewGame);
            if (newGameCancelButton != null) newGameCancelButton.onClick.AddListener(ClosePopup);
            ClosePopup();
        }

        private void Start()
        {
            // 게임 씬에서 돌아왔을 때를 대비해 커서를 풀어 둔다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (continueButton != null) continueButton.Interactable = GameSave.HasSave;
        }

        private void Update()
        {
            if (IsPopupOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePopup();
        }

        // 저장 기능이 없던 기존 시작 씬(01. Title) 버튼용, 지금 들고 있는 칩·임무 그대로 로비로 간다
        public void StartGame() => LoadFirstScene();

        public void NewGame()
        {
            if (isLoading)
                return;

            if (GameSave.HasSave && newGamePopup != null)
                OpenPopup(newGamePopup);
            else
                StartNewGame();
        }

        public void ContinueGame()
        {
            if (isLoading || GameSave.Load() == false)
                return;

            LoadFirstScene();
        }

        public void OpenSettings()
        {
            if (settingsPopup != null)
                OpenPopup(settingsPopup.gameObject);
        }

        public void ClosePopup()
        {
            if (newGamePopup != null) newGamePopup.SetActive(false);
            if (settingsPopup != null) settingsPopup.gameObject.SetActive(false);
            if (dim != null) dim.SetActive(false);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 이전 데이터를 지우고 빈 저장 파일을 만들어 둔다 (다음에 켜면 바로 이어할 수 있게)
        private void StartNewGame()
        {
            if (isLoading)
                return;

            GameSave.Delete();
            GameSave.Save();
            LoadFirstScene();
        }

        private void OpenPopup(GameObject popup)
        {
            ClosePopup();
            if (dim != null) dim.SetActive(true);
            popup.SetActive(true);
        }

        private void LoadFirstScene()
        {
            if (isLoading)
                return;

            isLoading = true;
            ClosePopup();
            SceneLoadManager.Instance.LoadScene(firstSceneName);
        }
    }
}
