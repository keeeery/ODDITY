using UnityEngine;
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
    public class AugmentationChipLayout : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(1)] private int gridSize = 4;

        [Header("Tray")]
        [SerializeField, Min(1)] private int trayMinChipCells = 4; // 트레이 한 줄에 최소 들어가야 하는 칩 폭(칸)
        [SerializeField] private float trayMinHeight = 372f;       // 그리드가 이보다 작아도 트레이는 이 높이를 유지한다

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
                Debug.LogWarning("[AugmentationChipLayout] 프리팹을 열어서(Prefab Mode) Build Layout을 실행해 주세요.", this);
                return;
            }

            // 패널 아래 RectTransform 변경을 한 번에 기록해 Ctrl+Z로 되돌릴 수 있게 한다
            Undo.RegisterFullObjectHierarchyUndo(gameObject, "Build Augmentation Chip Layout");

            grid.Build(gridSize);

            float gridLength = ChipGridMetrics.Length(gridSize);
            Vector2 traySize = new Vector2(tray.GetMinWidth(trayMinChipCells), Mathf.Max(gridLength, trayMinHeight));
            tray.Resize(traySize);

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
