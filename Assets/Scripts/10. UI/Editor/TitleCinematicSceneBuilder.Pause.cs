using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeamOdd.UI
{
    // 인게임 Esc 일시 정지 메뉴 프리팹(Resources/UI/Pause Menu.prefab)
    // 메인 화면 메뉴와 같은 글자 버튼·Dim·팝업(새 게임 확인 팝업·환경설정)을 써서 같은 생김새로 만든다
    //   메인 화면 팝업보다 짙은 Dim 한 겹(팝업이 뜨면 메뉴 위로 옮겨 감) 위 화면 가운데에 계속하기 / 정비소로 이동 / 환경설정 / 메인 메뉴 / 게임 종료를 세로로 나열하고, 우하단 끝에 일시 정지 아이콘(Ⅱ)
    //   정비소로 이동은 로비에서 숨기며, 세로 정렬(VerticalLayoutGroup)이라 숨기면 나머지가 붙음
    // 대상 씬(로비·보스 3종)이 열릴 때 PauseMenuUI가 이 프리팹을 띄우므로 씬은 건드리지 않는다
    public static partial class TitleCinematicSceneBuilder
    {
        private const string PausePrefabPath = "Assets/Resources/UI/Pause Menu.prefab";
        private const int PauseSortingOrder = 500; // 보상 창(200)·사망 화면(300)보다 위, 씬 전환(1000)보다 아래
        private static readonly Color PauseDim = new(0f, 0f, 0f, 0.85f);
        private static readonly Vector2 PauseIconMargin = new(64f, 56f); // 화면 오른쪽·아래 끝에서 아이콘까지

        [MenuItem("Tools/Team Odd/Build Pause Menu Prefab")]
        private static void BuildPauseMenu()
        {
            var menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuFontPath);
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            var canvasObject = new GameObject("Pause Menu", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PauseSortingOrder;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            PauseMenuUI pause = canvasObject.AddComponent<PauseMenuUI>();

            RectTransform content = CreateRect("Content", canvasObject.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(content);
            content.gameObject.AddComponent<CanvasGroup>();

            Image dim = CreateImage("Dim", content, null, PauseDim, Vector2.zero, Vector2.zero);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true; // 뒤의 HUD에 마우스가 닿지 않게

            GameObject lobbyButton = CreatePauseMenuList(content, menuFont, pause, out RectTransform menuGroup);
            RectTransform pauseIcon = CreatePauseIcon(content);

            // 팝업: 메뉴 뒤 Dim 대신 메뉴 위에 같은 진하기의 Dim을 깔고 가운데에 띄움 (두 겹으로 겹치지 않음)
            Image popupDim = CreateImage("Popup Dim", content, null, PauseDim, Vector2.zero, Vector2.zero);
            Stretch(popupDim.rectTransform);
            popupDim.raycastTarget = true;

            GameObject lobby = CreateConfirmPopup("Lobby Popup", content, "정비소로 이동", "진행 중인 임무를 중단하고\n정비소로 이동하시겠습니까?",
                menuFont, bodyFont, out Button lobbyConfirm, out Button lobbyCancel);
            GameObject mainMenu = CreateConfirmPopup("Main Menu Popup", content, "메인 메뉴", "메인 메뉴로 돌아가시겠습니까?",
                menuFont, bodyFont, out Button mainMenuConfirm, out Button mainMenuCancel);
            GameObject quit = CreateConfirmPopup("Quit Popup", content, "게임 종료", "게임을 종료하시겠습니까?",
                menuFont, bodyFont, out Button quitConfirm, out Button quitCancel);
            TitleSettingsPopup settings = CreateSettingsPopup(content, menuFont, bodyFont, null);

            foreach (GameObject popup in new[] { popupDim.gameObject, lobby, mainMenu, quit, settings.gameObject })
                popup.SetActive(false);

            Wire(pause,
                ("content", content.gameObject),
                ("dim", dim.gameObject),
                ("lobbyButton", lobbyButton),
                ("menuGroup", menuGroup),
                ("pauseIcon", pauseIcon),
                ("popupDim", popupDim.gameObject),
                ("lobbyPopup", lobby),
                ("lobbyConfirmButton", lobbyConfirm),
                ("lobbyCancelButton", lobbyCancel),
                ("mainMenuPopup", mainMenu),
                ("mainMenuConfirmButton", mainMenuConfirm),
                ("mainMenuCancelButton", mainMenuCancel),
                ("quitPopup", quit),
                ("quitConfirmButton", quitConfirm),
                ("quitCancelButton", quitCancel),
                ("settingsPopup", settings));

            content.gameObject.SetActive(false);

            Directory.CreateDirectory(Path.GetDirectoryName(PausePrefabPath));
            PrefabUtility.SaveAsPrefabAsset(canvasObject, PausePrefabPath, out bool success);
            Object.DestroyImmediate(canvasObject);

            if (success) Debug.Log($"[Pause] 일시 정지 메뉴 프리팹을 만들었습니다: {PausePrefabPath}");
            else Debug.LogError($"[Pause] 일시 정지 메뉴 프리팹을 저장하지 못했습니다: {PausePrefabPath}");
        }

        // 화면 가운데 세로 목록, 정비소로 이동 항목을 돌려줌
        private static GameObject CreatePauseMenuList(Transform parent, TMP_FontAsset menuFont, PauseMenuUI pause, out RectTransform list)
        {
            list = CreateRect("Menu Group", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 0f));
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreatePauseButton("계속하기", list, menuFont, TitleMenuButton.ButtonType.Resume, pause);
            GameObject lobby = CreatePauseButton("정비소로 이동", list, menuFont, TitleMenuButton.ButtonType.Lobby, pause);
            CreatePauseButton("환경설정", list, menuFont, TitleMenuButton.ButtonType.Settings, pause);
            CreatePauseButton("메인 메뉴", list, menuFont, TitleMenuButton.ButtonType.MainMenu, pause);
            CreatePauseButton("게임 종료", list, menuFont, TitleMenuButton.ButtonType.Quit, pause);
            return lobby;
        }

        // 우하단 끝 일시 정지 아이콘: 세로 막대 두 개, 메뉴 글자와 같은 색
        private static RectTransform CreatePauseIcon(Transform parent)
        {
            const float BarWidth = 10f;
            const float BarHeight = 38f;
            const float Gap = 10f;

            RectTransform icon = CreateRect("Pause Icon", parent, Vector2.one, Vector2.zero, new Vector2(BarWidth * 2f + Gap, BarHeight));
            SetAnchor(icon, new Vector2(1f, 0f));
            icon.pivot = new Vector2(1f, 0f);
            icon.anchoredPosition = new Vector2(-PauseIconMargin.x, PauseIconMargin.y);
            icon.gameObject.AddComponent<CanvasGroup>();

            foreach (float x in new[] { -(BarWidth + Gap) * 0.5f, (BarWidth + Gap) * 0.5f })
                CreateImage("Bar", icon, null, new Color(1f, 1f, 1f, 0.9f), new Vector2(x, 0f), new Vector2(BarWidth, BarHeight));

            return icon;
        }

        private static RectTransform CreatePauseRow(string name, Transform parent, float height)
        {
            RectTransform row = CreateRect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, height));
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            row.gameObject.AddComponent<CanvasGroup>(); // 열 때 줄마다 차례로 나타나는 투명도
            return row;
        }

        // 메인 화면 메뉴 글자 버튼(CreateButton)과 같은 값, 가운데 정렬
        // 글자는 줄(LayoutGroup이 배치) 가운데에 두어, 버튼이 매 프레임 글자 위치를 되돌려도 정렬과 부딪히지 않음
        private static GameObject CreatePauseButton(string label, Transform parent, TMP_FontAsset font, TitleMenuButton.ButtonType type, PauseMenuUI pause)
        {
            RectTransform row = CreatePauseRow(label, parent, MenuButtonSpacing);

            TMP_Text text = CreateText("Label", row, label, font, 40f, new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(420f, 64f));
            text.raycastTarget = true;
            text.characterSpacing = 6f;

            text.gameObject.AddComponent<CanvasGroup>();
            TitleMenuButton button = text.gameObject.AddComponent<TitleMenuButton>();
            Wire(button, ("buttonType", type), ("pauseMenu", pause), ("glitch", CreateGlitch(text)));

            var serialized = new SerializedObject(button);
            serialized.FindProperty("hoverScaleMultiplier").floatValue = 1.1f;
            serialized.FindProperty("labelShift").floatValue = 0f;
            serialized.FindProperty("glitchStrength").floatValue = 0.25f;
            serialized.FindProperty("pressedColor").colorValue = new Color(0.7f, 0.7f, 0.7f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return row.gameObject;
        }
    }
}
