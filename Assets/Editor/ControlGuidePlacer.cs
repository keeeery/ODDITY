using TeamOdd.Mission;
using TeamOdd.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamOdd.EditorTools
{
    // 로비 씬(02. Lobby)의 조작 콘솔(Control Guide NPC)에 말을 걸면 조작 안내 창(ControlGuideWindow)이 열리게 하는 메뉴.
    // 창 틀은 임무 이동 창과 같은 생김새(짙은 반투명 바탕·옅은 1px 외곽선·넓은 그림자, 제목줄 24/48/29, 우상단 X, UIPopIn)로 만든다.
    // 값은 1920x1080 기준이며, 4K 기준인 UI Canvas 창 값(WindowTitleBarUnifier·UIStyleUnifier)을 절반으로 옮겼다.
    // 같은 씬에서 다시 실행하면 창을 지우고 새로 만든 뒤 콘솔에 다시 연결한다.
    internal static class ControlGuidePlacer
    {
        private const string ConsoleName = "Control Guide NPC";
        private const string RootName = "Control Guide UI";
        private const string ParentName = "=UI";
        private const string TitleText = "조작 가이드";
        private const string TitleFontPath = "Assets/Font/Pretendard/Pretendard-SemiBold SDF.asset";

        // 콘솔 겉면에서 이 거리(m) 안이면 말을 걸 수 있다
        private const float InteractDistance = 2f;
        // UI Canvas(10)와 같은 층, 상호작용 안내(90)·시스템 메시지(100)보다 아래
        private const int SortingOrder = 10;

        // 1920x1080 기준 창 틀
        private const float TitleTop = 24f;
        private const float TitleHeight = 48f;
        private const float TitleFontSize = 29f;
        private const float ContentTop = 96f;
        private const float ContentBottom = 40f;
        // 제목 오른쪽 끝을 X 누름 영역 왼쪽에서 멈춘다
        private const float TitleRightReserve = 80f;

        private static readonly Color TextColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        private static readonly Color WindowTrack = new(0.043f, 0.047f, 0.055f, 0.94f);
        private static readonly Color WindowBorder = new(0.84705883f, 0.8235294f, 0.76862746f, 0.16f);
        private static readonly Color WindowShadow = new(0f, 0f, 0f, 0.45f);
        private const float WindowBorderWidth = 1f;
        private const float WindowShadowSize = 18f;

        [MenuItem("Tools/Team Odd/Set Up Lobby Control Guide")]
        private static void SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject console = FindInScene(scene, ConsoleName);
            if (console == null)
            {
                Debug.LogError($"[ControlGuidePlacer] {scene.name}에서 '{ConsoleName}'을(를) 찾지 못했습니다. 로비 씬을 열고 실행하세요.");
                return;
            }

            GameObject old = FindInScene(scene, RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            GameObject guide = BuildWindow();
            Undo.RegisterCreatedObjectUndo(guide, "Set Up Control Guide");
            GameObject parent = FindInScene(scene, ParentName);
            if (parent != null) Undo.SetTransformParent(guide.transform, parent.transform, "Set Up Control Guide");

            ConnectConsole(console, guide);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = console;
            Debug.Log($"[ControlGuidePlacer] {scene.name}의 {ConsoleName}에 조작 안내 창을 연결했습니다. 씬을 저장하세요.");
        }

        private static GameObject BuildWindow()
        {
            GameObject root = new(RootName, typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.AddComponent<GraphicRaycaster>();
            ControlGuideWindow guide = root.AddComponent<ControlGuideWindow>();

            // 창: 내용 크기에 맞춰 늘어나고 화면 가운데에 놓인다
            RectTransform window = NewUI("Window", root.transform);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            VectorPanel panel = window.gameObject.AddComponent<VectorPanel>();
            SerializedObject panelSettings = new(panel);
            panelSettings.FindProperty("trackColor").colorValue = WindowTrack;
            panelSettings.FindProperty("borderColor").colorValue = WindowBorder;
            panelSettings.FindProperty("shadowColor").colorValue = WindowShadow;
            panelSettings.FindProperty("borderWidth").floatValue = WindowBorderWidth;
            panelSettings.FindProperty("shadowSize").floatValue = WindowShadowSize;
            panelSettings.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.AddComponent<UIPopIn>();

            int side = Mathf.RoundToInt(WindowCloseButtonInstaller.MarkEdgeInset);
            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(side, side, Mathf.RoundToInt(ContentTop), Mathf.RoundToInt(ContentBottom));
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = window.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddTitle(window);
            RectTransform content = NewUI("Content", window);

            Button close = WindowCloseButtonInstaller.Create(window, true);
            close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UnityEventTools.AddPersistentListener(close.onClick, new UnityAction(guide.Close));

            SerializedObject guideSettings = new(guide);
            guideSettings.FindProperty("content").objectReferenceValue = content;
            guideSettings.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return root;
        }

        // 임무 이동 창 제목과 같은 글꼴·크기·자간, 창 위에서 TitleTop 띄운 줄의 세로 가운데에 왼쪽 정렬
        private static void AddTitle(RectTransform window)
        {
            RectTransform rect = NewUI("Title", window);
            float left = WindowCloseButtonInstaller.MarkEdgeInset;
            float right = WindowCloseButtonInstaller.MarkEdgeInset + TitleRightReserve;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, -TitleTop);
            rect.sizeDelta = new Vector2(-(left + right), TitleHeight);
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            TextMeshProUGUI title = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            if (font != null) title.font = font;
            title.text = TitleText;
            title.fontSize = TitleFontSize;
            title.color = TextColor;
            title.characterSpacing = 2f;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.raycastTarget = false;
        }

        private static void ConnectConsole(GameObject console, GameObject guide)
        {
            if (console.TryGetComponent(out MissionNPC npc) == false)
                npc = Undo.AddComponent<MissionNPC>(console);

            SerializedObject serialized = new(npc);
            serialized.FindProperty("popupUI").objectReferenceValue = guide;
            serialized.FindProperty("measureFromBounds").boolValue = true;
            serialized.FindProperty("distance").floatValue = InteractDistance;
            serialized.ApplyModifiedProperties();
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, objectName);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindDeep(Transform current, string objectName)
        {
            if (current.name == objectName) return current;
            foreach (Transform child in current)
            {
                Transform found = FindDeep(child, objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static RectTransform NewUI(string objectName, Transform parent)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
