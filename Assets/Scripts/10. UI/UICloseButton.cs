using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 창 우상단 X 버튼: 사각형은 그리지 않고 X자 선만 보이며, 그 주변(투명한 누름 영역)을 누르면 닫힌다.
    // 닫는 동작은 Button.onClick에 연결한다. 마우스를 올리면 X가 밝아지고 살짝 커진다. 실제 시간 기준이다.
    // 창마다 캔버스 배율·부모 크기가 달라도 화면에서 같은 크기·같은 거리로 보이도록 실행 중 자기 크기와 위치를 맞춘다.
    // 크기 값은 1920x1080 기준이며, 켜질 때 커지는 연출(UIPopIn)은 끝난 크기(1)로 계산해 X가 같이 흔들리지 않는다.
    // Tools → Team Odd → Add Window Close Buttons 가 만들어 붙인다.
    [RequireComponent(typeof(Button))]
    public sealed class UICloseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("X자를 이루는 선들의 부모, 마우스를 올리면 커진다")]
        [SerializeField] private RectTransform mark;
        [SerializeField] private Graphic[] strokes;
        [SerializeField] private Color normalColor = new(0.84705883f, 0.8235294f, 0.76862746f, 0.6f);
        [SerializeField] private Color hoverColor = new(0.93f, 0.91f, 0.87f, 1f);
        [SerializeField, Min(1f)] private float hoverScale = 1.15f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.12f;
        [Tooltip("닫은 뒤 커서를 다시 잠가 플레이어 조작으로 돌아간다(게임 화면의 창)")]
        [SerializeField] private bool lockCursorOnClose;
        [Tooltip("창 우상단 모서리에서 X 가운데까지(1920x1080 기준)")]
        [SerializeField, Min(0f)] private float inset = 48f;

        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private RectTransform rect;
        private Canvas canvas;

        private void Awake()
        {
            rect = (RectTransform)transform;
            // 연결된 닫기 동작(지속 호출)이 먼저 불린 뒤 커서를 잠근다
            if (lockCursorOnClose) GetComponent<Button>().onClick.AddListener(() => UIManager.Instance.LockCursor(true));
            SetHover(false, true);
        }

        private void OnDisable() => SetHover(false, true);

        private void LateUpdate()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Canvas root = canvas.rootCanvas;

            // 부모 1단위가 화면 몇 픽셀인지: 캔버스 배율 × 루트까지 부모들의 크기(UIPopIn은 끝난 크기 1로 본다)
            float parentPixels = root.scaleFactor;
            for (Transform current = rect.parent; current != null && current != root.transform; current = current.parent)
            {
                if (current.TryGetComponent(out UIPopIn _)) continue;
                parentPixels *= current.localScale.x;
            }
            if (parentPixels <= 0f) return;

            // 1920x1080 기준 1단위가 화면 몇 픽셀인지 (캔버스들이 쓰는 Expand 맞춤과 같은 계산)
            float target = Mathf.Min(Screen.width / ReferenceResolution.x, Screen.height / ReferenceResolution.y);
            float scale = target / parentPixels;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.anchoredPosition = new Vector2(-inset, -inset) * scale;
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHover(true, false);

        public void OnPointerExit(PointerEventData eventData) => SetHover(false, false);

        private void SetHover(bool hover, bool instant)
        {
            float duration = instant ? 0f : fadeDuration;
            Color color = hover ? hoverColor : normalColor;
            foreach (Graphic stroke in strokes)
            {
                if (stroke == null) continue;
                stroke.DOKill();
                if (instant) stroke.color = color;
                else stroke.DOColor(color, duration).SetUpdate(true);
            }

            if (mark == null) return;
            mark.DOKill();
            Vector3 scale = Vector3.one * (hover ? hoverScale : 1f);
            if (instant) mark.localScale = scale;
            else mark.DOScale(scale, duration).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}
