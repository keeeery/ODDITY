using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 장착 그리드. 칸 점유 상태, 배치 가능 여부, 드래그 미리보기를 담당한다.
    /// 점유 상태는 패널을 열 때마다 비우고, 인벤토리의 장착 기록대로 칩을 다시 올려 채운다. 기준은 인벤토리의 장착 기록이다.
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

        private ChipItemView[,] occupied;
        private RectTransform rect;

        public Color CellColor => cellColor; // 빌더가 칸을 만들 때 기본 색으로 쓴다

        // 패널을 열 때마다 호출해서 점유 상태를 비운다
        // 패널이 꺼져 있어도 호출할 수 있도록 Awake에 기대지 않는다
        public void Clear()
        {
            rect = (RectTransform)transform;
            occupied = new ChipItemView[gridSize, gridSize];

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

        // 칩의 지금 방향 그대로 origin에 놓을 수 있는지. 칸 밖으로 나가거나 다른 칩과 겹치면 안 된다
        public bool CanPlace(ChipItemView chip, Vector2Int origin)
        {
            foreach (Vector2Int offset in chip.Cells)
            {
                Vector2Int p = origin + offset;
                if (IsInside(p) == false)
                    return false;
                if (occupied[p.x, p.y] != null && occupied[p.x, p.y] != chip)
                    return false;
            }

            return true;
        }

        // 위쪽 줄부터, 같은 줄에서는 왼쪽부터 훑어서 칩을 놓을 수 있는 첫 자리를 찾는다
        public bool TryFindOrigin(ChipItemView chip, out Vector2Int origin)
        {
            for (int y = 0; y <= gridSize - chip.Height; y++)
                for (int x = 0; x <= gridSize - chip.Width; x++)
                {
                    origin = new Vector2Int(x, y);
                    if (CanPlace(chip, origin))
                        return true;
                }

            origin = default;
            return false;
        }

        public void Place(ChipItemView chip, Vector2Int origin)
        {
            foreach (Vector2Int offset in chip.Cells)
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
        }

        // blocked: 자리와 상관없이 장착할 수 없는 칩 (스킬 슬롯이 가득 찬 스킬 칩). 놓을 수 없는 색으로 보여준다
        public void ShowPreview(ChipItemView chip, bool blocked)
        {
            ClearPreview();

            Vector2Int origin = GetHoveredOrigin(chip);
            Color color = blocked == false && CanPlace(chip, origin) ? validColor : invalidColor;
            foreach (Vector2Int offset in chip.Cells)
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
