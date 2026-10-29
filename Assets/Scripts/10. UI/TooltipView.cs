using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 툴팁에 띄울 내용, 보여 주는 쪽이 채워서 넘긴다. 부제목·본문·아이콘은 비워 두면 숨긴다.
    public readonly struct TooltipContent
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string Body;
        public readonly Sprite Icon;

        public TooltipContent(string title, string subtitle = null, string body = null, Sprite icon = null)
        {
            Title = title;
            Subtitle = subtitle;
            Body = body;
            Icon = icon;
        }
    }

    // 마우스를 올린 대상의 정보를 띄우는 공용 툴팁, 포인터를 따라가며 화면 밖으로 나가지 않게 위치를 잡는다.
    // 마지막으로 띄운 대상(owner)만 위치를 옮기거나 숨길 수 있어서, 대상 사이를 옮겨 다녀도 늦게 온 나가기 이벤트에 꺼지지 않는다.
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform tooltipRect;
        [FormerlySerializedAs("nameText")]
        [SerializeField] private TMP_Text titleText;
        [FormerlySerializedAs("typeText")]
        [SerializeField] private TMP_Text subtitleText;
        [FormerlySerializedAs("descriptionText")]
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image iconImage;
        [Tooltip("헤더 오른쪽 끝의 빈 슬롯, ITooltipAccessory가 채운다. 채우지 않으면 숨긴다.")]
        [SerializeField] private RectTransform accessorySlot;
        [SerializeField, Min(0f)] private float pointerPadding = 16f;

        private Canvas rootCanvas;
        private object activeOwner;
        private readonly Vector3[] corners = new Vector3[4];

        private void Awake()
        {
            rootCanvas = tooltipRect.GetComponentInParent<Canvas>(true).rootCanvas;
            Hide();
        }

        public void Show(object owner, in TooltipContent content, Vector2 screenPosition, ITooltipAccessory accessory = null)
        {
            activeOwner = owner;
            titleText.text = content.Title;
            SetText(subtitleText, content.Subtitle);
            SetText(bodyText, content.Body);
            if (iconImage != null)
            {
                iconImage.sprite = content.Icon;
                iconImage.gameObject.SetActive(content.Icon != null);
            }
            FillAccessory(accessory);

            tooltipRect.gameObject.SetActive(true);
            tooltipRect.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
            Canvas.ForceUpdateCanvases();
            SetScreenPosition(owner, screenPosition);
        }

        public void Hide(object owner)
        {
            if (activeOwner == owner) Hide();
        }

        public void Hide()
        {
            activeOwner = null;
            if (tooltipRect != null) tooltipRect.gameObject.SetActive(false);
        }

        public void SetScreenPosition(object owner, Vector2 screenPosition)
        {
            if (activeOwner != owner || rootCanvas == null) return;
            Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            tooltipRect.GetWorldCorners(corners);
            float width = Vector2.Distance(RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]),
                RectTransformUtility.WorldToScreenPoint(uiCamera, corners[3]));
            float height = Vector2.Distance(RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]),
                RectTransformUtility.WorldToScreenPoint(uiCamera, corners[1]));

            Rect bounds = rootCanvas.pixelRect;
            bool right = screenPosition.x + pointerPadding + width <= bounds.xMax;
            bool below = screenPosition.y - pointerPadding - height >= bounds.yMin;
            Vector2 position = new Vector2(
                screenPosition.x + (right ? 1f : -1f) * (pointerPadding + width * 0.5f),
                screenPosition.y + (below ? -1f : 1f) * (pointerPadding + height * 0.5f));
            float halfWidth = Mathf.Min(width, bounds.width) * 0.5f;
            float halfHeight = Mathf.Min(height, bounds.height) * 0.5f;
            position.x = Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            position.y = Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)tooltipRect.parent, position, uiCamera, out Vector3 worldPosition))
                tooltipRect.position = worldPosition;
        }

        // 앞서 띄운 대상이 켜 둔 내용은 끄고 새 대상에게 채우게 한다. 채운 것이 없으면 슬롯째 숨긴다.
        private void FillAccessory(ITooltipAccessory accessory)
        {
            if (accessorySlot == null) return;

            foreach (Transform child in accessorySlot)
                child.gameObject.SetActive(false);
            accessorySlot.gameObject.SetActive(accessory != null && accessory.FillAccessory(accessorySlot));
        }

        // 빈 줄이 레이아웃 간격을 차지하지 않도록 오브젝트째 끈다.
        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            text.text = value;
            text.gameObject.SetActive(string.IsNullOrEmpty(value) == false);
        }

        private void OnDisable() => Hide();
    }
}
