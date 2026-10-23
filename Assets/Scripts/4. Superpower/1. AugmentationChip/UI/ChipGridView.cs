using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 장착 그리드. 칸 점유 상태, 배치 가능 여부, 드래그 미리보기를 담당한다.
    /// 칸은 에디터에서 AugmentationChipLayout의 "Build Layout"으로 만들어 프리팹에 저장해 두고, 런타임에는 Init으로 읽기만 한다.
    /// </summary>
    public class ChipGridView : MonoBehaviour
    {
        [SerializeField] private Color cellColor = new(0.20f, 0.22f, 0.27f, 1f);
        [SerializeField] private Color validColor = new(0.30f, 0.75f, 0.45f, 1f);
        [SerializeField] private Color invalidColor = new(0.85f, 0.30f, 0.30f, 1f);

        // Build Layout이 채운다. 직접 고치지 않는다
        [SerializeField, HideInInspector] private int gridSize;
        [SerializeField, HideInInspector] private Image[] cells; // 행 우선 (y * gridSize + x)

        private ChipItemView[,] occupied;
        private RectTransform rect;

        public int GridSize => gridSize;

        // 칩을 올리기 전에 한 번 호출한다
        // 패널이 꺼져 있어도 호출할 수 있도록 Awake에 기대지 않는다
        public void Init()
        {
            rect = (RectTransform)transform;
            occupied = new ChipItemView[gridSize, gridSize];

            if (cells == null || cells.Length != gridSize * gridSize)
                Debug.LogError("[ChipGridView] 그리드 칸이 없습니다. 프리팹에서 AugmentationChipLayout → Build Layout을 실행해 주세요.", this);
        }

#if UNITY_EDITOR
        // 칸을 size x size로 새로 깐다. 기존 자식(이전에 만든 칸 등)은 지운다
        // 호출하는 쪽(AugmentationChipLayout)이 Undo 기록을 시작해 둔다
        public void Build(int size)
        {
            rect = (RectTransform)transform;
            gridSize = size;

            for (int i = rect.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(rect.GetChild(i).gameObject);

            cells = new Image[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    cells[y * size + x] = CreateCell(new Vector2Int(x, y));

            float length = ChipGridMetrics.Length(size);
            rect.sizeDelta = new Vector2(length, length);
            EditorUtility.SetDirty(this);
        }

        private Image CreateCell(Vector2Int cell)
        {
            GameObject go = new GameObject($"Cell_{cell.x}_{cell.y}", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build Augmentation Chip Layout");
            go.layer = gameObject.layer;
            RectTransform cellRect = (RectTransform)go.transform;
            cellRect.SetParent(rect, false);
            cellRect.anchorMin = cellRect.anchorMax = cellRect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            cellRect.anchoredPosition = ChipGridMetrics.CellPosition(cell);
            cellRect.sizeDelta = new Vector2(ChipGridMetrics.CellSize, ChipGridMetrics.CellSize);

            Image image = go.AddComponent<Image>();
            image.color = cellColor;
            image.raycastTarget = false;
            return image;
        }
#endif

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

        // 위쪽 줄부터, 같은 줄에서는 왼쪽부터 훑어서 칩을 놓을 수 있는 첫 자리를 찾는다
        public bool TryFindOrigin(ChipItemView chip, out Vector2Int origin)
        {
            for (int y = 0; y <= gridSize - chip.Chip.Height; y++)
                for (int x = 0; x <= gridSize - chip.Chip.Width; x++)
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
