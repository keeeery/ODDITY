using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 트레이/그리드 위에 놓이는 칩 하나. 칩 모양(단위 칸, 외곽선, 아이콘)을 그리고 포인터 입력을 이벤트로 넘긴다.
    /// 어디에 놓을지는 AugmentationChipPanel이 결정하고, 장착 기록은 Owned(OwnedChip)에 있다.
    /// 이 컴포넌트는 지금 화면에 그려진 방향(Cells/Rotation)만 들고 있다.
    /// </summary>
    // 휠(IScrollHandler)은 구현하지 않아야 부모 ScrollRect로 그대로 전달된다
    public class ChipItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // 런타임에 AddComponent로 붙이므로 인스펙터 값 대신 상수로 둔다
        // 외곽선·NEW 표시는 HUD 글자색과 그로기 바 금색을 쓴다
        private static readonly Color OutlineColor = new Color(0.93f, 0.91f, 0.87f, 0.85f);
        private const float OutlineThickness = 6f;
        private static readonly Color NewBadgeColor = new Color(0.7686275f, 0.5764706f, 0.2901961f, 1f);
        private static readonly Color DarkTextColor = new Color(0.06f, 0.05f, 0.04f, 1f);
        private static readonly Vector2 NewBadgeSize = new Vector2(108f, 48f);
        private static readonly Vector2 NewBadgeOffset = new Vector2(12f, -12f); // 첫 칸 좌상단에서 외곽선 안쪽으로
        // 아이콘을 칸(CellSize)보다 작게 가운데에 둔다. 칸을 꽉 채우면 아이콘 구석의 등급 다이아가 외곽선에 닿는다
        private const float IconSize = 172f;

        private readonly List<RectTransform> units = new();
        private RectTransform outlineRoot;
        private RectTransform icon;
        private RectTransform newBadge; // 아직 확인하지 않은 칩에만 있다. 확인하면 지우고 null

        public OwnedChip Owned { get; private set; }
        public RectTransform Rect { get; private set; }

        // 지금 그려진 방향의 모양. 좌상단 (0,0), x는 오른쪽, y는 아래로 증가
        public Vector2Int[] Cells { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Rotation { get; private set; } // 시계 방향 90도 회전 횟수 (0~3)

        public event Action<ChipItemView, PointerEventData> PointerEntered;
        public event Action<ChipItemView, PointerEventData> PointerMoved;
        public event Action<ChipItemView> PointerExited;
        public event Action<ChipItemView> RightClicked;
        public event Action<ChipItemView, PointerEventData> DragBegan;
        public event Action<ChipItemView, PointerEventData> Dragged;
        public event Action<ChipItemView, PointerEventData> DragEnded;

        // 보유 칩으로 칩 UI를 만든다. 칩 목록은 런타임에 정해지므로 생성도 런타임에 한다
        public static ChipItemView Create(OwnedChip owned, Transform parent, Font font)
        {
            GameObject go = new GameObject($"Chip_{owned.Def.ID}", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            rect.sizeDelta = Vector2.zero;

            ChipItemView view = go.AddComponent<ChipItemView>();
            view.Owned = owned;
            view.Rect = rect;
            view.Build(font);
            return view;
        }

        private void Build(Font font)
        {
            AugmentationChipDef def = Owned.Def;

            // 회전하면 좌표가 바뀌므로 보유 칩의 배열을 공유하지 않고 복사해서 쓴다
            Cells = (Vector2Int[])Owned.Cells.Clone();
            RefreshSize();

            // Cells 좌표마다 단위 칩 스프라이트를 한 칸씩 깔아서 모양을 만든다
            for (int i = 0; i < Cells.Length; i++)
            {
                RectTransform unit = CreateRect($"Unit_{i}", transform);
                unit.sizeDelta = new Vector2(ChipGridMetrics.CellSize, ChipGridMetrics.CellSize);

                Image image = unit.gameObject.AddComponent<Image>();
                image.sprite = def.UnitSprite;

                units.Add(unit);
            }

            // 첫 칸에 칩 아이콘을 표시
            icon = CreateRect("Icon", transform);
            icon.sizeDelta = new Vector2(IconSize, IconSize);

            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = def.IconSprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            if (Owned.IsNew)
                BuildNewBadge(font);

            RefreshShape();
        }

        // 첫 칸 좌상단에 노란 바탕의 "NEW" 표시를 붙인다
        private void BuildNewBadge(Font font)
        {
            newBadge = CreateRect("NewBadge", transform);
            newBadge.sizeDelta = NewBadgeSize;

            Image background = newBadge.gameObject.AddComponent<Image>();
            background.color = NewBadgeColor;
            background.raycastTarget = false;

            RectTransform textRect = CreateRect("Text", newBadge);
            textRect.sizeDelta = NewBadgeSize;

            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = DarkTextColor;
            text.text = "NEW";
            text.raycastTarget = false;
        }

        // 마우스를 올리면 확인한 것으로 보고 NEW 표시를 지운다. 집거나 우클릭하기 전에도 항상 먼저 마우스가 올라간다
        private void MarkSeen()
        {
            if (Owned.IsNew == false)
                return;

            Owned.IsNew = false;
            Destroy(newBadge.gameObject);
            newBadge = null;
        }

        // 시계 방향 90도 회전. (x, y) -> (Height-1-y, x)
        // Cells의 순서는 그대로 두므로 i번째 단위 칩은 회전 후에도 같은 칸을 따라간다
        public void RotateClockwise()
        {
            Vector2Int[] rotated = new Vector2Int[Cells.Length];
            for (int i = 0; i < Cells.Length; i++)
                rotated[i] = new Vector2Int(Height - 1 - Cells[i].y, Cells[i].x);

            Cells = rotated;
            Rotation = (Rotation + 1) % 4;
            RefreshSize();
            RefreshShape();
        }

        // 장착 기록에 남은 방향으로 맞춘다
        public void SetRotation(int rotation)
        {
            int target = ((rotation % 4) + 4) % 4;
            while (Rotation != target)
                RotateClockwise();
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

        // i번째 단위 칩을 Cells[i] 위치로 옮기고 외곽선을 다시 그린다
        // 아이콘은 Cells[0] 칸을 따라가고, 새로 그린 외곽선에 가리지 않게 맨 위로 올린다
        private void RefreshShape()
        {
            for (int i = 0; i < units.Count; i++)
                units[i].anchoredPosition = ChipGridMetrics.CellPosition(Cells[i]);

            BuildOutline();

            Vector2 firstCell = ChipGridMetrics.CellPosition(Cells[0]);
            float iconInset = (ChipGridMetrics.CellSize - IconSize) * 0.5f;
            icon.anchoredPosition = firstCell + new Vector2(iconInset, -iconInset);
            icon.SetAsLastSibling();

            if (newBadge != null)
            {
                newBadge.anchoredPosition = firstCell + NewBadgeOffset;
                newBadge.SetAsLastSibling();
            }
        }

        #region Input

        public void OnPointerEnter(PointerEventData eventData)
        {
            MarkSeen();
            PointerEntered?.Invoke(this, eventData);
        }

        public void OnPointerMove(PointerEventData eventData) => PointerMoved?.Invoke(this, eventData);

        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke(this);
        }

        // 드래그는 왼쪽 버튼만 받는다. 오른쪽 버튼은 빠른 장착/해제(클릭)에 쓴다
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                DragBegan?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                DragEnded?.Invoke(this, eventData);
        }

        #endregion

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
            Vector2 thickness = new Vector2(Mathf.Min(OutlineThickness, size.x), Mathf.Min(OutlineThickness, size.y));

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
        private static float FinePosition(int index) => (index / 2) * ChipGridMetrics.Step + (index % 2 == 1 ? ChipGridMetrics.CellSize : 0f);
        private static float FineSize(int index) => index % 2 == 1 ? ChipGridMetrics.CellGap : ChipGridMetrics.CellSize;

        private static void CreateOutlinePiece(RectTransform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect("Line", parent);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = OutlineColor;
            image.raycastTarget = false;
        }

        #endregion

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            return rect;
        }
    }
}
