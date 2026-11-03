using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeamOdd.UI
{
    // 메인 화면 메뉴 항목(새 게임 / 이어하기 / 환경설정 / 게임 종료)과 팝업(새 게임 확인, 환경설정, 뒤에 까는 Dim)
    // 팝업은 인게임 증강 칩 장착 창(UIStyleUnifier의 창·버튼 규칙)과 같은 생김새로 만든다
    //   창 = 거의 불투명한 짙은 바탕 + 옅은 1px 외곽선 + 넓고 흐린 그림자, 제목은 SemiBold + 1px 구분선
    //   버튼 = 바탕 없는 외곽선 버튼(올리면 채워지고 선이 밝아짐), 주 버튼 = 밝게 채우고 글자를 어둡게
    // 씬을 다시 만들면 CreateMenuUI·Build가 같이 만들고, 이미 만든 씬은 Apply Title Menu로 메뉴와 팝업만 바꾼다
    public static partial class TitleCinematicSceneBuilder
    {
        private const string BodyFontPath = "Assets/Font/Pretendard/Pretendard-Medium SDF.asset";
        private const float MenuButtonSpacing = 76f; // 메뉴 항목 간격, 일시 정지 메뉴도 같이 씀
        private const float PopupPadding = 36f;

        private static class PopupPalette
        {
            public static readonly Color Text = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
            public static readonly Color WindowTrack = new(0.043f, 0.047f, 0.055f, 0.94f);
            public static readonly Color ListTrack = new(0.06f, 0.065f, 0.075f, 0.98f);
            public static readonly Color WindowShadow = new(0f, 0f, 0f, 0.45f);
            public static readonly Color InverseText = new(0.07f, 0.07f, 0.08f, 1f);
            public static readonly Color Dim = new(0f, 0f, 0f, 0.65f);

            public static Color TextAlpha(float alpha) => new(Text.r, Text.g, Text.b, alpha);
        }

        private enum PopupPanelRole { Window, List, Button, PrimaryButton }

        // 위에서부터 새 게임 / 이어하기 / 환경설정 / 게임 종료, 메뉴 영역 가운데를 기준으로 위아래로 고르게 둠
        private static CanvasGroup[] CreateMenuButtons(RectTransform menuRect, TMP_FontAsset font, StartMenuUI menu)
        {
            (string label, TitleMenuButton.ButtonType type)[] items =
            {
                ("새 게임", TitleMenuButton.ButtonType.NewGame),
                ("이어하기", TitleMenuButton.ButtonType.Continue),
                ("환경설정", TitleMenuButton.ButtonType.Settings),
                ("게임 종료", TitleMenuButton.ButtonType.Quit),
            };

            menuRect.sizeDelta = new Vector2(menuRect.sizeDelta.x, MenuButtonSpacing * items.Length + 20f);
            var groups = new CanvasGroup[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                float y = MenuButtonSpacing * ((items.Length - 1) * 0.5f - index);
                groups[index] = CreateButton(items[index].label, menuRect, font, y, items[index].type, menu);
                if (items[index].type == TitleMenuButton.ButtonType.Continue)
                    Wire(menu, ("continueButton", groups[index].GetComponent<TitleMenuButton>()));
            }

            return groups;
        }

        // Dim·새 게임 확인·환경설정 팝업, 이미 있으면 지우고 다시 만듦. 암전(Blackout) 바로 아래에 두어 씬 전환 때 함께 가려짐
        private static void CreateTitlePopups(StartMenuUI menu)
        {
            Transform root = menu.transform;
            foreach (string name in new[] { "Dim", "New Game Popup", "Settings Popup" })
            {
                Transform old = root.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuFontPath);
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            Image dim = CreateImage("Dim", root, null, PopupPalette.Dim, Vector2.zero, Vector2.zero);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true; // 뒤의 메뉴 항목에 마우스가 닿지 않게

            GameObject newGame = CreateNewGamePopup(root, titleFont, bodyFont, out Button confirm, out Button cancel);
            TitleSettingsPopup settings = CreateSettingsPopup(root, titleFont, bodyFont, menu);

            Transform blackout = root.Find("Blackout");
            foreach (Transform popup in new[] { dim.transform, newGame.transform, settings.transform })
            {
                if (blackout != null) popup.SetSiblingIndex(blackout.GetSiblingIndex());
                popup.gameObject.SetActive(false);
            }

            Wire(menu,
                ("dim", dim.gameObject),
                ("newGamePopup", newGame),
                ("newGameConfirmButton", confirm),
                ("newGameCancelButton", cancel),
                ("settingsPopup", settings));
        }

        private static GameObject CreateNewGamePopup(Transform root, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, out Button confirm, out Button cancel) =>
            CreateConfirmPopup("New Game Popup", root, "새 게임", "저장된 데이터를 삭제하고\n새 게임을 시작하시겠습니까?", titleFont, bodyFont, out confirm, out cancel);

        // 제목 + 가운데 문구 + 예(주 버튼)/아니오, 새 게임 확인과 일시 정지 메뉴의 메인 메뉴·게임 종료 확인이 같이 씀
        private static GameObject CreateConfirmPopup(string name, Transform root, string title, string message,
            TMP_FontAsset titleFont, TMP_FontAsset bodyFont, out Button confirm, out Button cancel)
        {
            RectTransform window = CreatePopupWindow(name, root, title, new Vector2(620f, 300f), titleFont);

            TMP_Text body = CreateText("Message", window, message, bodyFont, 24f,
                PopupPalette.TextAlpha(0.85f), Vector2.zero, Vector2.zero);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(PopupPadding, 116f);
            body.rectTransform.offsetMax = new Vector2(-PopupPadding, -84f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 12f;

            confirm = CreatePopupButton("Confirm Button", window, "예", bodyFont, true, new Vector2(-110f, 32f));
            cancel = CreatePopupButton("Cancel Button", window, "아니오", bodyFont, false, new Vector2(110f, 32f));
            return window.gameObject;
        }

        // 화면(모드·해상도) 두 줄, 구분선, 사운드(마스터·배경음·효과음) 세 줄, 우상단 X
        private static TitleSettingsPopup CreateSettingsPopup(Transform root, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, StartMenuUI menu)
        {
            RectTransform window = CreatePopupWindow("Settings Popup", root, "환경설정", new Vector2(760f, 500f), titleFont);
            Button close = CreateCloseButton(window);

            TMP_Dropdown displayMode = CreateDropdownRow(window, "화면 모드", -140f, bodyFont);
            TMP_Dropdown resolution = CreateDropdownRow(window, "해상도", -204f, bodyFont);

            Image line = CreateImage("Section Line", window, null, PopupPalette.TextAlpha(0.12f), Vector2.zero, Vector2.zero);
            PlacePopupRow(line.rectTransform, -248f, 1f);

            (Slider master, TMP_Text masterValue) = CreateSliderRow(window, "마스터 볼륨", -292f, bodyFont);
            (Slider bgm, TMP_Text bgmValue) = CreateSliderRow(window, "배경음", -356f, bodyFont);
            (Slider sfx, TMP_Text sfxValue) = CreateSliderRow(window, "효과음", -420f, bodyFont);

            TitleSettingsPopup popup = window.gameObject.AddComponent<TitleSettingsPopup>();
            Wire(popup,
                ("displayModeDropdown", displayMode),
                ("resolutionDropdown", resolution),
                ("masterSlider", master),
                ("masterValue", masterValue),
                ("bgmSlider", bgm),
                ("bgmValue", bgmValue),
                ("sfxSlider", sfx),
                ("sfxValue", sfxValue),
                ("closeButton", close),
                ("menu", menu));
            return popup;
        }

        #region POPUP_PARTS

        // 가운데 창: 제목(왼쪽 위) + 1px 구분선, 켜질 때 UIPopIn으로 부드럽게 나타남
        private static RectTransform CreatePopupWindow(string name, Transform parent, string title, Vector2 size, TMP_FontAsset titleFont)
        {
            RectTransform window = CreateRect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            VectorPanel panel = window.gameObject.AddComponent<VectorPanel>();
            panel.raycastTarget = true;
            StylePopupPanel(panel, PopupPanelRole.Window);

            // 제목은 X 버튼 가운데(위에서 48) 높이에 세로 가운데로 맞추고, 아래 구분선은 두지 않는다(증강 칩 창과 같게)
            // 왼쪽 여백은 X 선이 창 오른쪽 끝에서 떨어진 거리와 같게 둔다
            float titleLeft = WindowCloseButtonInstaller.MarkEdgeInset;
            TMP_Text titleText = CreateText("Title", window, title, titleFont, 29f, PopupPalette.Text, Vector2.zero, Vector2.zero);
            PlaceTopLeft(titleText.rectTransform, new Vector2(titleLeft, -24f), new Vector2(size.x - PopupPadding - titleLeft - 60f, 48f));
            titleText.alignment = TextAlignmentOptions.Left;

            window.gameObject.AddComponent<UIPopIn>();
            return window;
        }

        // 아래 가운데 기준 버튼 (200x52)
        private static Button CreatePopupButton(string name, Transform parent, string label, TMP_FontAsset font, bool primary, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0f), position, new Vector2(200f, 52f));
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;

            Button button = AddPopupButton(rect.gameObject, primary ? PopupPanelRole.PrimaryButton : PopupPanelRole.Button);
            TMP_Text text = CreateText("Label", rect, label, font, 24f, primary ? PopupPalette.InverseText : PopupPalette.Text, Vector2.zero, Vector2.zero);
            Stretch(text.rectTransform);
            return button;
        }

        // 우상단 X: 다른 게임 창(임무 목록·증강 칩)과 같은 모양·거리, 사각형 없이 X 주변을 누르면 닫힘
        private static Button CreateCloseButton(RectTransform window) => WindowCloseButtonInstaller.Create(window);

        private static TMP_Dropdown CreateDropdownRow(RectTransform window, string label, float y, TMP_FontAsset font)
        {
            RectTransform control = CreatePopupRowControl(window, label, y, font, 0f);

            GameObject go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = "Dropdown";
            SetLayerRecursive(go.transform, window.gameObject.layer);
            var rect = (RectTransform)go.transform;
            rect.SetParent(control, false);
            Stretch(rect);
            TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();

            // 닫혀 있는 칸: 외곽선 버튼 모양, 글자는 왼쪽, 오른쪽에 V자 화살표
            dropdown.targetGraphic = ReplaceWithPanel(go.GetComponent<Image>(), PopupPanelRole.Button);
            ApplyPopupSelectableColors(dropdown);
            StyleDropdownText(dropdown.captionText, font, new Vector2(16f, 0f), new Vector2(-44f, 0f));

            Transform arrow = rect.Find("Arrow");
            if (arrow != null) Object.DestroyImmediate(arrow.gameObject);
            RectTransform chevron = CreateRect("Arrow", rect, new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(12f, 12f));
            foreach (float side in new[] { -1f, 1f })
            {
                Image bar = CreateImage("Bar", chevron, null, PopupPalette.TextAlpha(0.7f), new Vector2(2.8f * side, 0f), new Vector2(8f, 2f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f * side);
            }

            // 펼친 목록: 창과 같은 짙은 바탕, 항목은 올리면 옅게 채워지고 고른 항목엔 왼쪽 표시선
            RectTransform template = dropdown.template;
            ReplaceWithPanel(template.GetComponent<Image>(), PopupPanelRole.List);
            template.anchoredPosition = new Vector2(0f, -4f);
            template.sizeDelta = new Vector2(0f, 240f);

            Toggle item = template.GetComponentInChildren<Toggle>(true);
            var itemRect = (RectTransform)item.transform;
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 44f);
            var content = (RectTransform)itemRect.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 44f);

            if (item.targetGraphic is Image itemBackground)
            {
                itemBackground.sprite = null;
                itemBackground.color = Color.white;
            }

            ColorBlock colors = item.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = PopupPalette.TextAlpha(0.08f);
            colors.selectedColor = PopupPalette.TextAlpha(0.08f);
            colors.pressedColor = PopupPalette.TextAlpha(0.14f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            item.colors = colors;

            if (item.graphic is Image checkmark)
            {
                checkmark.sprite = null;
                checkmark.color = PopupPalette.Text;
                RectTransform mark = checkmark.rectTransform;
                SetAnchor(mark, new Vector2(0f, 0.5f));
                mark.pivot = new Vector2(0f, 0.5f);
                mark.anchoredPosition = new Vector2(8f, 0f);
                mark.sizeDelta = new Vector2(3f, 20f);
            }

            StyleDropdownText(dropdown.itemText, font, new Vector2(24f, 0f), new Vector2(-12f, 0f));

            Scrollbar scrollbar = template.GetComponentInChildren<Scrollbar>(true);
            if (scrollbar != null)
            {
                var scrollRect = (RectTransform)scrollbar.transform;
                scrollRect.sizeDelta = new Vector2(6f, scrollRect.sizeDelta.y);
                if (scrollbar.TryGetComponent(out Image track))
                {
                    track.sprite = null;
                    track.color = PopupPalette.TextAlpha(0.04f);
                }

                if (scrollbar.handleRect != null)
                {
                    ((RectTransform)scrollbar.handleRect.parent).sizeDelta = Vector2.zero;
                    scrollbar.handleRect.sizeDelta = Vector2.zero;
                    if (scrollbar.handleRect.TryGetComponent(out Image handle))
                    {
                        handle.sprite = null;
                        handle.color = PopupPalette.TextAlpha(0.25f);
                    }
                }
            }

            return dropdown;
        }

        // 슬라이더 선 두께와 손잡이 높이, 처음(4, 14)보다 2/3로 낮춤
        private const float SliderBarHeight = 4f * 2f / 3f;
        private const float SliderHandleHeight = 14f * 2f / 3f;

        // 가는 선 위의 채움과 네모 손잡이, 오른쪽에 수치(0~100)
        private static (Slider slider, TMP_Text value) CreateSliderRow(RectTransform window, string label, float y, TMP_FontAsset font)
        {
            const float ValueWidth = 64f;
            RectTransform control = CreatePopupRowControl(window, label, y, font, ValueWidth + 16f);

            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = "Slider";
            SetLayerRecursive(go.transform, window.gameObject.layer);
            var rect = (RectTransform)go.transform;
            rect.SetParent(control, false);
            Stretch(rect);
            Slider slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 100f;

            if (rect.Find("Background") is RectTransform background) StyleSliderBar(background, PopupPalette.TextAlpha(0.12f));
            if (slider.fillRect != null)
            {
                StyleSliderBar((RectTransform)slider.fillRect.parent, Color.clear);
                Image fill = slider.fillRect.GetComponent<Image>();
                fill.sprite = null;
                fill.color = PopupPalette.TextAlpha(0.85f);
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                handle.sprite = null;
                handle.color = PopupPalette.Text;
                slider.handleRect.anchorMin = new Vector2(slider.handleRect.anchorMin.x, 0.5f);
                slider.handleRect.anchorMax = new Vector2(slider.handleRect.anchorMax.x, 0.5f);
                slider.handleRect.sizeDelta = new Vector2(14f, SliderHandleHeight);
            }

            ApplyPopupSelectableColors(slider);

            TMP_Text value = CreateText("Value", window, "100", font, 22f, PopupPalette.Text, Vector2.zero, Vector2.zero);
            RectTransform valueRect = value.rectTransform;
            valueRect.anchorMin = valueRect.anchorMax = valueRect.pivot = new Vector2(1f, 1f);
            valueRect.anchoredPosition = new Vector2(-PopupPadding, y + 22f);
            valueRect.sizeDelta = new Vector2(ValueWidth, 44f);
            value.alignment = TextAlignmentOptions.Right;
            return (slider, value);
        }

        // 한 줄: 왼쪽 이름 + 오른쪽 조작 영역(높이 44), y는 줄 가운데. rightReserve만큼 오른쪽을 비워 둠
        private static RectTransform CreatePopupRowControl(RectTransform window, string label, float y, TMP_FontAsset font, float rightReserve)
        {
            const float LabelWidth = 220f;
            float width = window.sizeDelta.x;

            TMP_Text text = CreateText($"{label} Label", window, label, font, 24f, PopupPalette.TextAlpha(0.85f), Vector2.zero, Vector2.zero);
            PlaceTopLeft(text.rectTransform, new Vector2(PopupPadding, y + 22f), new Vector2(LabelWidth, 44f));
            text.alignment = TextAlignmentOptions.Left;

            RectTransform control = CreateRect($"{label} Control", window, Vector2.zero, Vector2.zero, Vector2.zero);
            float controlWidth = width - PopupPadding * 2f - LabelWidth - rightReserve;
            PlaceTopLeft(control, new Vector2(PopupPadding + LabelWidth, y + 22f), new Vector2(controlWidth, 44f));
            return control;
        }

        private static void StyleDropdownText(TMP_Text text, TMP_FontAsset font, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (text == null) return;
            if (font != null) text.font = font;
            text.fontSize = 22f;
            text.color = PopupPalette.Text;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.offsetMin = offsetMin;
            text.rectTransform.offsetMax = offsetMax;
        }

        // 슬라이더 바탕·채움 영역을 세로 가운데 4px 선으로
        private static void StyleSliderBar(RectTransform bar, Color color)
        {
            bar.anchorMin = new Vector2(bar.anchorMin.x, 0.5f);
            bar.anchorMax = new Vector2(bar.anchorMax.x, 0.5f);
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, SliderBarHeight);
            if (bar.TryGetComponent(out Image image))
            {
                image.sprite = null;
                image.color = color;
            }
        }

        // 창 좌우 여백 안쪽을 가로지르는 가는 선 (y는 위 기준)
        private static void PlacePopupRow(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(PopupPadding, y - height);
            rect.offsetMax = new Vector2(-PopupPadding, y);
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Button AddPopupButton(GameObject go, PopupPanelRole role)
        {
            VectorPanel panel = go.AddComponent<VectorPanel>();
            panel.raycastTarget = true;
            StylePopupPanel(panel, role);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = panel;
            ApplyPopupSelectableColors(button);
            return button;
        }

        // 기본 컨트롤이 만든 Image를 같은 자리의 VectorPanel로 바꿈 (그래픽은 오브젝트당 하나라 먼저 지움)
        private static VectorPanel ReplaceWithPanel(Image image, PopupPanelRole role)
        {
            GameObject go = image.gameObject;
            bool raycastTarget = image.raycastTarget;
            Object.DestroyImmediate(image);

            VectorPanel panel = go.AddComponent<VectorPanel>();
            panel.raycastTarget = raycastTarget;
            StylePopupPanel(panel, role);
            return panel;
        }

        // UIStyleUnifier.StylePanel의 창·버튼 규칙과 같은 값 (캔버스 기준 1080p라 단위 1)
        private static void StylePopupPanel(VectorPanel panel, PopupPanelRole role)
        {
            Color track, border, hoverTrack = Color.clear, hoverBorder = PopupPalette.TextAlpha(0.85f);
            float shadow = 0f;
            bool hover = false;
            switch (role)
            {
                case PopupPanelRole.Window:
                    track = PopupPalette.WindowTrack;
                    border = PopupPalette.TextAlpha(0.16f);
                    shadow = 18f;
                    break;
                case PopupPanelRole.List:
                    track = PopupPalette.ListTrack;
                    border = PopupPalette.TextAlpha(0.16f);
                    shadow = 8f;
                    break;
                case PopupPanelRole.PrimaryButton:
                    track = PopupPalette.TextAlpha(0.92f);
                    border = PopupPalette.TextAlpha(0.92f);
                    hoverTrack = new Color(1f, 1f, 1f, 0.12f);
                    hoverBorder = Color.white;
                    hover = true;
                    break;
                default:
                    track = Color.clear;
                    border = PopupPalette.TextAlpha(0.22f);
                    hoverTrack = PopupPalette.TextAlpha(0.07f);
                    hover = true;
                    break;
            }

            panel.color = Color.white;
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("trackColor").colorValue = track;
            serialized.FindProperty("borderColor").colorValue = border;
            serialized.FindProperty("shadowColor").colorValue = PopupPalette.WindowShadow;
            serialized.FindProperty("borderWidth").floatValue = 1f;
            serialized.FindProperty("shadowSize").floatValue = shadow;
            serialized.FindProperty("highlightOnHover").boolValue = hover;
            serialized.FindProperty("highlightTrackColor").colorValue = hoverTrack;
            serialized.FindProperty("highlightBorderColor").colorValue = hoverBorder;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 패널이 마우스 반응으로 밝아지므로 색 전환은 눌림·비활성만 남김 (UIStyleUnifier.ApplySelectableColors와 같은 값)
        private static void ApplyPopupSelectableColors(Selectable selectable)
        {
            selectable.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            selectable.colors = colors;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        #endregion
    }
}
