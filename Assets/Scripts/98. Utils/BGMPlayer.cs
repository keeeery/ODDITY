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
            // 씬 이동으로 켜질 때는 OnSceneLoaded에서 한 번만 틂. 여기서도 틀면 이전 씬 BGMPlayer가 내려가며 부르는 StopBGM과 순서가 엉키거나,
            // 로비의 ChangeBGM이 화면이 꺼지는 도중 다음 곡을 켜서 줄어들던 BGM이 다시 커질 수 있음
            if (SceneLoadManager.IsInitialized && SceneLoad.IsLoading) return;

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
