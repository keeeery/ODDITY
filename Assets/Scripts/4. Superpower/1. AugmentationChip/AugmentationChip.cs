using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    public class AugmentationChip : MonoBehaviour
    {
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private float outlineThickness = 3f;

        private AugmentationChipDef refDef;
        private List<RectTransform> units = new List<RectTransform>();
        private RectTransform outlineRoot;

        // UI에서 칩 정보를 읽어갈 때 사용한다
        public int ID => refDef.ID;
        public string Name => refDef.Name;
        public string Description => refDef.Description;
        public StatType StatType => refDef.StatType;
        public int Value => refDef.Value;

        public Vector2Int[] Cells { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        // 장착 시 StatHandler에 넘겨줄 효과
        public IStat Effect { get; private set; }

        public void Init(AugmentationChipDef def)
        {
            // 매개변수로 전달된 AugmentationChipDef 객체(레퍼런스 타입)를 캐싱한다
            refDef = def;
            Effect = new AugmentationChipStat(def.StatType, def.Value);

            // 회전하면 좌표가 바뀌므로 Def의 배열을 공유하지 않고 복사해서 쓴다
            Cells = (Vector2Int[])refDef.Cells.Clone();
            RefreshSize();

            // Cells 좌표마다 단위 칩 스프라이트를 한 칸씩 깔아서 모양을 만든다
            for (int i = 0; i < Cells.Length; i++)
            {
                GameObject unit = new GameObject($"Unit_{i}", typeof(RectTransform));
                RectTransform rect = (RectTransform)unit.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // 좌상단 기준
                rect.sizeDelta = new Vector2(ChipGridMetrics.CellSize, ChipGridMetrics.CellSize);

                Image image = unit.AddComponent<Image>();
                image.sprite = refDef.UnitSprite;

                units.Add(rect);
            }

            RefreshUnits();
            BuildOutline();
        }

        // i번째 단위 칩을 Cells[i] 위치로 옮긴다
        private void RefreshUnits()
        {
            float step = ChipGridMetrics.Step;
            for (int i = 0; i < units.Count; i++)
                units[i].anchoredPosition = new Vector2(Cells[i].x * step, -Cells[i].y * step);
        }

        // 시계 방향 90도 회전. (x, y) -> (Height-1-y, x)
        // Cells의 순서는 그대로 두므로 i번째 단위 칩은 회전 후에도 같은 칸을 따라간다
        public void RotateClockwise()
        {
            Vector2Int[] rotated = new Vector2Int[Cells.Length];
            for (int i = 0; i < Cells.Length; i++)
                rotated[i] = new Vector2Int(Height - 1 - Cells[i].y, Cells[i].x);

            Cells = rotated;
            RefreshSize();
            RefreshUnits();
            BuildOutline();
        }

        // 칩이 차지하는 가로/세로 칸 수를 실제 Cells 범위로 계산한다
        private void RefreshSize()
        {
            Width = 0;
            Height = 0;
            foreach (Vector2Int cell in Cells)
            {
                Width = Mathf.Max(Width, cell.x + 1);
                Height = Mathf.Max(Height, cell.y + 1);
            }
        }

        #region Outline

        // 칸(짝수 인덱스)과 칸 사이 틈(홀수 인덱스)을 한 격자로 펼쳐서
        // 칩 전체를 하나의 모양으로 보고, 그 바깥 경계에만 선을 긋는다. 틈 자체는 비워둔다
        //   칸 W개 -> 2W-1칸 (칸, 틈, 칸, 틈, 칸)
        private void BuildOutline()
        {
            bool[,] filled = BuildFilledGrid();
            RectTransform outline = ResetOutlineRoot();

            for (int fy = 0; fy < filled.GetLength(1); fy++)
                for (int fx = 0; fx < filled.GetLength(0); fx++)
                    DrawFineCellOutline(outline, filled, fx, fy);
        }

        private bool[,] BuildFilledGrid()
        {
            HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>(Cells);

            int fineWidth = Width * 2 - 1;
            int fineHeight = Height * 2 - 1;
            bool[,] filled = new bool[fineWidth, fineHeight];
            for (int fy = 0; fy < fineHeight; fy++)
                for (int fx = 0; fx < fineWidth; fx++)
                    filled[fx, fy] = IsFilled(cellSet, fx, fy);

            return filled;
        }

        // 회전할 때마다 모양이 바뀌므로 기존 외곽선을 버리고 새로 그린다
        private RectTransform ResetOutlineRoot()
        {
            if (outlineRoot != null)
                Destroy(outlineRoot.gameObject);

            outlineRoot = CreateRect("Outline", transform);
            outlineRoot.sizeDelta = Vector2.zero;
            return outlineRoot;
        }

        private void DrawFineCellOutline(RectTransform outline, bool[,] filled, int fx, int fy)
        {
            if (filled[fx, fy] == false)
                return;

            Vector2 pos = new Vector2(FinePosition(fx), FinePosition(fy));
            Vector2 size = new Vector2(FineSize(fx), FineSize(fy));
            Vector2 thickness = new Vector2(Mathf.Min(outlineThickness, size.x), Mathf.Min(outlineThickness, size.y));

            DrawEdges(outline, filled, fx, fy, pos, size, thickness);
            DrawInnerCorners(outline, filled, fx, fy, pos, size, thickness);
        }

        // 이웃이 비어있는 변에만 선을 긋는다 (선은 모양 안쪽에 그린다)
        private void DrawEdges(RectTransform outline, bool[,] filled, int fx, int fy, Vector2 pos, Vector2 size, Vector2 t)
        {
            if (IsFilledAt(filled, fx, fy - 1) == false)
                CreateOutlinePiece(outline, pos, new Vector2(size.x, t.y));
            if (IsFilledAt(filled, fx, fy + 1) == false)
                CreateOutlinePiece(outline, pos + new Vector2(0f, size.y - t.y), new Vector2(size.x, t.y));
            if (IsFilledAt(filled, fx - 1, fy) == false)
                CreateOutlinePiece(outline, pos, new Vector2(t.x, size.y));
            if (IsFilledAt(filled, fx + 1, fy) == false)
                CreateOutlinePiece(outline, pos + new Vector2(size.x - t.x, 0f), new Vector2(t.x, size.y));
        }

        // 안쪽으로 꺾이는 모서리는 양쪽 이웃의 선이 맞닿지 않아 빈 점이 생기므로 메워준다
        private void DrawInnerCorners(RectTransform outline, bool[,] filled, int fx, int fy, Vector2 pos, Vector2 size, Vector2 t)
        {
            for (int dy = -1; dy <= 1; dy += 2)
            {
                for (int dx = -1; dx <= 1; dx += 2)
                {
                    if (IsInnerCorner(filled, fx, fy, dx, dy) == false)
                        continue;

                    Vector2 corner = pos + new Vector2(dx < 0 ? 0f : size.x - t.x, dy < 0 ? 0f : size.y - t.y);
                    CreateOutlinePiece(outline, corner, t);
                }
            }
        }

        // 가로/세로 이웃은 채워져 있는데 대각선 이웃만 비어있으면 안쪽 모서리
        private static bool IsInnerCorner(bool[,] filled, int fx, int fy, int dx, int dy)
        {
            return IsFilledAt(filled, fx + dx, fy) && IsFilledAt(filled, fx, fy + dy) &&
                   IsFilledAt(filled, fx + dx, fy + dy) == false;
        }

        // 펼친 격자의 한 칸이 칩 모양에 포함되는지
        //   칸: 해당 칸이 있으면 / 틈: 양옆(위아래) 칸이 모두 있으면 / 교차점: 주변 4칸이 모두 있으면
        //   (교차점을 3칸일 때도 채우면 안쪽 모서리에 틈 폭만큼 계단이 튀어나온다)
        private static bool IsFilled(HashSet<Vector2Int> cellSet, int fx, int fy)
        {
            int x = fx / 2;
            int y = fy / 2;
            bool gapX = fx % 2 == 1;
            bool gapY = fy % 2 == 1;

            if (gapX == false && gapY == false)
                return cellSet.Contains(new Vector2Int(x, y));
            if (gapX && gapY == false)
                return cellSet.Contains(new Vector2Int(x, y)) && cellSet.Contains(new Vector2Int(x + 1, y));
            if (gapX == false && gapY)
                return cellSet.Contains(new Vector2Int(x, y)) && cellSet.Contains(new Vector2Int(x, y + 1));

            int count = 0;
            if (cellSet.Contains(new Vector2Int(x, y))) count++;
            if (cellSet.Contains(new Vector2Int(x + 1, y))) count++;
            if (cellSet.Contains(new Vector2Int(x, y + 1))) count++;
            if (cellSet.Contains(new Vector2Int(x + 1, y + 1))) count++;
            return count == 4;
        }

        private static bool IsFilledAt(bool[,] filled, int fx, int fy)
        {
            if (fx < 0 || fy < 0 || fx >= filled.GetLength(0) || fy >= filled.GetLength(1))
                return false;
            return filled[fx, fy];
        }

        // 펼친 격자 인덱스 -> 칩 좌상단 기준 거리 / 크기
        private float FinePosition(int index) => (index / 2) * ChipGridMetrics.Step + (index % 2 == 1 ? ChipGridMetrics.CellSize : 0f);
        private float FineSize(int index) => index % 2 == 1 ? ChipGridMetrics.CellGap : ChipGridMetrics.CellSize;

        private void CreateOutlinePiece(RectTransform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect("Line", parent);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = outlineColor;
            image.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            return rect;
        }

        #endregion
    }
}
