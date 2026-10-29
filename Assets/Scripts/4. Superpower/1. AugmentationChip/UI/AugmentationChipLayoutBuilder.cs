using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 패널 전체 배치. 그리드 크기에 맞춰 그리드 칸을 만들고, 그리드/트레이/스킬 슬롯 줄/텍스트 위치와 패널 크기를 정한다.
    /// 에디터에서만 동작한다. 값을 바꾼 뒤 컴포넌트 우클릭(⋮) → "Build Layout"을 누르면 프리팹에 결과가 저장되고,
    /// 런타임에는 저장된 칸과 위치를 그대로 쓴다.
    /// 패널은 앵커·피벗이 중앙이라 전체 크기가 바뀌어도 중심은 화면 중앙에 남는다.
    /// 스킬 슬롯 줄(SkillBar)이 없으면 만들어서 연결한다. 슬롯 칸은 SkillSlotBarView가 런타임에 줄 크기에 맞춰 만든다.
    /// 트레이의 필터 토글(공격/방어/유틸/스탯)도 없으면 만들어서 연결한다. 글꼴과 색은 첫 번째 정렬 버튼을 따른다.
    /// 수치는 3840x2160 캔버스 기준이다.
    ///
    ///   ┌ padding ─────────────────────────────────────┐
    ///   │ Title                                         │
    ///   │ ┌ Grid (N x N) ┐ spacing ┌ Tray ───────────┐ │ ← headerHeight
    ///   │ │              │         │ 필터 토글        │ │
    ///   │ │              │         │ 정렬 버튼        │ │
    ///   │ └──────────────┘         │ 보유 칩          │ │
    ///   │                          └─────────────────┘ │
    ///   │ ┌ SkillBar (스킬 슬롯 1~5) ─────────────────┐ │ ← skillBarGap
    ///   │ └───────────────────────────────────────────┘ │
    ///   │ Info                                          │
    ///   └───────────────────────────────────────────────┘
    /// </summary>
    public class AugmentationChipLayoutBuilder : MonoBehaviour
    {
        // 필터 토글을 새로 만들 때 쓰는 순서와 글자
        private static readonly ChipFilter[] Filters = { ChipFilter.Attack, ChipFilter.Defense, ChipFilter.Utility, ChipFilter.Stat };
        private static readonly string[] FilterLabels = { "공격", "방어", "유틸", "스탯" };

        [Header("Grid")]
        [SerializeField, Min(1)] private int gridSize = 4;

        [Header("Tray")]
        [SerializeField, Min(1)] private int trayMinChipCells = 4; // 트레이 한 줄에 최소 들어가야 하는 칩 폭(칸)
        // 트레이 자체 폭 패딩
        // trayMinChipCells는 칸 사이 틈(CellGap)으로 폭을 재지만, 트레이에서는 칩끼리 ChipSpacing만큼 떨어져 놓인다
        // 예) 폭 3짜리 칩 두 개를 한 줄에 놓으려면 6칸 폭에 (ChipSpacing - CellGap)만큼 더 필요하다
        [SerializeField] private float trayPadding = 24f;
        [SerializeField] private float trayMinHeight = 1116f;       // 그리드가 이보다 작아도 트레이는 이 높이를 유지한다
        [SerializeField] private float trayInnerPadding = 36f;     // 트레이 가장자리 ~ 필터 토글/정렬 버튼/스크롤 영역
        [SerializeField] private float filterToggleTop = 108f;      // 트레이 위쪽 ~ 필터 토글 (위는 "보유 칩" 제목)
        [SerializeField] private float filterToggleHeight = 72f;   // 체크 상자는 이 높이의 정사각형
        [SerializeField] private float filterToggleGap = 18f;
        [SerializeField] private float filterLabelGap = 12f;        // 체크 상자 ~ 글자
        [SerializeField] private float filterCheckInset = 12f;      // 체크 상자 가장자리 ~ 체크 표시 (새로 만들 때만 쓴다)
        [SerializeField] private float sortButtonTop = 198f;       // 트레이 위쪽 ~ 정렬 버튼 (필터 토글 아래)
        [SerializeField] private float sortButtonHeight = 72f;
        [SerializeField] private float sortButtonGap = 18f;
        [SerializeField] private float scrollTop = 294f;           // 트레이 위쪽 ~ 스크롤 영역
        [SerializeField] private float scrollbarWidth = 36f;
        [SerializeField] private float scrollbarGap = 12f;

        [Header("Skill Bar")]
        [SerializeField] private float skillBarGap = 30f;    // 그리드/트레이 아래 ~ 스킬 슬롯 줄
        [SerializeField] private float skillBarHeight = 168f;

        [Header("Spacing")]
        [SerializeField] private float padding = 72f;
        [SerializeField] private float spacing = 60f;       // 그리드와 트레이 사이
        [SerializeField] private float headerHeight = 192f; // 패널 위쪽 ~ 그리드/트레이 위쪽 (제목 영역)
        [SerializeField] private float titleTop = 48f;
        [SerializeField] private float titleHeight = 96f;
        [SerializeField] private float infoGap = 30f;       // 본문 아래 ~ 정보 텍스트
        [SerializeField] private float infoHeight = 192f;

        [Header("References")]
        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform info;
        [SerializeField] private SkillSlotBarView skillBar; // 비워두면 Build Layout이 만든다

#if UNITY_EDITOR
        // 씬에 놓인 프리팹 인스턴스에서 만들면 칸이 전부 "추가된 오브젝트" 오버라이드로 쌓이므로 프리팹 안에서만 허용한다
        [ContextMenu("Build Layout")]
        private void BuildLayout()
        {
            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                Debug.LogWarning("[AugmentationChipLayoutBuilder] 프리팹을 열어서(Prefab Mode) Build Layout을 실행해 주세요.", this);
                return;
            }

            // 패널 아래 RectTransform 변경을 한 번에 기록해 Ctrl+Z로 되돌릴 수 있게 한다
            Undo.RegisterFullObjectHierarchyUndo(gameObject, "Build Augmentation Chip Layout");

            BuildGrid();
            EnsureSkillBar();
            EnsureFilterToggles();

            float gridLength = ChipGridMetrics.Length(gridSize);
            Vector2 traySize = new Vector2(GetTrayMinWidth() + trayPadding, Mathf.Max(gridLength, trayMinHeight));
            LayoutTray(traySize);

            float width = padding + gridLength + spacing + traySize.x + padding;
            float height = headerHeight + traySize.y + skillBarGap + skillBarHeight + infoGap + infoHeight + padding;

            RectTransform root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(width, height);

            float innerWidth = width - padding * 2f;
            float skillBarTop = headerHeight + traySize.y + skillBarGap;
            SetTopLeft(title, new Vector2(padding, -titleTop), new Vector2(innerWidth, titleHeight));
            SetTopLeft((RectTransform)grid.transform, new Vector2(padding, -headerHeight), new Vector2(gridLength, gridLength));
            SetTopLeft((RectTransform)tray.transform, new Vector2(padding + gridLength + spacing, -headerHeight), traySize);
            SetTopLeft((RectTransform)skillBar.transform, new Vector2(padding, -skillBarTop), new Vector2(innerWidth, skillBarHeight));
            SetTopLeft(info, new Vector2(padding, -(skillBarTop + skillBarHeight + infoGap)), new Vector2(innerWidth, infoHeight));
        }

        // 스킬 슬롯 줄이 없으면 패널 아래에 만들고 빌더와 패널(같은 오브젝트)에 연결한다
        private void EnsureSkillBar()
        {
            if (skillBar != null)
                return;

            GameObject go = new GameObject("SkillBar", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build Augmentation Chip Layout");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            SkillSlotBarView bar = go.AddComponent<SkillSlotBarView>();

            // 둘 다 private 직렬화 필드라 SerializedObject로 쓴다 (BuildGrid와 같은 방식)
            SetReference(this, "skillBar", bar);
            AugmentationChipPanel panel = GetComponent<AugmentationChipPanel>();
            if (panel != null)
                SetReference(panel, "skillBar", bar);
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(fieldName).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        #region Grid

        // 그리드 칸을 gridSize x gridSize로 새로 깐다. 기존 자식(이전에 만든 칸 등)은 지운다
        // 칸 배열은 행 우선 (y * gridSize + x) 순서로 채운다. ChipGridView.GetCell이 이 순서를 전제로 읽는다
        private void BuildGrid()
        {
            Transform gridTransform = grid.transform;
            for (int i = gridTransform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(gridTransform.GetChild(i).gameObject);

            Image[] cells = new Image[gridSize * gridSize];
            for (int y = 0; y < gridSize; y++)
                for (int x = 0; x < gridSize; x++)
                    cells[y * gridSize + x] = CreateCell(gridTransform, new Vector2Int(x, y));

            // ChipGridView의 private 직렬화 필드라 SerializedObject로 쓴다 (Undo 기록과 저장 표시도 같이 된다)
            SerializedObject so = new SerializedObject(grid);
            so.FindProperty("gridSize").intValue = gridSize;
            SerializedProperty cellsProperty = so.FindProperty("cells");
            cellsProperty.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
                cellsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            so.ApplyModifiedProperties();
        }

        private Image CreateCell(Transform parent, Vector2Int cell)
        {
            GameObject go = new GameObject($"Cell_{cell.x}_{cell.y}", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build Augmentation Chip Layout");
            go.layer = parent.gameObject.layer;
            RectTransform cellRect = (RectTransform)go.transform;
            cellRect.SetParent(parent, false);
            cellRect.anchorMin = cellRect.anchorMax = cellRect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            cellRect.anchoredPosition = ChipGridMetrics.CellPosition(cell);
            cellRect.sizeDelta = new Vector2(ChipGridMetrics.CellSize, ChipGridMetrics.CellSize);

            Image image = go.AddComponent<Image>();
            image.color = grid.CellColor;
            image.raycastTarget = false;
            return image;
        }

        #endregion

        #region Tray

        // 칩 trayMinChipCells칸 폭이 스크롤 영역 한 줄에 들어가는 최소 트레이 폭
        //   트레이 안쪽 여백 + content 안쪽 여백(ChipTrayView가 칩을 놓을 때 쓰는 ChipSpacing) + 스크롤바
        private float GetTrayMinWidth()
        {
            float contentWidth = ChipGridMetrics.Length(trayMinChipCells) + tray.ChipSpacing * 2f;
            return trayInnerPadding + contentWidth + scrollbarGap + scrollbarWidth + trayInnerPadding;
        }

        // 트레이 크기에 맞춰 필터 토글/정렬 버튼 폭과 스크롤 영역을 다시 잡는다
        private void LayoutTray(Vector2 size)
        {
            ((RectTransform)tray.transform).sizeDelta = size;

            float innerWidth = size.x - trayInnerPadding * 2f;
            LayoutFilterToggles(innerWidth);
            LayoutSortButtons(innerWidth);
            LayoutScrollView(new Vector2(innerWidth, size.y - scrollTop - trayInnerPadding));
        }

        // 필터 토글이 없으면 트레이 아래에 만들고 ChipTrayView에 연결한다. 처음엔 모두 켜 둔다
        //   Filter_공격 (Toggle) > Background (체크 상자) > Checkmark
        //                        > Label
        private void EnsureFilterToggles()
        {
            if (tray.FilterToggleCount > 0)
                return;

            // 정렬 버튼과 같은 글꼴·색으로 맞춘다
            Text sortLabel = tray.GetSortLabelRect(0).GetComponent<Text>();
            Image sortBackground = tray.GetSortButtonRect(0).GetComponent<Image>();

            SerializedObject so = new SerializedObject(tray);
            SerializedProperty togglesProperty = so.FindProperty("filterToggles");
            togglesProperty.arraySize = Filters.Length;
            for (int i = 0; i < Filters.Length; i++)
            {
                RectTransform toggleRect = CreateChild($"Filter_{Filters[i]}", tray.transform);

                Image background = CreateChild("Background", toggleRect).gameObject.AddComponent<Image>();
                background.color = sortBackground.color;

                RectTransform checkRect = CreateChild("Checkmark", background.transform);
                checkRect.anchorMin = Vector2.zero;
                checkRect.anchorMax = Vector2.one;
                checkRect.offsetMin = new Vector2(filterCheckInset, filterCheckInset);
                checkRect.offsetMax = new Vector2(-filterCheckInset, -filterCheckInset);
                Image check = checkRect.gameObject.AddComponent<Image>();
                check.color = sortLabel.color;
                check.raycastTarget = false;

                Text label = CreateChild("Label", toggleRect).gameObject.AddComponent<Text>();
                label.font = sortLabel.font;
                label.fontSize = sortLabel.fontSize;
                label.color = sortLabel.color;
                label.alignment = TextAnchor.MiddleLeft;
                label.text = FilterLabels[i];

                Toggle toggle = toggleRect.gameObject.AddComponent<Toggle>();
                toggle.targetGraphic = background;
                toggle.graphic = check;
                toggle.isOn = true;

                SerializedProperty element = togglesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("filter").enumValueIndex = (int)Filters[i];
                element.FindPropertyRelative("toggle").objectReferenceValue = toggle;
                element.FindPropertyRelative("text").objectReferenceValue = label;
            }

            so.ApplyModifiedProperties();
        }

        // 토글 개수만큼 폭을 똑같이 나누고, 각 토글 안에서 왼쪽에 체크 상자, 오른쪽에 글자를 놓는다
        private void LayoutFilterToggles(float width)
        {
            int count = tray.FilterToggleCount;
            float toggleWidth = (width - filterToggleGap * (count - 1)) / count;
            float boxSize = filterToggleHeight;
            for (int i = 0; i < count; i++)
            {
                Toggle toggle = tray.GetFilterToggle(i);
                SetTopLeft((RectTransform)toggle.transform,
                    new Vector2(trayInnerPadding + i * (toggleWidth + filterToggleGap), -filterToggleTop),
                    new Vector2(toggleWidth, filterToggleHeight));
                SetTopLeft(toggle.targetGraphic.rectTransform, Vector2.zero, new Vector2(boxSize, boxSize));

                float labelX = boxSize + filterLabelGap;
                SetTopLeft(tray.GetFilterLabelRect(i), new Vector2(labelX, 0f), new Vector2(toggleWidth - labelX, filterToggleHeight));
            }
        }

        // 버튼 개수만큼 폭을 똑같이 나눈다
        private void LayoutSortButtons(float width)
        {
            int count = tray.SortButtonCount;
            if (count == 0)
                return;

            float buttonWidth = (width - sortButtonGap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                RectTransform button = tray.GetSortButtonRect(i);
                button.anchoredPosition = new Vector2(trayInnerPadding + i * (buttonWidth + sortButtonGap), -sortButtonTop);
                button.sizeDelta = new Vector2(buttonWidth, sortButtonHeight);
                tray.GetSortLabelRect(i).sizeDelta = button.sizeDelta;
            }
        }

        // ScrollView > Viewport > Content 와 옆의 Scrollbar 크기를 맞춘다
        private void LayoutScrollView(Vector2 size)
        {
            RectTransform content = tray.Content;
            ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>(true);

            RectTransform view = (RectTransform)scrollRect.transform;
            view.anchoredPosition = new Vector2(trayInnerPadding, -scrollTop);
            view.sizeDelta = size;

            float viewportWidth = size.x - scrollbarWidth - scrollbarGap;
            scrollRect.viewport.sizeDelta = new Vector2(viewportWidth, size.y);

            RectTransform bar = (RectTransform)scrollRect.verticalScrollbar.transform;
            bar.anchoredPosition = new Vector2(size.x - scrollbarWidth, 0f);
            bar.sizeDelta = new Vector2(scrollbarWidth, size.y);

            // ChipTrayView가 칩을 줄바꿈할 때 이 폭을 기준으로 삼는다
            content.sizeDelta = new Vector2(viewportWidth, content.sizeDelta.y);
        }

        #endregion

        // 부모 아래에 빈 UI 오브젝트를 만든다. 위치와 크기는 Layout 쪽에서 잡는다
        private RectTransform CreateChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build Augmentation Chip Layout");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // 부모 좌상단 기준으로 배치 (anchor/pivot 모두 좌상단)
        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
#endif
    }
}
