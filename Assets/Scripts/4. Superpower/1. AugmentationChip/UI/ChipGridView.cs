using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 장착 그리드 그리기. 칩을 칸 위치에 올려 그리고, 드래그 미리보기 색을 칠한다.
    /// 칸 점유와 배치 가능 여부는 AugmentationChipEquipment가 판단한다. 이 클래스는 결과를 받아 그리기만 한다.
    /// 칸은 에디터에서 AugmentationChipLayoutBuilder의 "Build Layout"으로 만들어 프리팹에 저장해 두고, 런타임에는 읽기만 한다.
    /// </summary>
    public class ChipGridView : MonoBehaviour
    {
        [SerializeField] private Color cellColor = new(0.20f, 0.22f, 0.27f, 1f);
        [SerializeField] private Color validColor = new(0.30f, 0.75f, 0.45f, 1f);
        [SerializeField] private Color invalidColor = new(0.85f, 0.30f, 0.30f, 1f);

        // AugmentationChipLayoutBuilder가 SerializedObject로 채운다(필드 이름으로 찾으므로 이름을 바꾸면 빌더도 같이 고친다). 직접 고치지 않는다
        [SerializeField, HideInInspector] private int gridSize;
        [SerializeField, HideInInspector] private Image[] cells; // 행 우선 (y * gridSize + x)

        private RectTransform rect;

        public int GridSize => gridSize;   // 인스톨러가 AugmentationChipEquipment의 보드 크기를 맞출 때 읽는다
        public Color CellColor => cellColor; // 빌더가 칸을 만들 때 기본 색으로 쓴다

        // 칩을 올리기 전에 한 번 호출한다
        // 패널이 꺼져 있어도 호출할 수 있도록 Awake에 기대지 않는다
        public void Init()
        {
            rect = (RectTransform)transform;

            if (cells == null || cells.Length != gridSize * gridSize)
                Debug.LogError("[ChipGridView] 그리드 칸이 없습니다. 프리팹에서 AugmentationChipLayoutBuilder → Build Layout을 실행해 주세요.", this);
        }

        // 칩 좌상단 칸의 중심이 어느 그리드 칸 위에 있는지
        public Vector2Int GetHoveredOrigin(ChipItemView chip)
        {
            float half = ChipGridMetrics.CellSize * 0.5f;
            Vector3 topLeftCenter = chip.Rect.TransformPoint(new Vector3(half, -half, 0f));
            Vector2 local = rect.InverseTransformPoint(topLeftCenter);
            return new Vector2Int(Mathf.FloorToInt(local.x / ChipGridMetrics.Step), Mathf.FloorToInt(-local.y / ChipGridMetrics.Step));
        }

        // 장착이 확정된 칩을 origin 칸 위치에 그린다
        public void Place(ChipItemView chip, Vector2Int origin)
        {
            chip.Rect.SetParent(rect, false);
            chip.Rect.anchoredPosition = ChipGridMetrics.CellPosition(origin);
        }

        // origin에 cells 모양을 놓았을 때 걸치는 칸을 칠한다. 놓을 수 있는지는 호출하는 쪽이 판단해서 넘긴다
        public void ShowPreview(Vector2Int origin, Vector2Int[] shape, bool valid)
        {
            ClearPreview();

            Color color = valid ? validColor : invalidColor;
            foreach (Vector2Int offset in shape)
            {
                Vector2Int p = origin + offset;
                if (IsInside(p))
                    GetCell(p).color = color;
            }
        }

        public void ClearPreview()
        {
            foreach (Image cell in cells)
                cell.color = cellColor;
        }

        private bool IsInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize && p.y < gridSize;
        private Image GetCell(Vector2Int p) => cells[p.y * gridSize + p.x];
    }
}
