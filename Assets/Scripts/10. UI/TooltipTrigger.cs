using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamOdd.UI
{
    // 툴팁 내용을 넘겨 주는 쪽, 보여 줄 것이 없으면 false를 돌려 툴팁을 띄우지 않는다.
    public interface ITooltipSource
    {
        bool TryGetTooltip(out TooltipContent content);
    }

    // 툴팁 헤더 오른쪽 슬롯에 글자로 못 담는 것(칩 모양 등)을 그리는 쪽, ITooltipSource와 함께 구현한다.
    // 슬롯의 자식은 툴팁이 미리 꺼 두므로 쓸 자식만 켜고, 채운 것이 없으면 false를 돌려 슬롯을 숨긴다.
    public interface ITooltipAccessory
    {
        bool FillAccessory(RectTransform slot);
    }

    // 마우스를 올리면 source의 내용으로 툴팁을 띄우고, 벗어나거나 꺼지면 숨긴다.
    public sealed class TooltipTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Tooltip("ITooltipSource를 구현한 컴포넌트, 비워 두면 이 오브젝트와 부모에서 찾는다.")]
        [SerializeField] private MonoBehaviour source;
        [SerializeField] private TooltipView tooltip;

        private ITooltipSource tooltipSource;

        private void Awake()
        {
            tooltipSource = source as ITooltipSource ?? GetComponentInParent<ITooltipSource>(true);
            if (tooltipSource == null) Debug.LogError($"[TooltipTrigger] {name}: ITooltipSource를 찾지 못했습니다.", this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null && tooltipSource != null && tooltipSource.TryGetTooltip(out TooltipContent content))
                tooltip.Show(this, content, eventData.position, tooltipSource as ITooltipAccessory);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Hide(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.SetScreenPosition(this, eventData.position);
        }

        private void OnDisable()
        {
            if (tooltip != null) tooltip.Hide(this);
        }
    }
}
