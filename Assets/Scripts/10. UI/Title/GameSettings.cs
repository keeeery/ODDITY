using UnityEngine;

namespace TeamOdd
{
    // 환경설정 값 (PlayerPrefs, 0~1)
    // 화면 모드·해상도는 빌드된 게임이 마지막 값을 스스로 기억하므로 따로 저장하지 않는다.
    // 아직 사운드가 없어 마스터 볼륨만 AudioListener에 반영하고, 배경음·효과음은 값만 저장해 둔다 (사운드가 생기면 믹서에 연결)
    public static class GameSettings
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string BgmVolumeKey = "Settings.BgmVolume";
        private const string SfxVolumeKey = "Settings.SfxVolume";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = MasterVolume;
            }
        }

        public static float BgmVolume
        {
            get => PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
            set => PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public static void Save() => PlayerPrefs.Save();

        // 어느 씬에서 시작해도 저장해 둔 마스터 볼륨으로 시작
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
