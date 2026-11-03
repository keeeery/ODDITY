using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace TeamOdd
{
    // 컴포넌트 없이 쓰는 쪽 클래스에 필드로 선언해 두고, 인스펙터에서 클립과 볼륨을 골라 재생함
    [Serializable]
    public class SoundData
    {
        [ValueDropdown(nameof(GetClipNames))]
        [SerializeField] private string clipName;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        public string ClipName => clipName;
        public float Volume => volume;

        private SoundManager Sound => SoundManager.Instance;

        /// <summary>
        /// 클립을 한 번 재생
        /// </summary>
        public void Play()
        {
            // 게임 종료 중에는 싱글톤이 null을 돌려줌
            if (Sound == null) return;

            Sound.Play(clipName, volume);
        }

        /// <summary>
        /// 클립을 반복 재생하고 멈출 때 넘길 AudioSource를 돌려줌, 다 쓰면 StopLoop로 멈춰야 함
        /// </summary>
        public AudioSource PlayLoop()
        {
            if (Sound == null) return null;

            return Sound.PlayLoop(clipName, volume);
        }

        /// <summary>
        /// PlayLoop로 튼 소리를 fadeTime초 동안 줄이며 멈춤, 틀지 못해 null이면 아무 일도 없음
        /// </summary>
        public static void StopLoop(AudioSource loop, float fadeTime = 0f)
        {
            if (loop == null || SoundManager.Instance == null) return;

            SoundManager.Instance.StopLoop(loop, fadeTime);
        }

        /// <summary>
        /// 클립을 재생하고 끝날 때까지 기다림, 취소해도 이미 재생된 소리는 멈추지 않음
        /// </summary>
        public async UniTask PlayAsync(CancellationToken token = default)
        {
            if (Sound == null) return;
            if (Sound.TryPlay(clipName, volume, out AudioClip clip) == false) return;

            // PlayOneShot은 timeScale의 영향을 받지 않으므로 히트스톱·슬로모션 중에도 실제 시간으로 기다림
            await UniTask.Delay(
                TimeSpan.FromSeconds(clip.length),
                DelayType.Realtime,
                cancellationToken: token
            );
        }

        #region ODIN_INSPECTOR
#if UNITY_EDITOR
        private static readonly string AUDIO_FOLDER = $"Assets/Resources/{SoundManager.AUDIO_ROOT}";

        // 하위 폴더에 있는 클립도 Resources.Load로 찾을 수 있도록 파일명이 아닌 AUDIO_FOLDER 기준 경로(확장자 제외)를 씀
        // 경로의 '/'로 드롭다운이 폴더별로 묶임
        private static IEnumerable<string> GetClipNames()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AUDIO_FOLDER });
            List<string> result = new();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string relativePath = path.Substring(AUDIO_FOLDER.Length + 1);
                string name = Path.ChangeExtension(relativePath, null);

                result.Add(name);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }
#endif
        #endregion
    }
}
