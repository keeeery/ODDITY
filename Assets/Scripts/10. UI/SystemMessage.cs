using DG.Tweening;
using TMPro;
using UnityEngine;

namespace TeamOdd.UI
{
    // 기상기 슬롯 위에 잠깐 떠오르는 시스템 안내 한 줄. HUD 프리팹에 들어 있어 어느 씬에서나 SystemMessage.Show로 띄운다.
    // 같은 문구를 연달아 띄우면(쿨타임 중 키 연타 등) 새로 나타나지 않고 표시 시간만 늘리며 살짝 튄다.
    // 다른 문구가 오면 바로 바꿔 띄운다. 시간 정지 중에도 사라지도록 실제 시간 기준이다.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SystemMessage : MonoBehaviour
    {
        public enum Tone
        {
            Normal,  // 안내 (퀘스트 수락 등)
            Warning, // 할 수 없는 행동 (재사용 대기 등)
        }

        private static SystemMessage current;

        [SerializeField] private TMP_Text text;
        [SerializeField] private Color normalColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        [SerializeField] private Color warningColor = new(0.7686275f, 0.5764706f, 0.2901961f, 1f);

        [Header("Timing (초, 실제 시간)")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
        [SerializeField, Min(0f)] private float holdDuration = 1.8f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.4f;
        [Tooltip("나타날 때 아래에서 올라오는 거리(px)")]
        [SerializeField] private float rise = 8f;
        [Tooltip("같은 문구를 다시 띄울 때 커졌다 돌아오는 정도")]
        [SerializeField, Min(0f)] private float repeatPunch = 0.06f;
        [Tooltip("다른 Overlay 캔버스(증강칩 패널 등)에 가리지 않도록 이 메시지만 따로 올려 그리는 순서")]
        [SerializeField] private int sortingOrder = 100;

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 basePosition;
        private Sequence sequence;
        private Tween punch;
        private string shownMessage;

        // 지금 HUD가 없으면(타이틀 등) 로그로만 남긴다.
        public static void Show(string message, Tone tone = Tone.Normal)
        {
            if (current == null)
            {
                Debug.Log($"[SystemMessage] {message}");
                return;
            }
            current.Display(message, tone);
        }

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
            if (text == null) text = GetComponentInChildren<TMP_Text>(true);
            // 밝은 바닥·벽 위에서도 읽히도록 바깥 외곽선과 그림자를 켠 머티리얼로 바꾼다
            ReadableTextStyle.Apply(text);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        // HUD 캔버스와 UI 캔버스가 같은 정렬 순서라 열린 창 뒤로 숨는다. 메시지만 하위 캔버스로 빼서 위에 그린다.
        // 하위 캔버스의 정렬 덮어쓰기는 켜져 있을 때 지정해야 남는다.
        private void OnEnable()
        {
            current = this;
            if (TryGetComponent(out Canvas canvas) == false) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        private void OnDisable()
        {
            if (current == this) current = null;
            sequence?.Kill();
            punch?.Kill(true);
            group.alpha = 0f;
            rect.anchoredPosition = basePosition;
            shownMessage = null;
        }

        private void Display(string message, Tone tone)
        {
            bool repeat = message == shownMessage && group.alpha > 0.01f;
            shownMessage = message;
            text.SetText(message);
            text.color = tone == Tone.Warning ? warningColor : normalColor;

            sequence?.Kill();
            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            if (repeat)
            {
                group.alpha = 1f;
                rect.anchoredPosition = basePosition;
                punch?.Kill(true);
                punch = rect.DOPunchScale(Vector3.one * repeatPunch, 0.2f, 1, 0f).SetUpdate(true).SetTarget(this);
            }
            else
            {
                group.alpha = 0f;
                rect.anchoredPosition = basePosition - new Vector2(0f, rise);
                sequence.Join(group.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad))
                    .Join(rect.DOAnchorPos(basePosition, fadeInDuration).SetEase(Ease.OutCubic));
            }

            sequence.AppendInterval(holdDuration)
                .Append(group.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad))
                .OnComplete(() => shownMessage = null);
        }
    }
}
