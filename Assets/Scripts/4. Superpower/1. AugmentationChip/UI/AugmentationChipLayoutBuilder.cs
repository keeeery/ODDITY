using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 패널 전체 배치. 그리드 크기에 맞춰 그리드 칸을 만들고, 그리드/트레이/텍스트 위치와 패널 크기를 정한다.
    /// 에디터에서만 동작한다. 값을 바꾼 뒤 컴포넌트 우클릭(⋮) → "Build Layout"을 누르면 프리팹에 결과가 저장되고,
    /// 런타임에는 저장된 칸과 위치를 그대로 쓴다.
    /// 패널은 앵커·피벗이 중앙이라 전체 크기가 바뀌어도 중심은 화면 중앙에 남는다.
    ///
    ///   ┌ padding ─────────────────────────────────────┐
    ///   │ Title                                         │
    ///   │ ┌ Grid (N x N) ┐ spacing ┌ Tray ───────────┐ │ ← headerHeight
    ///   │ │              │         │ 정렬 버튼        │ │
    ///   │ └──────────────┘         │ 보유 칩          │ │
    ///   │                          └─────────────────┘ │
    ///   │ Info                                          │
    ///   └───────────────────────────────────────────────┘
    /// </summary>
    public class AugmentationChipLayoutBuilder : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(1)] private int gridSize = 4;

        [Header("Tray")]
        [SerializeField, Min(1)] private int trayMinChipCells = 4; // 트레이 한 줄에 최소 들어가야 하는 칩 폭(칸)
        // 트레이 자체 폭 패딩
        // trayMinChipCells는 칸 사이 틈(CellGap)으로 폭을 재지만, 트레이에서는 칩끼리 ChipSpacing만큼 떨어져 놓인다
        // 예) 폭 3짜리 칩 두 개를 한 줄에 놓으려면 6칸 폭에 (ChipSpacing - CellGap)만큼 더 필요하다
        [SerializeField] private float trayPadding = 8;
        [SerializeField] private float trayMinHeight = 372f;       // 그리드가 이보다 작아도 트레이는 이 높이를 유지한다
        [SerializeField] private float trayInnerPadding = 12f;     // 트레이 가장자리 ~ 정렬 버튼/스크롤 영역
        [SerializeField] private float sortButtonTop = 36f;        // 트레이 위쪽 ~ 정렬 버튼 (위는 "보유 칩" 제목)
        [SerializeField] private float sortButtonHeight = 24f;
        [SerializeField] private float sortButtonGap = 6f;
        [SerializeField] private float scrollTop = 68f;            // 트레이 위쪽 ~ 스크롤 영역
        [SerializeField] private float scrollbarWidth = 12f;
        [SerializeField] private float scrollbarGap = 4f;

        [Header("Spacing")]
        [SerializeField] private float padding = 24f;
        [SerializeField] private float spacing = 20f;      // 그리드와 트레이 사이
        [SerializeField] private float headerHeight = 64f; // 패널 위쪽 ~ 그리드/트레이 위쪽 (제목 영역)
        [SerializeField] private float titleTop = 16f;
        [SerializeField] private float titleHeight = 32f;
        [SerializeField] private float infoGap = 10f;      // 본문 아래 ~ 정보 텍스트
        [SerializeField] private float infoHeight = 64f;

        [Header("References")]
        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform info;

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

            grid.Build(gridSize);

            float gridLength = ChipGridMetrics.Length(gridSize);
            Vector2 traySize = new Vector2(GetTrayMinWidth() + trayPadding, Mathf.Max(gridLength, trayMinHeight));
            LayoutTray(traySize);

            float width = padding + gridLength + spacing + traySize.x + padding;
            float height = headerHeight + traySize.y + infoGap + infoHeight + padding;

            RectTransform root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(width, height);

            float innerWidth = width - padding * 2f;
            SetTopLeft(title, new Vector2(padding, -titleTop), new Vector2(innerWidth, titleHeight));
            SetTopLeft((RectTransform)grid.transform, new Vector2(padding, -headerHeight), new Vector2(gridLength, gridLength));
            SetTopLeft((RectTransform)tray.transform, new Vector2(padding + gridLength + spacing, -headerHeight), traySize);
            SetTopLeft(info, new Vector2(padding, -(headerHeight + traySize.y + infoGap)), new Vector2(innerWidth, infoHeight));
        }

        #region Tray

        // 칩 trayMinChipCells칸 폭이 스크롤 영역 한 줄에 들어가는 최소 트레이 폭
        //   트레이 안쪽 여백 + content 안쪽 여백(ChipTrayView가 칩을 놓을 때 쓰는 ChipSpacing) + 스크롤바
        private float GetTrayMinWidth()
        {
            float contentWidth = ChipGridMetrics.Length(trayMinChipCells) + tray.ChipSpacing * 2f;
            return trayInnerPadding + contentWidth + scrollbarGap + scrollbarWidth + trayInnerPadding;
        }

        // 트레이 크기에 맞춰 정렬 버튼 폭과 스크롤 영역을 다시 잡는다
        private void LayoutTray(Vector2 size)
        {
            ((RectTransform)tray.transform).sizeDelta = size;

            float innerWidth = size.x - trayInnerPadding * 2f;
            LayoutSortButtons(innerWidth);
            LayoutScrollView(new Vector2(innerWidth, size.y - scrollTop - trayInnerPadding));
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
