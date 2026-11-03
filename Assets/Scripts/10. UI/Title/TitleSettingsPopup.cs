using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd
{
    // 메인 화면의 환경설정 팝업
    // - 화면 모드: 전체화면 / 전체 창 모드 / 창 모드, 고르는 즉시 바뀜
    // - 해상도: 창 모드일 때만 지금 모니터보다 크지 않은 해상도 중에서 고르고, 전체화면·전체 창 모드는 모니터 해상도로 고정
    // - 마스터 볼륨 / 배경음 / 효과음 슬라이더(0~100), 옆에 수치를 표시
    // 에디터의 Game 창에서는 화면 모드·해상도가 바뀌지 않음 (빌드에서 확인)
    public class TitleSettingsPopup : MonoBehaviour
    {
        private static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        private static readonly string[] ModeLabels = { "전체화면", "전체 창 모드", "창 모드" };

        [Header("Display")]
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Sound")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private TMP_Text bgmValue;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text sfxValue;

        [Header("Close")]
        [SerializeField] private Button closeButton;
        [SerializeField] private StartMenuUI menu;

        private readonly List<Vector2Int> resolutions = new(); // 해상도 목록에 보이는 순서
        private FullScreenMode mode;
        private Vector2Int windowSize; // SetResolution 직후엔 Screen.width가 다음 프레임에야 바뀌므로 고른 값을 따로 들고 있음

        private void Awake()
        {
            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(new List<string>(ModeLabels));
            displayModeDropdown.onValueChanged.AddListener(OnModeChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

            BindSlider(masterSlider, masterValue, value => GameSettings.MasterVolume = value);
            BindSlider(bgmSlider, bgmValue, value => GameSettings.BgmVolume = value);
            BindSlider(sfxSlider, sfxValue, value => GameSettings.SfxVolume = value);

            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        // 열 때마다 지금 화면 상태와 저장된 볼륨으로 다시 맞춤
        private void OnEnable()
        {
            mode = Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? FullScreenMode.FullScreenWindow : Screen.fullScreenMode;
            windowSize = new Vector2Int(Screen.width, Screen.height);
            displayModeDropdown.SetValueWithoutNotify(System.Array.IndexOf(Modes, mode));
            RefreshResolutions();

            SetSlider(masterSlider, masterValue, GameSettings.MasterVolume);
            SetSlider(bgmSlider, bgmValue, GameSettings.BgmVolume);
            SetSlider(sfxSlider, sfxValue, GameSettings.SfxVolume);
        }

        private void OnDisable()
        {
            GameSettings.Save();
        }

        private void Close()
        {
            if (menu != null) menu.ClosePopup();
            else gameObject.SetActive(false);
        }

        #region DISPLAY

        private static Vector2Int MonitorSize
        {
            get
            {
                DisplayInfo display = Screen.mainWindowDisplayInfo;
                if (display.width > 0 && display.height > 0) return new Vector2Int(display.width, display.height);
                return new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            }
        }

        private void OnModeChanged(int index)
        {
            mode = Modes[index];
            Vector2Int monitor = MonitorSize;

            // 창 모드로 바꾸면 모니터를 꽉 채우지 않는 가장 큰 해상도로 시작
            if (mode == FullScreenMode.Windowed)
            {
                if (windowSize.x >= monitor.x || windowSize.y >= monitor.y)
                {
                    List<Vector2Int> sizes = WindowedSizes(monitor);
                    windowSize = sizes.Find(size => size.x < monitor.x && size.y < monitor.y);
                    if (windowSize == Vector2Int.zero) windowSize = monitor;
                }

                Screen.SetResolution(windowSize.x, windowSize.y, mode);
            }
            else
            {
                Screen.SetResolution(monitor.x, monitor.y, mode);
            }

            RefreshResolutions();
        }

        private void OnResolutionChanged(int index)
        {
            if (mode != FullScreenMode.Windowed || index < 0 || index >= resolutions.Count)
                return;

            windowSize = resolutions[index];
            Screen.SetResolution(windowSize.x, windowSize.y, mode);
        }

        // 창 모드면 고를 수 있는 목록을, 아니면 모니터 해상도 하나만 보이고 막음
        private void RefreshResolutions()
        {
            Vector2Int monitor = MonitorSize;
            bool windowed = mode == FullScreenMode.Windowed;

            resolutions.Clear();
            if (windowed)
            {
                resolutions.AddRange(WindowedSizes(monitor));
                if (resolutions.Contains(windowSize) == false) resolutions.Insert(0, windowSize);
            }
            else
            {
                resolutions.Add(monitor);
            }

            var labels = new List<string>(resolutions.Count);
            foreach (Vector2Int size in resolutions) labels.Add($"{size.x} x {size.y}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(windowed ? resolutions.IndexOf(windowSize) : 0);
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.interactable = windowed;
            if (resolutionDropdown.captionText != null) resolutionDropdown.captionText.alpha = windowed ? 1f : 0.4f; // 고정된 값은 흐리게
        }

        // 모니터가 지원하는 해상도 중 모니터보다 크지 않은 것 (주사율만 다른 항목은 하나로), 큰 것부터
        private static List<Vector2Int> WindowedSizes(Vector2Int monitor)
        {
            var sizes = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.x <= monitor.x && size.y <= monitor.y && sizes.Contains(size) == false) sizes.Add(size);
            }

            if (sizes.Contains(monitor) == false) sizes.Add(monitor);
            sizes.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return sizes;
        }

        #endregion

        #region SOUND

        private static void BindSlider(Slider slider, TMP_Text label, System.Action<float> save)
        {
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(value =>
            {
                save(value / 100f);
                label.SetText("{0}", value);
            });
        }

        private static void SetSlider(Slider slider, TMP_Text label, float volume)
        {
            float value = Mathf.Round(volume * 100f);
            slider.SetValueWithoutNotify(value);
            label.SetText("{0}", value);
        }

        #endregion
    }
}
