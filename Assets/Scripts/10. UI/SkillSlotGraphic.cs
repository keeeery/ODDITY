using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 스킬 슬롯 한 칸의 생김새, SkillSlotHUD가 모든 슬롯에 같은 값을 넘긴다.
    // 기본값은 체력바(VectorBar)의 씬 설정과 맞춘 값
    [Serializable]
    public class SkillSlotStyle
    {
        public Color trackColor = new(0.07058824f, 0.07843137f, 0.09019608f, 0.85f);
        public Color emptyTrackColor = new(0.07058824f, 0.07843137f, 0.09019608f, 0.5f);
        public Color borderColor = new(0.1176471f, 0.1058824f, 0.09019608f, 1f);
        public Color shadowColor = new(0f, 0f, 0f, 0.35f);
        [Tooltip("쿨타임이 남은 부분을 덮는 색")]
        public Color cooldownColor = new(0f, 0f, 0f, 0.6f);
        [Tooltip("사용 가능할 때 테두리 색, HUD 글자와 어울리는 따뜻한 흰색")]
        public Color readyBorderColor = new(0.93f, 0.91f, 0.87f, 0.9f);
        [Tooltip("쿨타임이 끝나는 순간 슬롯 안쪽이 잠깐 번쩍이는 색")]
        public Color readyFlashColor = new(1f, 1f, 1f, 0.7f);
        [Min(0f)] public float borderWidth = 1f;
        [Min(0f)] public float shadowSize = 3f;
        [Tooltip("사용 가능할 때 테두리 두께")]
        [Min(0f)] public float readyBorderWidth = 1f;
        [Range(0f, 3f)] public float antiAliasPixels = 1f;
    }

    // 각진 사각 슬롯 한 칸을 체력바와 같은 방식(그림자, 테두리, 트랙)으로 그린다.
    // 쿨타임은 남은 비율만큼 위쪽(12시)부터 시계 방향으로 덮고, 시간이 지나며 덮개가 걷힌다.
    // 사용 가능하면 테두리가 밝은 색으로 바뀐다.
    // 글자(키, 스킬 이름, 남은 초)는 SkillSlotHUD가 자식 TMP로 따로 둔다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class SkillSlotGraphic : MaskableGraphic
    {
        private static readonly float[] cornerAngles = { 45f, 135f, 225f, 315f };

        private readonly VectorShape shape = new();
        private readonly List<Vector2> sweep = new();

        private SkillSlotStyle style = new();
        private bool hasSkill;
        private float cooldownRatio;
        private float flashAlpha;

        public void SetStyle(SkillSlotStyle value)
        {
            style = value ?? new SkillSlotStyle();
            SetVerticesDirty();
        }

        public bool HasSkill
        {
            get => hasSkill;
            set
            {
                if (hasSkill == value) return;
                hasSkill = value;
                SetVerticesDirty();
            }
        }

        // 남은 쿨타임 / 전체 쿨타임, 0이면 사용 가능
        public float CooldownRatio
        {
            get => cooldownRatio;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(cooldownRatio, clamped)) return;
                cooldownRatio = clamped;
                SetVerticesDirty();
            }
        }

        // 쿨타임이 끝난 순간 번쩍이는 정도(0~1), SkillSlotHUD가 줄여 나간다.
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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            float antiAlias = VectorShape.PixelsToUnits(canvas, style.antiAliasPixels);

            bool isReady = hasSkill && cooldownRatio <= 0.001f;
            float borderWidth = isReady ? style.readyBorderWidth : style.borderWidth;
            Color borderColor = isReady ? style.readyBorderColor : style.borderColor;

            shape.SetRoundedRect(rect, 0f, 1);
            if (style.shadowSize > 0f)
                shape.FillShadow(vh, rect.center, borderWidth, style.shadowSize, style.shadowColor * color);
            if (borderWidth > 0f)
                shape.Fill(vh, rect.center, borderWidth, antiAlias, borderColor * color);
            shape.Fill(vh, rect.center, 0f, antiAlias, (hasSkill ? style.trackColor : style.emptyTrackColor) * color);

            if (hasSkill == false) return;

            if (flashAlpha > 0.001f)
            {
                Color flash = style.readyFlashColor;
                flash.a *= flashAlpha;
                shape.Fill(vh, rect.center, 0f, antiAlias, flash * color);
            }

            if (isReady == false) AddCooldownSweep(vh, rect, style.cooldownColor * color);
        }

        // 지난 비율만큼 12시부터 걷히고 남은 부분(지난 각도 ~ 360도)을 사각형 테두리까지 부채꼴로 덮는다.
        // 꺾이는 모서리 각도를 사이에 넣어 변이 곧게 유지되도록 한다.
        private void AddCooldownSweep(VertexHelper vh, Rect rect, Color32 fill)
        {
            float startAngle = (1f - cooldownRatio) * 360f;

            sweep.Clear();
            sweep.Add(GetEdgePoint(rect, startAngle));
            foreach (float corner in cornerAngles)
                if (corner > startAngle) sweep.Add(GetEdgePoint(rect, corner));
            sweep.Add(GetEdgePoint(rect, 360f));

            int centerIndex = vh.currentVertCount;
            vh.AddVert(rect.center, fill, Vector2.zero);
            foreach (Vector2 point in sweep)
                vh.AddVert(point, fill, Vector2.zero);

            for (int index = 1; index < sweep.Count; index++)
                vh.AddTriangle(centerIndex, centerIndex + index, centerIndex + index + 1);
        }

        // 중심에서 angle 방향으로 나아가 사각형 테두리와 만나는 점
        private static Vector2 GetEdgePoint(Rect rect, float angle)
        {
            Vector2 direction = VectorShape.AngleToDirection(angle);
            Vector2 half = rect.size * 0.5f;
            float scale = Mathf.Min(
                Mathf.Abs(direction.x) > 0.0001f ? half.x / Mathf.Abs(direction.x) : float.MaxValue,
                Mathf.Abs(direction.y) > 0.0001f ? half.y / Mathf.Abs(direction.y) : float.MaxValue);
            return rect.center + direction * scale;
        }
    }
}
