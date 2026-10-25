using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 화면 가장자리를 붉게 물들이는 피격 연출
    // 피격 시 번쩍 → 유지 → 서서히 사라짐, 저체력이면 맥박처럼 깜빡임
    [RequireComponent(typeof(CanvasRenderer))]
    public class DamageVignette : MaskableGraphic
    {
        private const int GradientRings = 16;
        private const int CornerSegments = 8;
        private const int ContourPoints = (CornerSegments + 1) * 4;

        [Header("Shape")]
        [SerializeField] private Color vignetteColor = new(0.55f, 0.04f, 0.04f, 1f);
        // 가장자리에서 안쪽으로 이어지는 그라데이션 폭 (화면 짧은 쪽 절반 대비)
        [SerializeField, Range(0.05f, 1f)] private float edgeWidth = 0.8f;
        // 클수록 가장자리에 붉은색이 몰림
        [SerializeField, Min(1f)] private float falloffPower = 2f;
        // 클수록 꼭짓점 쪽 붉은색이 중앙으로 더 들어옴
        [SerializeField, Min(0f)] private float cornerRoundness = 2f;

        // alpha로 번쩍 → holdDuration 동안 유지 → fadeDuration 동안 사라짐
        [Serializable]
        private struct HitEnvelope
        {
            [Range(0f, 1f)] public float alpha;
            [Min(0f)] public float holdDuration;
            [Min(0.01f)] public float fadeDuration;
        }

        [Header("Hit")]
        [SerializeField] private HitEnvelope normalHit = new()
        {
            alpha = 0.3f, holdDuration = 0.05f, fadeDuration = 0.4f,
        };
        [SerializeField] private HitEnvelope heavyHit = new()
        {
            alpha = 0.7f, holdDuration = 0.4f, fadeDuration = 1.4f,
        };

        [Header("Low Health")]
        [SerializeField, Range(0f, 1f)] private float lowHealthRatio = 0.3f;
        [SerializeField, Range(0f, 1f)] private float lowHealthMinAlpha = 0.2f;
        [SerializeField, Range(0f, 1f)] private float lowHealthMaxAlpha = 0.5f;
        // 초당 맥박 횟수
        [SerializeField, Min(0.1f)] private float lowHealthPulseRate = 0.8f;

        private float hitAlpha;
        private Sequence hitSequence;
        private float healthRatio = 1f;
        private float currentAlpha;

        public float HealthRatio
        {
            set => healthRatio = Mathf.Clamp01(value);
        }

        public void Pulse(bool isHeavy)
        {
            HitEnvelope envelope = isHeavy ? heavyHit : normalHit;

            // 더 강한 연출 중이면 덮어쓰지 않음
            if (hitAlpha > envelope.alpha) return;

            hitSequence?.Kill();
            hitAlpha = envelope.alpha;
            hitSequence = DOTween.Sequence().SetTarget(this)
                .AppendInterval(envelope.holdDuration)
                .Append(DOTween.To(() => hitAlpha, value => hitAlpha = value, 0f, envelope.fadeDuration)
                    .SetEase(Ease.InOutSine));
        }

        protected override void OnDisable()
        {
            hitSequence?.Kill();
            hitAlpha = 0f;
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif

        private void Update()
        {
            if (Application.isPlaying == false) return;

            float alpha = hitAlpha;

            if (healthRatio < lowHealthRatio)
            {
                // 기준 체력 아래로 내려가면 절반 세기부터 시작해 낮을수록 진해짐
                float danger = Mathf.Lerp(0.5f, 1f, 1f - healthRatio / lowHealthRatio);
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * lowHealthPulseRate * Mathf.PI * 2f);
                alpha = Mathf.Max(alpha, Mathf.Lerp(lowHealthMinAlpha, lowHealthMaxAlpha, beat) * danger);
            }

            if (Mathf.Approximately(alpha, currentAlpha)) return;

            currentAlpha = alpha;
            SetVerticesDirty();
        }

        // 네 변에서 같은 거리만큼 들어온 둥근 사각형 고리를 겹쳐, 화면 끝은 진하고 안쪽은 투명한 그라데이션을 만든다.
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (currentAlpha <= 0.001f) return;

            Rect rect = rectTransform.rect;
            float maxInset = Mathf.Min(rect.width, rect.height) * 0.5f;
            float width = maxInset * edgeWidth;

            if (width <= 0f) return;

            Color32 solid = vignetteColor * color;
            byte maxAlpha = (byte)(solid.a * currentAlpha);
            int startIndex = vh.currentVertCount;

            for (int ring = 0; ring <= GradientRings; ring++)
            {
                float t = (float)ring / GradientRings;
                float inset = Mathf.Min(width * t, maxInset - 0.5f);
                Color32 ringColor = solid;
                ringColor.a = (byte)(maxAlpha * Mathf.Pow(1f - t, falloffPower));
                AddContour(vh, rect, inset, inset * cornerRoundness, ringColor);
            }

            for (int ring = 0; ring < GradientRings; ring++)
            {
                int outer = startIndex + ring * ContourPoints;
                int inner = outer + ContourPoints;

                for (int index = 0; index < ContourPoints; index++)
                {
                    int next = (index + 1) % ContourPoints;
                    vh.AddTriangle(outer + index, outer + next, inner + index);
                    vh.AddTriangle(inner + index, outer + next, inner + next);
                }
            }
        }

        // 화면 사각형을 inset만큼 줄인 둥근 사각형, 고리끼리 이으려면 점 개수가 같아야 한다.
        private static void AddContour(VertexHelper vh, Rect rect, float inset, float cornerRadius, Color32 ringColor)
        {
            float left = rect.xMin + inset;
            float right = rect.xMax - inset;
            float bottom = rect.yMin + inset;
            float top = rect.yMax - inset;
            float radius = Mathf.Min(cornerRadius, (right - left) * 0.5f, (top - bottom) * 0.5f);

            AddCorner(vh, new Vector2(right - radius, top - radius), radius, 0f, ringColor);
            AddCorner(vh, new Vector2(right - radius, bottom + radius), radius, 90f, ringColor);
            AddCorner(vh, new Vector2(left + radius, bottom + radius), radius, 180f, ringColor);
            AddCorner(vh, new Vector2(left + radius, top - radius), radius, 270f, ringColor);
        }

        private static void AddCorner(VertexHelper vh, Vector2 center, float radius, float startAngle, Color32 ringColor)
        {
            for (int index = 0; index <= CornerSegments; index++)
            {
                Vector2 direction = VectorShape.AngleToDirection(startAngle + 90f * index / CornerSegments);
                vh.AddVert(center + direction * radius, ringColor, Vector2.zero);
            }
        }
    }
}
