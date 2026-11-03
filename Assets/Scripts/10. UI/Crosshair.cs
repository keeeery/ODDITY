using DG.Tweening;
using TeamOdd.Mission;
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
        private float hitAlpha;
        private float currentHitRadius;
        private Sequence hitSequence;
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

        protected override void OnDisable()
        {
            hitSequence?.Kill();
            hitSequence = null;
            hitAlpha = 0f;
            base.OnDisable();
        }

        private void HandleAttackHit()
        {
            if (isActiveAndEnabled == false) return;

            hitSequence?.Kill();
            hitAlpha = 1f;
            currentHitRadius = hitStartRadius;
            hitSequence = DOTween.Sequence()
                .Join(DOTween.To(() => hitAlpha, value => hitAlpha = value, 0f, hitDuration)
                    .SetEase(Ease.Linear))
                .Join(DOTween.To(() => currentHitRadius, value => currentHitRadius = value, hitEndRadius, hitDuration)
                    .SetEase(Ease.OutQuad))
                .OnUpdate(SetVerticesDirty).OnComplete(SetVerticesDirty).OnKill(() => hitSequence = null);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (Application.isPlaying == false) return;

            PsychokinesisObject held = psychokinesis != null ? psychokinesis.HeldObject : null;
            PsychokinesisObject focused = PsychokinesisObject.OutlinedObject;
            // 염동력 대상이 없으면 말을 걸 수 있는 NPC에도 같은 링을 띄운다 (외곽선과 같은 우선순위)
            MissionNPC npc = focused == null ? MissionNPC.OutlinedNPC : null;
            bool showRing = held == null && ((focused != null && focused.IsFocusAvailable) || npc != null);
            bool canAttack = psychokinesis != null && psychokinesis.CanNormalAttack;
            bool hasEnemy = canAttack && psychokinesis.HasAttackTarget;

            float blend = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
            enemyBlend = canAttack ? Mathf.Lerp(enemyBlend, hasEnemy ? 1f : 0f, blend) : 0f;
            currentRingRadius = Mathf.Lerp(currentRingRadius, showRing ? ringRadius : hiddenRadius, blend);
            ringAlpha = Mathf.Lerp(ringAlpha, showRing ? 1f : 0f, blend);

            if (showRing) ringColor = focused != null ? focused.OutlineColor : npc.OutlineColor;

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

            if (hitAlpha > 0f)
            {
                Color hit = hitColor;
                hit.a *= hitAlpha;
                AddTicks(vh, center, 45f, currentHitRadius, hitLength, hitWidth, hit, antiAlias);
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
