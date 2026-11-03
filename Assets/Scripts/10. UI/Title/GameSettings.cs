using System;
using System.IO;
using UnityEngine;

namespace TeamOdd
{
    // 환경설정 값을 로컬(persistentDataPath/settings.json)에 저장하고 게임을 켤 때 불러와 적용한다.
    // - 화면 모드·창 크기: 켤 때 저장된 값으로 화면을 맞춤 (에디터의 Game 창에서는 바뀌지 않음)
    // - 볼륨(0~1): 마스터 볼륨은 AudioListener에, 배경음·효과음은 SoundManager가 VolumeChanged를 받아 각 소스에 곱해 반영
    // 값을 바꾸면 메모리에만 두고, Save를 부를 때(설정 창을 닫을 때) 파일에 쓴다.
    public static class GameSettings
    {
        private const string FileName = "settings.json";

        // 이전 버전이 PlayerPrefs에 남긴 볼륨, settings.json이 없을 때 한 번 옮겨 온다
        private const string LegacyMasterVolumeKey = "Settings.MasterVolume";
        private const string LegacyBgmVolumeKey = "Settings.BgmVolume";
        private const string LegacySfxVolumeKey = "Settings.SfxVolume";

        [Serializable]
        private class SettingsData
        {
            public bool hasDisplay; // 화면 설정을 한 번이라도 저장했는지, 아니면 지금 화면을 그대로 씀
            public int displayMode = (int)FullScreenMode.FullScreenWindow;
            public int windowWidth;
            public int windowHeight;
            public float masterVolume = 1f;
            public float bgmVolume = 1f;
            public float sfxVolume = 1f;
        }

        private static SettingsData data;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private static SettingsData Data => data ??= Load();

        public static float MasterVolume
        {
            get => Data.masterVolume;
            set
            {
                Data.masterVolume = Mathf.Clamp01(value);
                AudioListener.volume = Data.masterVolume;
            }
        }

        public static float BgmVolume
        {
            get => Data.bgmVolume;
            set
            {
                Data.bgmVolume = Mathf.Clamp01(value);
                VolumeChanged?.Invoke();
            }
        }

        public static float SfxVolume
        {
            get => Data.sfxVolume;
            set
            {
                Data.sfxVolume = Mathf.Clamp01(value);
                VolumeChanged?.Invoke();
            }
        }

        // 배경음·효과음 볼륨이 바뀌면 알림, SoundManager가 받아 재생 중인 소리에도 바로 반영
        public static event Action VolumeChanged;

        public static bool HasDisplay => Data.hasDisplay;
        public static FullScreenMode DisplayMode => (FullScreenMode)Data.displayMode;
        public static Vector2Int WindowSize => new(Data.windowWidth, Data.windowHeight);

        // 화면 모드와 창 모드일 때의 크기를 기억해 둔다 (전체화면 계열이면 창 크기는 이전 값을 유지)
        public static void SetDisplay(FullScreenMode mode, Vector2Int windowSize)
        {
            Data.hasDisplay = true;
            Data.displayMode = (int)mode;
            if (mode == FullScreenMode.Windowed && windowSize.x > 0 && windowSize.y > 0)
            {
                Data.windowWidth = windowSize.x;
                Data.windowHeight = windowSize.y;
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSettings] 저장하지 못했습니다: {exception.Message}");
            }
        }

        private static SettingsData Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    SettingsData loaded = JsonUtility.FromJson<SettingsData>(File.ReadAllText(FilePath));
                    if (loaded != null) return loaded;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[GameSettings] 설정 파일을 읽지 못해 기본값을 씁니다: {exception.Message}");
                }

                return new SettingsData();
            }

            return new SettingsData
            {
                masterVolume = PlayerPrefs.GetFloat(LegacyMasterVolumeKey, 1f),
                bgmVolume = PlayerPrefs.GetFloat(LegacyBgmVolumeKey, 1f),
                sfxVolume = PlayerPrefs.GetFloat(LegacySfxVolumeKey, 1f),
            };
        }

        // 어느 씬에서 시작해도 저장해 둔 볼륨·화면으로 시작
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            data = Load(); // 도메인 리로드를 꺼 둔 채 다시 플레이해도 파일 값으로 시작
            VolumeChanged = null; // 도메인 리로드를 꺼 두면 지난 플레이의 구독이 남으므로 비움
            AudioListener.volume = MasterVolume;

#if !UNITY_EDITOR
            if (HasDisplay == false) return;

            FullScreenMode mode = DisplayMode;
            if (mode == FullScreenMode.Windowed && Data.windowWidth > 0 && Data.windowHeight > 0)
            {
                Screen.SetResolution(Data.windowWidth, Data.windowHeight, mode);
            }
            else if (mode != FullScreenMode.Windowed)
            {
                DisplayInfo display = Screen.mainWindowDisplayInfo;
                int width = display.width > 0 ? display.width : Screen.currentResolution.width;
                int height = display.height > 0 ? display.height : Screen.currentResolution.height;
                Screen.SetResolution(width, height, mode);
            }
#endif
        }
    }
}
