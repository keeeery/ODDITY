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
            Color border = Color.Lerp(borderColor, highlightBorderColor, highlightAmount);

            shape.SetRoundedRect(rect, 0f, 1);
            if (shadowSize > 0f)
                shape.FillShadow(vh, rect.center, borderWidth, shadowSize, shadowColor * color);
            if (borderWidth > 0f)
                shape.Fill(vh, rect.center, borderWidth, antiAlias, border * color);
            shape.Fill(vh, rect.center, 0f, antiAlias, trackColor * color);
            if (highlightAmount > 0.001f && highlightTrackColor.a > 0f)
            {
                Color hover = highlightTrackColor;
                hover.a *= highlightAmount;
                shape.Fill(vh, rect.center, 0f, antiAlias, hover * color);
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
