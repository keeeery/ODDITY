using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 칩 모양(칸 좌표)을 작은 각진 칸들로 그린다. 좌상단 (0,0), x는 오른쪽, y는 아래로 증가하는 좌표를 받는다.
    // 모양에 꼭 맞게 잘라 영역 가운데에 놓고, 영역에 들어가도록 칸 크기를 줄인다(최대 maxCellSize).
    // 칸 스프라이트를 넣으면 인벤토리처럼 칩 색으로 칠하고, 없으면 HUD 글자색으로 칠한다.
    // 테두리는 인벤토리 칩 외곽선과 같은 색이다. 스프라이트로 칠하는 동안은 테두리에 칩 색이 섞이지 않도록 자식(ChipShapeBorder)이 따로 그린다.
    // 기준 해상도가 큰 캔버스에서는 얇은 테두리가 1픽셀보다 가늘어져 선마다 진하기가 달라지므로, 두께는 화면 픽셀로 정하고
    // 실행 중에는 칸 가장자리를 화면 픽셀 경계에 맞춘다(맞춘 가장자리는 안티앨리어싱 없이 또렷하게 그림).
    [RequireComponent(typeof(CanvasRenderer))]
    public class ChipShapeGraphic : MaskableGraphic
    {
        [Header("Layout")]
        [SerializeField, Min(1f)] private float maxCellSize = 10f;
        [SerializeField, Min(0f)] private float cellGap = 2f;

        [Header("Colors")]
        [SerializeField] private Color cellColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        [Tooltip("인벤토리 칩 외곽선(ChipItemView)과 같은 색")]
        [SerializeField] private Color borderColor = new(0.93f, 0.91f, 0.87f, 0.85f);

        [Header("Style")]
        [Tooltip("테두리 두께(화면 픽셀)")]
        [SerializeField, Min(0f)] private float borderWidth = 1f;
        [SerializeField, Range(0f, 3f)] private float antiAliasPixels = 1f;

        private readonly VectorShape shape = new();
        private Vector2Int[] cells;
        private Sprite cellSprite;
        private ChipShapeBorder border;
        private readonly List<Rect> cellRects = new();
        private Vector3 lastPosition;
        private Vector3 lastScale;

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

        // 실행 중 스프라이트로 칠할 때만 테두리를 자식이 그린다. 에디터에서 멈춰 있을 때는 저장되는 자식을 만들지 않도록 직접 그린다
        internal bool DrawsBorderSeparately => cellSprite != null && borderWidth > 0f && Application.isPlaying;
        internal Color BorderColor => borderColor;

        // 테두리 두께를 이 그래픽 좌표로 바꾼 값
        internal float BorderWidthUnits => borderWidth * UnitsPerPixel();

        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();
            if (DrawsBorderSeparately) EnsureBorder();
            if (border != null) border.SetVerticesDirty();
        }

        // 툴팁이 커서를 따라 움직이거나 커졌다 작아지면 픽셀 경계가 달라지므로 다시 맞춘다
        private void LateUpdate()
        {
            if (DrawsBorderSeparately == false) return;

            Vector3 position = rectTransform.position;
            Vector3 scale = rectTransform.lossyScale;
            if (position == lastPosition && scale == lastScale) return;

            lastPosition = position;
            lastScale = scale;
            SetVerticesDirty();
        }

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

            GetCellRects(cellRects);
            if (cellRects.Count == 0) return;

            // 픽셀 경계에 맞춘 칸은 안티앨리어싱을 넣으면 오히려 가장자리가 두 픽셀에 번진다
            bool snapped = DrawsBorderSeparately;
            float antiAlias = snapped ? 0f : VectorShape.PixelsToUnits(canvas, antiAliasPixels);
            Vector2 uv = GetSpriteCenterUV();
            Color fill = cellSprite != null ? Color.white : cellColor;
            bool drawsBorder = borderWidth > 0f && snapped == false;
            float borderUnits = BorderWidthUnits;

            foreach (Rect cellRect in cellRects)
            {
                shape.SetRoundedRect(cellRect, 0f, 1);
                if (drawsBorder)
                    shape.Fill(vh, cellRect.center, borderUnits, antiAlias, borderColor * color, uv);
                shape.Fill(vh, cellRect.center, 0f, antiAlias, fill * color, uv);
            }
        }

        // 모양 전체를 영역 가운데에 놓고, 아래로 증가하는 y를 위에서부터 내려 그린 칸 영역들 (이 그래픽 좌표)
        internal void GetCellRects(List<Rect> rects)
        {
            rects.Clear();

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

            Vector2 topLeft = rect.center + new Vector2(-(columns * step - cellGap), rows * step - cellGap) * 0.5f;
            bool snap = DrawsBorderSeparately;
            foreach (Vector2Int cell in cells)
            {
                Vector2Int local = cell - min;
                Rect cellRect = new Rect(topLeft.x + local.x * step, topLeft.y - local.y * step - size, size, size);
                if (snap)
                {
                    Vector2 snappedMin = SnapToPixel(cellRect.min);
                    Vector2 snappedMax = SnapToPixel(cellRect.max);
                    cellRect = Rect.MinMaxRect(snappedMin.x, snappedMin.y, snappedMax.x, snappedMax.y);
                }
                rects.Add(cellRect);
            }
        }

        // Overlay 캔버스면 화면 좌표 변환에 카메라를 쓰지 않는다
        private Camera CanvasCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        // 이 그래픽 좌표의 점을 가장 가까운 화면 픽셀 경계로 옮긴다
        private Vector2 SnapToPixel(Vector2 local)
        {
            Camera camera = CanvasCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(local));
            screen.x = Mathf.Round(screen.x);
            screen.y = Mathf.Round(screen.y);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out Vector2 snapped) ? snapped : local;
        }

        // 화면 1픽셀이 이 그래픽 좌표로 얼마인지, 캔버스 배율뿐 아니라 창이 커졌다 작아지는 연출의 크기까지 따른다
        private float UnitsPerPixel()
        {
            Camera camera = CanvasCamera;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, origin, camera, out Vector2 a) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, origin + Vector2.right, camera, out Vector2 b))
            {
                float units = Vector2.Distance(a, b);
                if (units > 0f) return units;
            }
            return VectorShape.PixelsToUnits(canvas, 1f);
        }

        // 이 그래픽과 같은 크기로 늘인 자식, 칸 위에 그려지지만 테두리는 칸 바깥에만 두르므로 칸을 가리지 않는다
        private void EnsureBorder()
        {
            if (border != null) return;

            GameObject borderObject = new GameObject("Border", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            borderObject.layer = gameObject.layer;
            RectTransform borderRect = (RectTransform)borderObject.transform;
            borderRect.SetParent(transform, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = borderRect.offsetMax = Vector2.zero;

            border = borderObject.AddComponent<ChipShapeBorder>();
            border.raycastTarget = false;
            border.Owner = this;
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
