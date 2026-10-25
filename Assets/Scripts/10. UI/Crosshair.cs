using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 가운데 점에 상황별 표시를 더하는 조준점
    // 링: 잡을 수 있는 대상, +: 기본 공격 사거리 안의 적, ×: 공격 적중
    [RequireComponent(typeof(CanvasRenderer))]
    public class Crosshair : MaskableGraphic
    {
        private const int CircleSegments = 24;
        private const int RingSegments = 64;
        private const float VisibleAlpha = 0.01f;

        [Header("References")]
        [SerializeField] private PsychokinesisController psychokinesis;

        [Header("Dot")]
        [SerializeField, Min(0f)] private float dotRadius = 2.5f;
        [SerializeField] private Color dotColor = new(0.93f, 0.9f, 0.84f, 1f);
        // 점에만 두르는 어두운 테두리, 밝은 배경에서도 보이게 한다.
        [SerializeField] private Color dotBorderColor = new(0.118f, 0.106f, 0.09f, 1f);
        [SerializeField, Min(0f)] private float dotBorderWidth = 1.2f;

        [Header("Attack Target")]
        // 기본 공격 사거리 안에 적이 있으면 상하좌우 짧은 선이 조여들며 나타난다.
        [SerializeField] private Color attackTickColor = new(0.93f, 0.9f, 0.84f, 1f);
        // 짧은 선이 시작하는(숨어 있는) 거리와 나타난 뒤의 거리
        [SerializeField, Min(0f)] private float attackTickHiddenRadius = 10f;
        [SerializeField, Min(0f)] private float attackTickRadius = 5.5f;
        [SerializeField, Min(0f)] private float attackTickLength = 6f;
        [SerializeField, Min(0f)] private float attackTickWidth = 1.8f;

        [Header("Grab Ring")]
        [SerializeField, Min(0f)] private float ringWidth = 1.5f;
        // 이 크기에서 투명하게 시작해 조여들며 나타난다.
        [SerializeField, Min(0f)] private float hiddenRadius = 28f;
        [SerializeField, Min(0f)] private float ringRadius = 18f;

        [Header("Hit Marker")]
        [SerializeField] private Color hitColor = new(0.93f, 0.9f, 0.84f, 1f);
        [SerializeField, Min(0.01f)] private float hitDuration = 0.25f;
        // 대각선 표시가 시작하고 끝나는 중심으로부터의 거리
        [SerializeField, Min(0f)] private float hitStartRadius = 6f;
        [SerializeField, Min(0f)] private float hitEndRadius = 10f;
        [SerializeField, Min(0f)] private float hitLength = 7f;
        [SerializeField, Min(0f)] private float hitWidth = 1.8f;

        [Header("Style")]
        // 크기, 색, 투명도가 목표값을 따라가는 속도
        [SerializeField, Min(0.1f)] private float transitionSpeed = 14f;
        // 가장자리를 부드럽게 하는 폭(화면 픽셀)
        [SerializeField, Range(0f, 3f)] private float antiAliasPixels = 1f;

        private readonly VectorShape shape = new();

        private float enemyBlend;
        private float currentRingRadius;
        private float ringAlpha;
        private Color ringColor;
        private float lastHitTime = float.NegativeInfinity;
        private PsychokinesisController subscribed;

        protected override void Start()
        {
            base.Start();
            currentRingRadius = hiddenRadius;

            if (Application.isPlaying == false) return;

            if (psychokinesis == null) psychokinesis = FindAnyObjectByType<PsychokinesisController>();
            Subscribe(psychokinesis);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Subscribe(null);
        }

        private void Subscribe(PsychokinesisController target)
        {
            if (subscribed != null) subscribed.AttackHit -= HandleAttackHit;

            subscribed = target;

            if (subscribed != null) subscribed.AttackHit += HandleAttackHit;
        }

        private void HandleAttackHit() => lastHitTime = Time.time;

        private void Update()
        {
            if (Application.isPlaying == false) return;

            PsychokinesisObject held = psychokinesis != null ? psychokinesis.HeldObject : null;
            PsychokinesisObject focused = PsychokinesisObject.OutlinedObject;
            bool showRing = held == null && focused != null && focused.IsFocusAvailable;
            bool hasEnemy = held == null && psychokinesis != null && psychokinesis.HasAttackTarget;

            float blend = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
            enemyBlend = Mathf.Lerp(enemyBlend, hasEnemy ? 1f : 0f, blend);
            currentRingRadius = Mathf.Lerp(currentRingRadius, showRing ? ringRadius : hiddenRadius, blend);
            ringAlpha = Mathf.Lerp(ringAlpha, showRing ? 1f : 0f, blend);

            if (showRing) ringColor = focused.OutlineColor;

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

            Vector2 center = rectTransform.rect.center;
            float antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);

            if (ringAlpha > VisibleAlpha)
            {
                Color ring = ringColor;
                ring.a *= ringAlpha;
                VectorShape.FillArc(vh, center, currentRingRadius, ringWidth, 0f, 360f, RingSegments, antiAlias,
                    ring * color);
            }

            if (enemyBlend > VisibleAlpha)
            {
                Color tick = attackTickColor;
                tick.a *= enemyBlend;
                float tickRadius = Mathf.Lerp(attackTickHiddenRadius, attackTickRadius, enemyBlend);
                AddTicks(vh, center, 0f, tickRadius, attackTickLength, attackTickWidth, tick, antiAlias);
            }

            float hitProgress = (Time.time - lastHitTime) / hitDuration;

            if (hitProgress >= 0f && hitProgress < 1f)
            {
                float eased = 1f - (1f - hitProgress) * (1f - hitProgress);
                Color hit = hitColor;
                hit.a *= 1f - hitProgress;
                AddTicks(vh, center, 45f, Mathf.Lerp(hitStartRadius, hitEndRadius, eased), hitLength, hitWidth, hit, antiAlias);
            }

            shape.SetCircle(center, dotRadius, CircleSegments);
            if (dotBorderWidth > 0f) shape.Fill(vh, center, dotBorderWidth, antiAlias, dotBorderColor * color);
            shape.Fill(vh, center, 0f, antiAlias, dotColor * color);
        }

        // 중심에서 inner만큼 떨어진 곳부터 바깥으로 뻗는 짧은 선 4개, startAngle이 0이면 +, 45면 × 모양
        private void AddTicks(VertexHelper vh, Vector2 center, float startAngle, float inner, float length, float width, Color fill, float antiAlias)
        {
            for (int index = 0; index < 4; index++)
                AddRadialTick(vh, center, startAngle + 90f * index, inner, length, width, fill * color, antiAlias);
        }

        // angle 방향으로 중심에서 inner만큼 떨어진 곳부터 length만큼 뻗는 짧은 선
        private void AddRadialTick(VertexHelper vh, Vector2 center, float angle, float inner, float length, float width, Color32 fill, float antiAlias)
        {
            Vector2 direction = VectorShape.AngleToDirection(angle);
            Vector2 side = new Vector2(direction.y, -direction.x) * (width * 0.5f);
            Vector2 from = center + direction * inner;
            Vector2 to = center + direction * (inner + length);

            shape.Clear();
            shape.Add(from + side);
            shape.Add(to + side);
            shape.Add(to - side);
            shape.Add(from - side);
            shape.Fill(vh, (from + to) * 0.5f, 0f, antiAlias, fill);
        }
    }
}
