using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 그림자, 테두리, 배경 트랙, 채움을 사각형(모서리 반경 조절 가능)으로 그리는 게이지 바
    // 미니맵 마커와 같이 가장자리를 부드럽게 처리해 벡터 이미지처럼 보이게 한다.
    // 값이 줄면 줄어든 구간을 잔상으로 잠시 남겼다가 따라 줄여서, 받은 피해량이 한눈에 보이게 한다.
    // 차오름을 켜면 값이 늘 때 바로 채우지 않고 DOTween으로 차오르게 한다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class VectorBar : MaskableGraphic
    {
        private const int CornerSegments = 8;

        [Header("Value")]
        [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;

        [Header("Shape")]
        // 0 미만이면 높이의 절반을 써서 알약 모양, 0이면 각진 사각형
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

        [Header("Damage Trail")]
        // 스태미나처럼 계속 줄었다 차는 바는 꺼두는 편이 자연스럽다.
        [SerializeField] private bool useTrail;
        [SerializeField] private Color trailColor = new(0.79f, 0.64f, 0.29f, 1f);
        // 마지막으로 줄어든 뒤 잔상이 줄기 시작할 때까지 기다리는 시간(초)
        [SerializeField, Min(0f)] private float trailDelay = 0.6f;
        // 잔상이 줄어드는 속도(초당 전체 대비 비율)
        [SerializeField, Min(0.01f)] private float trailSpeed = 0.6f;

        [Header("Rise")]
        [SerializeField] private bool useRise;
        // 차오르는 속도(초당 전체 대비 비율)
        [SerializeField, Min(0.01f)] private float riseSpeed = 1.2f;
        [SerializeField] private Ease riseEase = Ease.OutCubic;

        [Header("Hit Flash")]
        // Flash() 호출 시 채움이 번쩍이고 바가 좌우로 흔들린다.
        [SerializeField] private Color flashColor = new(1f, 0.95f, 0.88f, 0.8f);
        [SerializeField, Min(0.01f)] private float flashDuration = 0.2f;
        [SerializeField, Min(0f)] private float shakeDistance = 4f;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.3f;
        // DOTween.Punch의 진동 횟수와 탄성
        [SerializeField, Min(1)] private int shakeVibrato = 20;
        [SerializeField, Range(0f, 1f)] private float shakeElasticity = 1f;

        private readonly VectorShape shape = new();
        private float trailAmount = 1f;
        private float trailHoldUntil;
        private Tween trailTween;
        private float targetAmount = 1f; // 차오르는 중이면 fillAmount는 여기까지 따라간다.
        private Tweener riseTween;
        private float flashAlpha;
        private Vector3 shakeOffset;

        public bool UseTrail
        {
            get => useTrail;
            set { useTrail = value; trailAmount = Mathf.Max(trailAmount, fillAmount); SetVerticesDirty(); }
        }

        public Color TrailColor
        {
            get => trailColor;
            set { trailColor = value; SetVerticesDirty(); }
        }

        public bool UseRise
        {
            get => useRise;
            set => useRise = value;
        }

        public float FillAmount
        {
            get => targetAmount;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(targetAmount, clamped)) return;
                targetAmount = clamped;

                // 보이는 값보다 늘면 차오르게 하고, 줄면 차오르기를 멈추고 바로 줄인다.
                if (clamped > fillAmount && useRise && Application.isPlaying && isActiveAndEnabled)
                {
                    Rise();
                    return;
                }
                riseTween?.Pause();

                // 줄어들면 잔상을 남기고 대기 시간을 다시 잡고, 늘어나면 잔상도 함께 올린다.
                if (clamped < fillAmount) trailHoldUntil = Time.time + trailDelay;
                fillAmount = clamped;
                if (useTrail == false || trailAmount < fillAmount) trailAmount = fillAmount;
                UpdateTrail();
                SetVerticesDirty();
            }
        }

        // 잔상 없이 바로 맞춘다. 대상이 바뀌는 경우처럼 줄어든 것이 피해가 아닐 때 쓴다.
        public void SetFillImmediate(float amount)
        {
            trailTween?.Kill();
            trailTween = null;
            riseTween?.Pause();
            fillAmount = Mathf.Clamp01(amount);
            targetAmount = fillAmount;
            trailAmount = fillAmount;
            SetVerticesDirty();
        }

        public void Flash()
        {
            // 진행 중인 연출은 끝내고 새로 시작한다.
            DOTween.Kill(this, true);

            flashAlpha = 1f;
            DOTween.To(() => flashAlpha, value => flashAlpha = value, 0f, flashDuration)
                .SetEase(Ease.OutQuad).SetTarget(this).OnUpdate(SetVerticesDirty).OnComplete(SetVerticesDirty);

            shakeOffset = Vector3.zero;
            DOTween.Punch(() => shakeOffset, value => shakeOffset = value, Vector3.right * shakeDistance, shakeDuration,
                    shakeVibrato, shakeElasticity)
                .SetTarget(this).OnUpdate(SetVerticesDirty).OnComplete(SetVerticesDirty);
        }

        protected override void OnDisable()
        {
            DOTween.Kill(this);
            trailTween?.Kill();
            trailTween = null;
            riseTween?.Kill();
            fillAmount = targetAmount; // 꺼질 때 차오르던 값은 끝값으로 맞춤
            flashAlpha = 0f;
            shakeOffset = Vector3.zero;
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            trailAmount = fillAmount;
            targetAmount = fillAmount;
        }

        // 지금 위치에서 새 끝값으로 차오름, 자연 회복처럼 매 프레임 조금씩 늘어도 트윈을 새로 만들지 않고 다시 씀
        private void Rise()
        {
            float duration = (targetAmount - fillAmount) / riseSpeed;
            if (riseTween != null)
            {
                riseTween.ChangeEndValue(targetAmount, duration, true).Restart();
                return;
            }

            riseTween = DOTween.To(() => fillAmount, SetRisingFill, targetAmount, duration)
                .SetEase(riseEase).SetAutoKill(false).OnKill(() => riseTween = null);
        }

        // 잔상이 채움 아래로 내려가 있으면 함께 올림
        private void SetRisingFill(float amount)
        {
            fillAmount = amount;
            if (trailAmount < fillAmount) trailAmount = fillAmount;
            SetVerticesDirty();
        }

        private void UpdateTrail()
        {
            trailTween?.Kill();
            trailTween = null;
            if (Application.isPlaying == false || isActiveAndEnabled == false ||
                useTrail == false || trailAmount <= fillAmount) return;

            float duration = (trailAmount - fillAmount) / trailSpeed;
            // Flash()가 같은 컴포넌트의 연출을 종료해도 잔상은 계속 진행한다.
            trailTween = DOTween.To(() => trailAmount, value => trailAmount = value, fillAmount, duration)
                .SetDelay(Mathf.Max(0f, trailHoldUntil - Time.time))
                .SetEase(Ease.Linear).OnUpdate(SetVerticesDirty).OnComplete(SetVerticesDirty)
                .OnKill(() => trailTween = null);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            trailAmount = fillAmount;
            targetAmount = fillAmount;
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            // RectTransform 대신 그리는 위치만 옮긴다.
            rect.x += shakeOffset.x;

            float antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);
            float radius = cornerRadius < 0f ? rect.height * 0.5f : cornerRadius;

            shape.SetRoundedRect(rect, radius, CornerSegments);
            if (shadowSize > 0f)
                shape.FillShadow(vh, rect.center, borderWidth, shadowSize, shadowColor * color);
            if (borderWidth > 0f)
                shape.Fill(vh, rect.center, borderWidth, antiAlias, borderColor * color);
            shape.Fill(vh, rect.center, 0f, antiAlias, trackColor * color);

            // 잔상은 채움 뒤에 깔아서 줄어든 구간만 보이게 한다.
            Rect inner = new Rect(rect.x + fillPadding, rect.y + fillPadding,
                rect.width - fillPadding * 2f, rect.height - fillPadding * 2f);
            float innerRadius = Mathf.Max(0f, Mathf.Min(radius, rect.height * 0.5f) - fillPadding);
            if (useTrail) AddInnerFill(vh, inner, innerRadius, trailAmount, trailColor, antiAlias);
            AddInnerFill(vh, inner, innerRadius, fillAmount, fillColor, antiAlias);

            // 잔상 구간까지 함께 밝힌다.
            if (flashAlpha > 0.001f)
            {
                Color overlay = flashColor;
                overlay.a *= flashAlpha;
                AddInnerFill(vh, inner, innerRadius, useTrail ? trailAmount : fillAmount, overlay, antiAlias);
            }
        }

        // 트랙 안쪽에서 왼쪽부터 amount만큼 채우며, 짧아져도 둥근 끝이 유지되도록 반지름을 줄여서 그린다.
        private void AddInnerFill(VertexHelper vh, Rect inner, float radius, float amount, Color fill, float antiAlias)
        {
            float width = inner.width * amount;
            if (width <= antiAlias || inner.height <= 0f) return;

            Rect area = new Rect(inner.x, inner.y, width, inner.height);
            shape.SetRoundedRect(area, radius, CornerSegments);
            shape.Fill(vh, area.center, 0f, antiAlias, fill * color);
        }
    }
}
