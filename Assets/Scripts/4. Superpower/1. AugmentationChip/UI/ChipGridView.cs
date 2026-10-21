using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 장착 그리드. 칸 점유 상태, 배치 가능 여부, 드래그 미리보기를 담당한다.
    /// </summary>
    public class ChipGridView : MonoBehaviour
    {
        [SerializeField] private int gridSize = 4;
        [SerializeField] private Image[] cells; // 행 우선 (y * gridSize + x)
        [SerializeField] private Color cellColor = new(0.20f, 0.22f, 0.27f, 1f);
        [SerializeField] private Color validColor = new(0.30f, 0.75f, 0.45f, 1f);
        [SerializeField] private Color invalidColor = new(0.85f, 0.30f, 0.30f, 1f);

        private ChipItemView[,] occupied;
        private RectTransform rect;

        private void Awake()
        {
            occupied = new ChipItemView[gridSize, gridSize];
            rect = (RectTransform)transform;
        }

        public Vector2Int GetHoveredOrigin(ChipItemView chip)
        {
            // 칩 좌상단 칸의 중심이 어느 그리드 칸 위에 있는지로 판단
            float half = ChipGridMetrics.CellSize * 0.5f;
            Vector3 topLeftCenter = chip.Rect.TransformPoint(new Vector3(half, -half, 0f));
            Vector2 local = rect.InverseTransformPoint(topLeftCenter);
            return new Vector2Int(Mathf.FloorToInt(local.x / ChipGridMetrics.Step), Mathf.FloorToInt(-local.y / ChipGridMetrics.Step));
        }

        public bool CanPlace(ChipItemView chip, Vector2Int origin)
        {
            foreach (Vector2Int offset in chip.Chip.Cells)
            {
                Vector2Int p = origin + offset;
                if (IsInside(p) == false)
                    return false;
                if (occupied[p.x, p.y] != null && occupied[p.x, p.y] != chip)
                    return false;
            }

            return true;
        }

        public void Place(ChipItemView chip, Vector2Int origin)
        {
            chip.GridOrigin = origin;
            foreach (Vector2Int offset in chip.Chip.Cells)
                occupied[origin.x + offset.x, origin.y + offset.y] = chip;

            chip.Rect.SetParent(rect, false);
            chip.Rect.anchoredPosition = ChipGridMetrics.CellPosition(origin);
        }

        public void Remove(ChipItemView chip)
        {
            for (int y = 0; y < gridSize; y++)
                for (int x = 0; x < gridSize; x++)
                    if (occupied[x, y] == chip)
                        occupied[x, y] = null;

            chip.GridOrigin = null;
        }

        public void ShowPreview(ChipItemView chip)
        {
            ClearPreview();

            Vector2Int origin = GetHoveredOrigin(chip);
            Color color = CanPlace(chip, origin) ? validColor : invalidColor;
            foreach (Vector2Int offset in chip.Chip.Cells)
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
