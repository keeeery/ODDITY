using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 장착 데모 UI를 씬에 생성하는 에디터 메뉴.
    /// 4x4 그리드 + 칩 트레이 + 스탯/정보 텍스트를 만들고, 각 UI 컴포넌트의 참조를 연결해 둔다.
    /// 칩 자체는 테이블에서 읽어 런타임에 AugmentationChipPanel이 만든다.
    /// </summary>
    public static class AugmentationChipUIBuilder
    {
        private const string RootName = "AugmentationChipDemo";
        private const int GridSize = 4;
        private const float TrayPadding = 12f;

        private static readonly Color PanelColor = new(0.10f, 0.11f, 0.14f, 0.95f);
        private static readonly Color CellColor = new(0.20f, 0.22f, 0.27f, 1f);
        private static readonly Color ButtonColor = new(0.30f, 0.32f, 0.38f, 1f);

        private static Font font;

        // 선택한 오브젝트가 속한 Canvas(없으면 씬의 첫 Canvas) 아래에 만든다. 같은 이름이 있으면 교체한다
        [MenuItem("Tools/Superpower/Build Augmentation Chip UI")]
        private static void Build()
        {
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Build Augmentation Chip UI", "씬에 Canvas가 없습니다. Canvas를 먼저 만들어 주세요.", "확인");
                return;
            }

            Transform existing = canvas.transform.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform root = CreatePanel(RootName, canvas.transform, new Vector2(908f, 500f), PanelColor);

            CreateText("Title", root, "증강칩 장착 데모", 24, TextAnchor.UpperLeft,
                new Vector2(24f, -16f), new Vector2(400f, 32f));

            ChipGridView grid = BuildGrid(root);
            ChipTrayView tray = BuildTray(root);

            Text statText = CreateText("Stat", root, string.Empty, 16, TextAnchor.UpperLeft,
                new Vector2(24f, -376f), new Vector2(312f, 64f));

            Text infoText = CreateText("Info", root, "칩을 드래그해서 그리드에 올려보세요. (드래그 중 R: 회전)", 16,
                TextAnchor.UpperLeft, new Vector2(24f, -446f), new Vector2(860f, 40f));

            AugmentationChipPanel panel = root.gameObject.AddComponent<AugmentationChipPanel>();
            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("grid").objectReferenceValue = grid;
            so.FindProperty("tray").objectReferenceValue = tray;
            so.FindProperty("statText").objectReferenceValue = statText;
            so.FindProperty("infoText").objectReferenceValue = infoText;
            so.FindProperty("labelFont").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Augmentation Chip UI");
            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        private static ChipGridView BuildGrid(RectTransform root)
        {
            float gridLength = ChipGridMetrics.Length(GridSize);
            RectTransform gridRect = CreateRect("Grid", root);
            SetTopLeft(gridRect, new Vector2(24f, -64f), new Vector2(gridLength, gridLength));

            ChipGridView grid = gridRect.gameObject.AddComponent<ChipGridView>();
            SerializedObject so = new SerializedObject(grid);
            so.FindProperty("gridSize").intValue = GridSize;
            so.FindProperty("cellColor").colorValue = CellColor;
            SerializedProperty cells = so.FindProperty("cells");
            cells.arraySize = GridSize * GridSize;

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    RectTransform cell = CreateRect($"Cell_{x}_{y}", gridRect);
                    SetTopLeft(cell, ChipGridMetrics.CellPosition(new Vector2Int(x, y)),
                        new Vector2(ChipGridMetrics.CellSize, ChipGridMetrics.CellSize));
                    Image image = cell.gameObject.AddComponent<Image>();
                    image.color = CellColor;
                    image.raycastTarget = false;
                    cells.GetArrayElementAtIndex(y * GridSize + x).objectReferenceValue = image;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return grid;
        }

        private static ChipTrayView BuildTray(RectTransform root)
        {
            Vector2 traySize = new Vector2(540f, 372f);
            RectTransform trayRect = CreatePanel("Tray", root, traySize, new Color(0.15f, 0.16f, 0.20f, 1f));
            SetTopLeft(trayRect, new Vector2(344f, -64f), traySize);

            CreateText("TrayTitle", trayRect, "보유 칩", 16, TextAnchor.UpperLeft, new Vector2(12f, -8f), new Vector2(200f, 24f));

            ChipTrayView tray = trayRect.gameObject.AddComponent<ChipTrayView>();
            SerializedObject so = new SerializedObject(tray);

            BuildSortButtons(trayRect, so.FindProperty("sortButtons"));
            RectTransform content = BuildScrollView(trayRect, new Vector2(TrayPadding, -68f),
                new Vector2(traySize.x - TrayPadding * 2f, traySize.y - 68f - TrayPadding));

            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("padding").floatValue = TrayPadding;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tray;
        }

        // 클릭 리스너는 런타임에 ChipTrayView가 붙인다
        private static void BuildSortButtons(RectTransform tray, SerializedProperty sortButtons)
        {
            (ChipSortKey key, string label)[] buttons =
            {
                (ChipSortKey.ID, "ID"), (ChipSortKey.Name, "이름"), (ChipSortKey.StatType, "스탯"),
                (ChipSortKey.Value, "수치"), (ChipSortKey.Size, "크기"),
            };

            const float buttonWidth = 76f;
            const float buttonGap = 6f;
            sortButtons.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = CreatePanel($"Sort_{buttons[i].key}", tray, Vector2.zero, ButtonColor);
                SetTopLeft(rect, new Vector2(TrayPadding + i * (buttonWidth + buttonGap), -36f), new Vector2(buttonWidth, 24f));
                Image image = rect.GetComponent<Image>();
                image.raycastTarget = true;

                Text text = CreateText("Label", rect, buttons[i].label, 13, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(buttonWidth, 24f));

                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                SerializedProperty element = sortButtons.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").enumValueIndex = (int)buttons[i].key;
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("text").objectReferenceValue = text;
                element.FindPropertyRelative("label").stringValue = buttons[i].label;
            }
        }

        // 마스크된 뷰포트 + 세로 스크롤바. 칩은 content 아래에 배치된다
        private static RectTransform BuildScrollView(RectTransform tray, Vector2 position, Vector2 size)
        {
            const float scrollbarWidth = 12f;

            RectTransform view = CreateRect("ScrollView", tray);
            SetTopLeft(view, position, size);
            ScrollRect scrollRect = view.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // 빈 공간에서도 휠이 먹도록 투명 Image를 레이캐스트 대상으로 둔다
            RectTransform viewport = CreatePanel("Viewport", view, Vector2.zero, new Color(0f, 0f, 0f, 0.15f));
            SetTopLeft(viewport, Vector2.zero, new Vector2(size.x - scrollbarWidth - 4f, size.y));
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", viewport);
            SetTopLeft(content, Vector2.zero, new Vector2(viewport.sizeDelta.x, 0f));

            RectTransform bar = CreatePanel("Scrollbar", view, Vector2.zero, new Color(0f, 0f, 0f, 0.3f));
            SetTopLeft(bar, new Vector2(size.x - scrollbarWidth, 0f), new Vector2(scrollbarWidth, size.y));
            bar.GetComponent<Image>().raycastTarget = true;

            RectTransform slidingArea = CreateRect("SlidingArea", bar);
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = slidingArea.offsetMax = Vector2.zero;

            RectTransform handle = CreatePanel("Handle", slidingArea, Vector2.zero, ButtonColor);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;

            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            return content;
        }

        #region Helpers

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            SetTopLeft(rect, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        // 부모 좌상단 기준으로 배치 (anchor/pivot 모두 좌상단)
        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        #endregion
    }
}
