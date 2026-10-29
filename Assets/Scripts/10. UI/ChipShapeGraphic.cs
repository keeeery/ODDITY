using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 칩 모양(칸 좌표)을 작은 각진 칸들로 그린다. 좌상단 (0,0), x는 오른쪽, y는 아래로 증가하는 좌표를 받는다.
    // 모양에 꼭 맞게 잘라 영역 가운데에 놓고, 영역에 들어가도록 칸 크기를 줄인다(최대 maxCellSize).
    // 칸 스프라이트를 넣으면 인벤토리처럼 칩 색으로 칠하고, 없으면 HUD 글자색으로 칠한다. 테두리는 체력바와 같다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ChipShapeGraphic : MaskableGraphic
    {
        [Header("Layout")]
        [SerializeField, Min(1f)] private float maxCellSize = 10f;
        [SerializeField, Min(0f)] private float cellGap = 2f;

        [Header("Colors")]
        [SerializeField] private Color cellColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        [SerializeField] private Color borderColor = new(0.1176471f, 0.1058824f, 0.09019608f, 1f);

        [Header("Style")]
        [SerializeField, Min(0f)] private float borderWidth = 1f;
        [SerializeField, Range(0f, 3f)] private float antiAliasPixels = 1f;

        private readonly VectorShape shape = new();
        private Vector2Int[] cells;
        private Sprite cellSprite;

        public Vector2Int[] Cells
        {
            get => cells;
            set
            {
                cells = value;
                SetVerticesDirty();
            }
        }

        // 단색 칩 칸 스프라이트(AugmentationChipDef.UnitSprite), 가운데 색 한 점으로 칸을 칠한다.
        public Sprite CellSprite
        {
            get => cellSprite;
            set
            {
                if (cellSprite == value) return;
                cellSprite = value;
                SetMaterialDirty();
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture => cellSprite != null ? cellSprite.texture : s_WhiteTexture;

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (cells == null || cells.Length == 0 || rect.width <= 0f || rect.height <= 0f) return;

            Vector2Int min = cells[0];
            Vector2Int max = cells[0];
            foreach (Vector2Int cell in cells)
            {
                min = Vector2Int.Min(min, cell);
                max = Vector2Int.Max(max, cell);
            }
            int columns = max.x - min.x + 1;
            int rows = max.y - min.y + 1;

            float step = Mathf.Min(maxCellSize + cellGap,
                (rect.width + cellGap) / columns, (rect.height + cellGap) / rows);
            float size = step - cellGap;
            if (size <= 0f) return;

            // 모양 전체를 영역 가운데에 놓고, 아래로 증가하는 y를 위에서부터 내려 그린다.
            Vector2 topLeft = rect.center + new Vector2(-(columns * step - cellGap), rows * step - cellGap) * 0.5f;
            float antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);
            Vector2 uv = GetSpriteCenterUV();
            Color fill = cellSprite != null ? Color.white : cellColor;

            foreach (Vector2Int cell in cells)
            {
                Vector2Int local = cell - min;
                Rect cellRect = new Rect(topLeft.x + local.x * step, topLeft.y - local.y * step - size, size, size);
                shape.SetRoundedRect(cellRect, 0f, 1);
                if (borderWidth > 0f)
                    shape.Fill(vh, cellRect.center, borderWidth, antiAlias, borderColor * color, uv);
                shape.Fill(vh, cellRect.center, 0f, antiAlias, fill * color, uv);
            }
        }

        // 스프라이트가 아틀라스에 들어가도 맞도록 텍스처 안 스프라이트 영역의 가운데를 쓴다.
        private Vector2 GetSpriteCenterUV()
        {
            if (cellSprite == null) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            Vector2[] spriteUV = cellSprite.uv;
            foreach (Vector2 point in spriteUV)
                sum += point;
            return sum / spriteUV.Length;
        }
    }
}
