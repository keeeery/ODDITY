using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd
{
    // 씬마다 하나 두고, 씬 이동으로 이 씬이 로드되면 BGM을 바꿈
    public class BGMPlayer : MonoBehaviour
    {
        [Title("Settings")]
        [SerializeField, InlineProperty, HideLabel] private SoundData bgm;
        [SerializeField, Min(0f)] private float fadeTime = 1f;

        private SceneLoadManager SceneLoad => SceneLoadManager.Instance;
        private SoundManager Sound => SoundManager.Instance;
        
        public SoundData BGM => bgm;

        private void Awake() => SceneLoad.OnSceneLoaded += HandleSceneLoaded;

        private void OnEnable()
        {
            Sound.PlayBGM(bgm.ClipName, bgm.Volume, fadeTime);
        }

        private void OnDisable()
        {
            Sound.StopBGM();
        }

        // SceneLoadManager는 씬이 바뀌어도 남으므로, 파괴된 뒤 다음 씬 로드 때 불리지 않도록 해제
        private void OnDestroy()
        {
            // 게임 종료 중에는 싱글톤이 null을 돌려줌
            if (SceneLoad == null) return;

            SceneLoad.OnSceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(string _) => Sound.PlayBGM(bgm.ClipName, bgm.Volume, fadeTime);
    }
}
