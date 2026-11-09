using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 체력바·스킬 슬롯과 같은 방식(그림자, 1px 테두리, 반투명 트랙)으로 그리는 각진 패널
    // 툴팁·보상 슬롯·버튼 배경처럼 HUD 밖의 화면도 같은 생김새로 맞출 때 쓴다.
    // 마우스 반응을 켜면 올렸을 때 테두리가 스킬 슬롯의 사용 가능 상태처럼 밝아진다. 자식 위에 올려도 반응한다.
    // 밝아지고 어두워지는 전환은 highlightFadeDuration 동안 실제 시간 기준으로 부드럽게 이어진다.
    // 기본값은 SkillSlotHUD의 씬 설정과 맞춘 값
    [RequireComponent(typeof(CanvasRenderer))]
    public class VectorPanel : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Colors")]
        [SerializeField] private Color trackColor = new(0.07058824f, 0.07843137f, 0.09019608f, 0.85f);
        [SerializeField] private Color borderColor = new(0.1176471f, 0.1058824f, 0.09019608f, 1f);
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.35f);

        [Header("Style")]
        [SerializeField, Min(0f)] private float borderWidth = 1f;
        [SerializeField, Min(0f)] private float shadowSize = 3f;
        [SerializeField, Range(0f, 3f)] private float antiAliasPixels = 1f;

        [Header("Hover")]
        [SerializeField] private bool highlightOnHover;
        [SerializeField] private Color highlightBorderColor = new(0.93f, 0.91f, 0.87f, 0.9f);
        [Tooltip("올렸을 때 트랙 위에 더하는 색, 알파가 0이면 테두리만 밝아짐")]
        [SerializeField] private Color highlightTrackColor = new(0.93f, 0.91f, 0.87f, 0f);
        [SerializeField, Min(0f)] private float highlightFadeDuration = 0.12f;

        [Header("Flash")]
        [SerializeField] private Color flashColor = new(1f, 1f, 1f, 0.7f);

        private readonly VectorShape shape = new();
        private bool highlighted;
        private float highlightAmount;
        private float flashAlpha;
        private bool outlineOnly;

        public bool Highlighted
        {
            get => highlighted;
            set
            {
                if (highlighted == value) return;
                highlighted = value;
                if (highlightFadeDuration <= 0f || Application.isPlaying == false) SetHighlightAmount(value ? 1f : 0f);
            }
        }

        // 바탕 색. 버튼을 복제해 입력 칸처럼 바탕만 바꿔 쓸 때 쓴다
        public Color TrackColor
        {
            get => trackColor;
            set
            {
                if (trackColor == value) return;
                trackColor = value;
                SetVerticesDirty();
            }
        }

        // 켜면 테두리를 선으로만 그려 트랙이 투명한 버튼도 바탕이 비어 보인다. 끄면(기본) 테두리 색이 트랙 아래를 채운다
        // 증강 칩 창의 전체 해제 버튼처럼 실행 중에 복제한 버튼 하나만 다르게 보이게 할 때 쓴다
        public bool OutlineOnly
        {
            get => outlineOnly;
            set
            {
                if (outlineOnly == value) return;
                outlineOnly = value;
                SetVerticesDirty();
            }
        }

        // 켜질 때 트랙 위를 덮는 번쩍임 세기(0~1), 스킬 슬롯의 사용 가능 번쩍임과 같은 방식
        public float FlashAlpha
        {
            get => flashAlpha;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(flashAlpha, clamped)) return;
                flashAlpha = clamped;
                SetVerticesDirty();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightOnHover) Highlighted = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 자식으로 옮겨 갈 때는 나가기가 오지 않고, 패널과 자식을 모두 벗어날 때만 온다.
            if (highlightOnHover) Highlighted = false;
        }

        private void Update()
        {
            float target = highlighted ? 1f : 0f;
            if (Mathf.Approximately(highlightAmount, target)) return;
            SetHighlightAmount(Mathf.MoveTowards(highlightAmount, target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, highlightFadeDuration)));
        }

        private void SetHighlightAmount(float amount)
        {
            highlightAmount = amount;
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            highlighted = false;
            highlightAmount = 0f;
            flashAlpha = 0f;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            float antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);

            shape.SetRoundedRect(rect, 0f, 1);
            if (shadowSize > 0f)
                shape.FillShadow(vh, rect.center, borderWidth, shadowSize, shadowColor * color);
            // 테두리 색은 트랙 아래를 통째로 채운다. 트랙이 투명한 버튼은 이 색이 옅은 바탕으로 보인다
            if (borderWidth > 0f && outlineOnly == false)
                shape.Fill(vh, rect.center, borderWidth, antiAlias, borderColor * color);
            shape.Fill(vh, rect.center, 0f, antiAlias, trackColor * color);
            if (highlightAmount > 0.001f && highlightTrackColor.a > 0f)
            {
                Color hover = highlightTrackColor;
                hover.a *= highlightAmount;
                shape.Fill(vh, rect.center, 0f, antiAlias, hover * color);
            }
            // 강조는 테두리 선에만 칠한다. 바탕까지 칠하면 트랙이 투명한 버튼·드롭다운은 바탕 전체가 밝아져 밝은 글자가 묻힌다
            if (borderWidth > 0f)
            {
                if (outlineOnly)
                {
                    Color border = Color.Lerp(borderColor, highlightBorderColor, highlightAmount);
                    shape.Stroke(vh, 0f, borderWidth, antiAlias, border * color);
                }
                else if (highlightAmount > 0.001f && highlightBorderColor.a > 0f)
                {
                    Color highlight = highlightBorderColor;
                    highlight.a *= highlightAmount;
                    shape.Stroke(vh, 0f, borderWidth, antiAlias, highlight * color);
                }
            }

            if (flashAlpha > 0.001f)
            {
                Color flash = flashColor;
                flash.a *= flashAlpha;
                shape.Fill(vh, rect.center, 0f, antiAlias, flash * color);
            }
        }
    }
}
