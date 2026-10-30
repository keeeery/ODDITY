using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd
{
    // 시작 씬의 게임 시작 / 종료 버튼
    public class StartMenuUI : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "1. Get mission 1";

        private bool isLoading;

        private void Start()
        {
            // 게임 씬에서 돌아왔을 때를 대비해 커서를 풀어 둔다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void StartGame()
        {
            if (isLoading)
                return;

            isLoading = true;
            SceneManager.LoadScene(firstSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
