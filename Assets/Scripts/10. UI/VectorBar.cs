using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 그림자, 테두리, 배경 트랙, 채움을 둥근 사각형으로 그리는 게이지 바
    // 미니맵 마커와 같이 가장자리를 부드럽게 처리해 벡터 이미지처럼 보이게 한다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class VectorBar : MaskableGraphic
    {
        private const int CornerSegments = 8;

        [Header("Value")]
        [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;

        [Header("Shape")]
        // 0 미만이면 높이의 절반을 써서 알약 모양으로 만든다.
        [SerializeField] private float cornerRadius = -1f;
        // 트랙 가장자리와 채움 사이 여백
        [SerializeField, Min(0f)] private float fillPadding = 2f;

        [Header("Colors")]
        [SerializeField] private Color fillColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color trackColor = new(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color borderColor = Color.white;
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);

        [Header("Style")]
        [SerializeField, Min(0f)] private float borderWidth = 2f;
        [SerializeField, Min(0f)] private float shadowSize = 3f;
        // 가장자리를 부드럽게 하는 폭(화면 픽셀)
        [SerializeField, Range(0f, 3f)] private float antiAliasPixels = 1f;

        private readonly VectorShape shape = new();

        public float FillAmount
        {
            get => fillAmount;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(fillAmount, clamped)) return;
                fillAmount = clamped;
                SetVerticesDirty();
            }
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

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            float antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);
            float radius = cornerRadius < 0f ? rect.height * 0.5f : cornerRadius;

            shape.SetRoundedRect(rect, radius, CornerSegments);
            if (shadowSize > 0f)
                shape.Fill(vh, rect.center, borderWidth + shadowSize * 0.5f, shadowSize, shadowColor * color);
            if (borderWidth > 0f)
                shape.Fill(vh, rect.center, borderWidth, antiAlias, borderColor * color);
            shape.Fill(vh, rect.center, 0f, antiAlias, trackColor * color);

            // 채움은 트랙 안쪽에서 왼쪽부터 늘어나며, 짧아져도 둥근 끝이 유지되도록 반지름을 줄여서 그린다.
            Rect inner = new Rect(rect.x + fillPadding, rect.y + fillPadding,
                rect.width - fillPadding * 2f, rect.height - fillPadding * 2f);
            float fillWidth = inner.width * fillAmount;
            if (fillWidth <= antiAlias || inner.height <= 0f) return;

            Rect fill = new Rect(inner.x, inner.y, fillWidth, inner.height);
            float innerRadius = Mathf.Max(0f, Mathf.Min(radius, rect.height * 0.5f) - fillPadding);
            shape.SetRoundedRect(fill, innerRadius, CornerSegments);
            shape.Fill(vh, fill.center, 0f, antiAlias, fillColor * color);
        }
    }
}
