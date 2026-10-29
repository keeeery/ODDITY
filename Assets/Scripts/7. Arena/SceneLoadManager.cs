using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jay;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamOdd
{
    public class SceneLoadManager : SingletonBase<SceneLoadManager>
    {
        [Title("Components")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image fadeScreen;

        [Title("Settings")]
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float fadeInDuration = 0.5f;

        public event Action<string> OnFadeOutStarted;
        public event Action<string> OnFadeOutCompleted;
        public event Action<string> OnSceneLoaded;
        public event Action<string> OnFadeInCompleted;
        
        public async UniTask LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoadManager] Scene name is null or whitespace.");
                return;
            }

            canvas.enabled = true;
            
            OnFadeOutStarted?.Invoke(sceneName);
            
            await fadeScreen.DOFade(1f, fadeOutDuration).ToUniTask();
            OnFadeOutCompleted?.Invoke(sceneName);
            
            await SceneManager.LoadSceneAsync(sceneName);
            OnSceneLoaded?.Invoke(sceneName);

            await fadeScreen.DOFade(0f, fadeInDuration).ToUniTask();
            OnFadeInCompleted?.Invoke(sceneName);

            canvas.enabled = false;
        }

        [Button]
        public void LoadScene(string sceneName)
        {
            _ = LoadSceneAsync(sceneName);
        }
    }
}
