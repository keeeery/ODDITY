using TeamOdd.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamOdd
{
    // 메인 메뉴의 글자 버튼
    // - 마우스를 올리면 살짝 커지고, 왼쪽에서 강조 바가 펼쳐지며 글자가 오른쪽으로 밀리고, 짧게 지직거림(UIGlitch)
    // - 누르는 동안 글자색이 바뀌었다가 떼면 돌아오며, 클릭하면 동작
    // - 메뉴가 나타나기 전에는 상위 CanvasGroup이 입력을 막음
    // - Interactable을 끄면 글자가 회색으로 바뀌고 올리기·클릭에 반응하지 않음 (저장 데이터가 없을 때의 이어하기)
    public class TitleMenuButton : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        // 씬에 저장된 값이 바뀌지 않도록 기존 항목(Start=0, Quit=1)의 번호를 지킴
        public enum ButtonType { NewGame = 0, Quit = 1, Continue = 2, Settings = 3 }

        [SerializeField] private ButtonType buttonType;
        [SerializeField] private StartMenuUI menu;
        [SerializeField] private float hoverScaleMultiplier = 1.05f;
        [SerializeField, Min(0f)] private float hoverSpeed = 16f; // 0이면 바로 바뀜
        [SerializeField] private Color pressedColor = new(0.45f, 0.85f, 1f, 1f);
        [SerializeField] private Color disabledTint = new(0.4f, 0.4f, 0.4f, 1f); // 막혔을 때 글자색에 곱함

        [Header("Hover")]
        [SerializeField] private RectTransform highlight; // 왼쪽에서 가로로 펼쳐지는 강조 바 (피벗이 왼쪽)
        [SerializeField] private float labelShift = 18f; // 올렸을 때 글자가 오른쪽으로 밀리는 거리(px)

        [Header("Glitch")]
        [SerializeField] private UIGlitch glitch;
        [SerializeField, Min(0f)] private float glitchDuration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float glitchStrength = 0.6f;

        private TMP_Text text;
        private Vector3 originalScale;
        private Vector2 labelBase;
        private Color originalColor;
        private float hover;
        private float hoverTarget;
        private bool interactable = true;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (value == false) hoverTarget = 0f;
                if (text != null) text.color = BaseColor;
            }
        }

        private Color BaseColor => interactable ? originalColor : originalColor * disabledTint;

        private void Awake()
        {
            text = GetComponentInChildren<TMP_Text>();
            originalScale = transform.localScale;

            if (text != null)
            {
                originalColor = text.color;
                labelBase = text.rectTransform.anchoredPosition;
            }

            Apply();
        }

        // 클릭으로 씬이 바뀌는 동안 시간이 멈춰 있어도 움직이도록 실제 시간으로 진행
        private void Update()
        {
            hover = hoverSpeed > 0f
                ? Mathf.Lerp(hover, hoverTarget, 1f - Mathf.Exp(-hoverSpeed * Time.unscaledDeltaTime))
                : hoverTarget;
            Apply();
        }

        private void OnDisable()
        {
            hover = hoverTarget = 0f;
            Apply();
            if (text != null) text.color = BaseColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (interactable == false) return;
            hoverTarget = 1f;
            if (glitch != null) glitch.Play(glitchDuration, glitchStrength);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverTarget = 0f;
            if (text != null) text.color = BaseColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (interactable == false) return;
            if (text != null) text.color = new Color(pressedColor.r, pressedColor.g, pressedColor.b, originalColor.a);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (text != null) text.color = BaseColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactable == false) return;
            if (menu == null)
            {
                Debug.LogWarning("[Title] StartMenuUI가 지정되지 않았습니다.");
                return;
            }

            switch (buttonType)
            {
                case ButtonType.NewGame:
                    menu.NewGame();
                    break;

                case ButtonType.Continue:
                    menu.ContinueGame();
                    break;

                case ButtonType.Settings:
                    menu.OpenSettings();
                    break;

                case ButtonType.Quit:
                    menu.QuitGame();
                    break;
            }
        }

        private void Apply()
        {
            transform.localScale = originalScale * Mathf.Lerp(1f, hoverScaleMultiplier, hover);
            if (highlight != null) highlight.localScale = new Vector3(hover, 1f, 1f);
            if (text != null) text.rectTransform.anchoredPosition = labelBase + Vector2.right * (labelShift * hover);
        }
    }
}
