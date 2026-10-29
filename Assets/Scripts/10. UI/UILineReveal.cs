using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 가운데 1px 선이 좌우로 뻗은 뒤 위아래로 열리며 내용을 드러내고, 닫을 때는 반대로 선으로 접힌 뒤 점으로 줄어든다.
    // 이 오브젝트의 RectMask2D로 가리므로 드러낼 그래픽은 모두 자식이어야 한다(자기 그래픽은 가려지지 않음).
    // 선은 가려지면 안 되므로 마스크 밖의 형제로 두고(가운데 기준점), 크기·위치는 여기서 맞춘다. 실제 시간 기준이다.
    [RequireComponent(typeof(RectMask2D))]
    public sealed class UILineReveal : MonoBehaviour
    {
        [Tooltip("가운데 선, 마스크 밖에 둔 Image 등. 비워 두면 선 없이 열고 닫는다.")]
        [SerializeField] private Graphic line;
        [Tooltip("선 굵기(화면 픽셀)")]
        [SerializeField, Min(0.5f)] private float lineThickness = 1f;
        [SerializeField, Min(0.01f)] private float lineDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float openDuration = 0.4f;
        [Tooltip("열린 뒤 / 점으로 줄어든 뒤 선이 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float lineFadeDuration = 0.3f;

        private RectMask2D mask;
        private Sequence sequence;
        private float lineBaseAlpha = -1f;
        private float lineWidth;
        private float lineAlpha;
        private float openAmount = 1f;

        // 선이 다 뻗고 내용이 다 열리기까지 걸리는 시간
        public float OpenDuration => lineDuration + openDuration;

        public Sequence Open()
        {
            Snap(false);
            lineAlpha = 1f;
            Apply();

            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Append(DOTween.To(() => lineWidth, SetLineWidth, 1f, lineDuration).SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => openAmount, SetOpenAmount, 1f, openDuration).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => lineAlpha, SetLineAlpha, 0f, lineFadeDuration).SetEase(Ease.OutQuad));
            return sequence;
        }

        public Sequence Close()
        {
            sequence?.Kill();
            lineWidth = 1f;
            lineAlpha = 1f;
            Apply();

            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Append(DOTween.To(() => openAmount, SetOpenAmount, 0f, openDuration).SetEase(Ease.InCubic))
                .Append(DOTween.To(() => lineWidth, SetLineWidth, 0f, lineDuration).SetEase(Ease.InCubic))
                .Append(DOTween.To(() => lineAlpha, SetLineAlpha, 0f, lineFadeDuration).SetEase(Ease.OutQuad));
            return sequence;
        }

        // 연출 없이 다 열린(선 없음) 상태나 다 닫힌(선 없음) 상태로 맞춘다.
        public void Snap(bool open)
        {
            sequence?.Kill();
            sequence = null;
            openAmount = open ? 1f : 0f;
            lineWidth = open ? 1f : 0f;
            lineAlpha = 0f;
            Apply();
        }

        private void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
        }

        private void SetLineWidth(float value)
        {
            lineWidth = value;
            Apply();
        }

        private void SetOpenAmount(float value)
        {
            openAmount = value;
            Apply();
        }

        private void SetLineAlpha(float value)
        {
            lineAlpha = value;
            Apply();
        }

        private void Apply()
        {
            if (mask == null) mask = GetComponent<RectMask2D>();

            Rect rect = mask.rectTransform.rect;
            float hidden = rect.height * 0.5f * (1f - openAmount);
            mask.padding = new Vector4(0f, hidden, 0f, hidden);

            if (line == null) return;

            RectTransform lineRect = line.rectTransform;
            lineRect.position = mask.rectTransform.TransformPoint(rect.center);
            lineRect.sizeDelta = new Vector2(rect.width * lineWidth, VectorShape.PixelsToUnits(line.canvas, lineThickness));

            if (lineBaseAlpha < 0f) lineBaseAlpha = line.color.a;
            Color color = line.color;
            color.a = lineBaseAlpha * lineAlpha;
            line.color = color;
        }
    }
}
