using System;
using System.IO;
using UnityEngine;

namespace TeamOdd
{
    // 환경설정 값을 로컬(persistentDataPath/settings.json)에 저장하고 게임을 켤 때 불러와 적용한다.
    // - 화면 모드·창 크기는 저장하지 않음, 게임을 실행했을 때의 화면을 그대로 따르고 설정 창도 지금 화면 상태를 보여 줌
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

        // 어느 씬에서 시작해도 저장해 둔 볼륨으로 시작
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            data = Load(); // 도메인 리로드를 꺼 둔 채 다시 플레이해도 파일 값으로 시작
            VolumeChanged = null; // 도메인 리로드를 꺼 두면 지난 플레이의 구독이 남으므로 비움
            AudioListener.volume = MasterVolume;
        }
    }
}
