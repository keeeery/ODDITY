using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 임무 성공·실패 화면의 제목 강조 연출: 윗줄(MISSION COMPLETE 등)이 한 글자씩 찍히고, 제목이 흰빛으로 번쩍이며 살짝 줄어들어 자리 잡고,
    // 장식 선들이 기준점 쪽에서 뻗어 나간다. 진행 막대가 있으면 PlayProgress로 가운데로 줄어들게 한다(사망 화면 카운트다운).
    // 선의 길이는 씬에 놓인 가로 크기를 그대로 쓴다. 실제 시간 기준이다.
    // Tools → Team Odd → Enhance Result Screens 가 만들어 붙인다.
    [DisallowMultipleComponent]
    public sealed class ResultBanner : MonoBehaviour
    {
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private Graphic title;
        [Tooltip("기준점 쪽에서 가로로 뻗는 장식 선")]
        [SerializeField] private RectTransform[] lines;
        [Tooltip("PlayProgress로 가로 크기가 1→0으로 줄어드는 막대, 비워 두면 쓰지 않음")]
        [SerializeField] private RectTransform progress;

        [SerializeField, Min(0.01f)] private float kickerDuration = 0.35f;
        [SerializeField, Min(1f)] private float titleScale = 1.12f;
        [SerializeField, Min(0.01f)] private float titleDuration = 0.45f;
        [SerializeField] private Color titleFlashColor = Color.white;
        [SerializeField, Min(0f)] private float lineDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float lineDuration = 0.6f;

        private float[] lineLengths;
        private Color titleColor;
        private bool prepared;
        private Sequence sequence;
        private Tween progressTween;

        public Sequence Play()
        {
            Snap(false);

            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            if (kicker != null)
            {
                int count = kicker.textInfo.characterCount > 0 ? kicker.textInfo.characterCount : kicker.text.Length;
                sequence.Insert(0f, DOVirtual.Int(0, count, kickerDuration, value => kicker.maxVisibleCharacters = value).SetEase(Ease.Linear));
            }
            if (title != null)
            {
                sequence.Insert(0f, title.rectTransform.DOScale(1f, titleDuration).SetEase(Ease.OutCubic));
                sequence.Insert(0f, title.DOColor(titleColor, titleDuration).SetEase(Ease.OutQuad));
            }
            for (int i = 0; i < lines.Length; i++)
            {
                RectTransform line = lines[i];
                float length = lineLengths[i];
                sequence.Insert(lineDelay, DOTween.To(() => line.sizeDelta.x, value => SetWidth(line, value), length, lineDuration)
                    .SetEase(Ease.OutExpo));
            }
            return sequence;
        }

        // 막대를 duration 동안 가운데로 줄인다.
        public Tween PlayProgress(float duration)
        {
            if (progress == null) return null;
            progressTween?.Kill();
            progress.localScale = Vector3.one;
            progressTween = progress.DOScaleX(0f, duration).SetEase(Ease.Linear).SetUpdate(true).SetTarget(this);
            return progressTween;
        }

        // 연출 없이 다 드러난 상태나 시작 전 상태로 맞춘다.
        public void Snap(bool shown)
        {
            Prepare();
            sequence?.Kill();
            sequence = null;
            progressTween?.Kill();
            progressTween = null;

            if (kicker != null) kicker.maxVisibleCharacters = shown ? 99999 : 0;
            if (title != null)
            {
                title.rectTransform.localScale = Vector3.one * (shown ? 1f : titleScale);
                title.color = shown ? titleColor : titleFlashColor;
            }
            for (int i = 0; i < lines.Length; i++)
                SetWidth(lines[i], shown ? lineLengths[i] : 0f);
            if (progress != null) progress.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            if (sequence != null || progressTween != null) Snap(true);
        }

        private void Prepare()
        {
            if (prepared) return;
            prepared = true;

            lines ??= new RectTransform[0];
            lineLengths = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                lineLengths[i] = lines[i].sizeDelta.x;
            if (title != null) titleColor = title.color;
        }

        private static void SetWidth(RectTransform rect, float width)
        {
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }
    }
}
