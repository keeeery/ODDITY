using System;
using System.Collections.Generic;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 패널 아래쪽의 스킬 슬롯 줄 (1~5번 키). 줄의 위치와 크기는 AugmentationChipLayoutBuilder가 정하고,
    /// 칸은 처음 열 때 Build()에서 그 폭을 슬롯 수만큼 나눠 만든다.
    /// 그리드에 스킬 칩을 놓으면 인벤토리가 빈 슬롯에 넣고, 이 줄은 SkillSlotsChanged를 받아 다시 그린다.
    /// 채워진 슬롯을 드래그해서 다른 슬롯에 놓으면 순서를 바꾼다 (다른 스킬이 있으면 서로 바꾸고, 비어 있으면 그 번호로 옮긴다).
    /// </summary>
    public class SkillSlotBarView : MonoBehaviour
    {
        [SerializeField] private float slotGap = 24f;

        private readonly List<SkillSlotView> slots = new();
        private AugmentationChipInventory inventory;
        private RectTransform root; // 드래그 중인 고스트를 올려두는 최상단 (패널)
        private Font font;
        private TooltipView tooltip; // 슬롯에 마우스를 올리면 칩 툴팁을 띄운다. 없으면 띄우지 않는다

        private SkillSlotView dragFrom; // 들고 있는 슬롯
        private OwnedChip draggedChip;  // 들어 올릴 때 그 슬롯에 있던 칩
        private SkillSlotView ghost;    // 포인터를 따라다니는 복사본
        private Vector2 dragOffset;     // 고스트 위치 - 포인터 위치

        public bool IsDragging => dragFrom != null;

        // 패널을 열 때마다 부른다. 슬롯은 처음 열 때 한 번 만든다
        public void Open(AugmentationChipInventory inventory, RectTransform root, Font font, TooltipView tooltip = null)
        {
            Close();
            this.inventory = inventory;
            this.root = root;
            this.font = font;
            this.tooltip = tooltip;

            if (slots.Count == 0)
                Build();

            inventory.SkillSlotsChanged += Refresh;
            Refresh();
        }

        public void Close()
        {
            CancelDrag();
            if (inventory != null)
                inventory.SkillSlotsChanged -= Refresh;
            inventory = null;
        }

        // 패널이 꺼지면 OnEndDrag가 오지 않으므로 여기서 고스트를 정리한다
        private void OnDisable()
        {
            CancelDrag();
        }

        // 쿨타임은 패널이 열려 있는 동안에도 흐르므로 계속 갱신한다
        private void Update()
        {
            foreach (SkillSlotView slot in slots)
                slot.RefreshCooldown();

            // 들고 있는 고스트도 아래 슬롯과 같은 시간을 보여준다
            if (ghost != null)
                ghost.RefreshCooldown();
        }

        // 줄의 폭을 슬롯 수만큼 똑같이 나눈다
        private void Build()
        {
            Vector2 size = ((RectTransform)transform).sizeDelta;
            int count = AugmentationChipInventory.SkillSlotCount;
            Vector2 slotSize = new Vector2((size.x - slotGap * (count - 1)) / count, size.y);

            for (int i = 0; i < count; i++)
            {
                SkillSlotView slot = SkillSlotView.Create(i, transform, slotSize, font, interactive: true);
                slot.Rect.anchoredPosition = new Vector2(i * (slotSize.x + slotGap), 0f);
                slot.PointerEntered += OnPointerEntered;
                slot.PointerMoved += OnPointerMoved;
                slot.PointerExited += OnPointerExited;
                slot.DragBegan += OnBeginDrag;
                slot.Dragged += OnDrag;
                slot.DragEnded += OnEndDrag;
                slots.Add(slot);
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].Show(inventory.SkillSlots[i]);
        }

        // 들고 있는 동안에는 툴팁을 띄우지 않는다
        private void OnPointerEntered(SkillSlotView slot, PointerEventData eventData)
        {
            if (slot.Chip != null && dragFrom == null)
                ChipTooltip.Show(tooltip, slot, slot.Chip, eventData.position, ChipTooltip.GetEquipStatus(slot.Chip, inventory));
        }

        private void OnPointerMoved(SkillSlotView slot, PointerEventData eventData)
        {
            if (tooltip != null)
                tooltip.SetScreenPosition(slot, eventData.position);
        }

        private void OnPointerExited(SkillSlotView slot)
        {
            if (tooltip != null)
                tooltip.Hide(slot);
        }

        #region Drag

        private void OnBeginDrag(SkillSlotView slot, PointerEventData eventData)
        {
            // 빈 슬롯은 들 것이 없다
            if (slot.Chip == null)
                return;

            dragFrom = slot;
            draggedChip = slot.Chip;

            // 들고 있는 슬롯과 같은 모습의 고스트를 같은 자리에 만들어 최상단에 올린다
            ghost = SkillSlotView.Create(slot.Index, root, slot.Rect.sizeDelta, font, interactive: false);
            ghost.Show(slot.Chip);
            ghost.Rect.position = slot.Rect.position;
            ghost.Rect.SetAsLastSibling();

            dragOffset = ghost.Rect.anchoredPosition - ScreenToLocal(root, eventData.position);
            if (tooltip != null)
                tooltip.Hide();
        }

        private void OnDrag(SkillSlotView slot, PointerEventData eventData)
        {
            if (slot != dragFrom)
                return;

            ghost.Rect.anchoredPosition = ScreenToLocal(root, eventData.position) + dragOffset;

            SkillSlotView target = FindSlotAt(eventData.position);
            foreach (SkillSlotView other in slots)
                other.SetHighlight(other == target && other != dragFrom);
        }

        private void OnEndDrag(SkillSlotView slot, PointerEventData eventData)
        {
            if (slot != dragFrom)
                return;

            // 슬롯 밖에 놓으면 아무것도 바뀌지 않는다
            // 드는 사이 슬롯 내용이 바뀌었으면(다른 경로로 장착/해제) 엉뚱한 스킬을 옮기지 않도록 취소한다
            SkillSlotView target = FindSlotAt(eventData.position);
            int from = dragFrom.Index;
            bool unchanged = inventory.SkillSlots[from] == draggedChip;
            CancelDrag();

            if (target != null && unchanged)
                inventory.SwapSkillSlots(from, target.Index);
        }

        private void CancelDrag()
        {
            if (ghost != null)
                Destroy(ghost.gameObject);

            ghost = null;
            dragFrom = null;
            draggedChip = null;
            foreach (SkillSlotView slot in slots)
                slot.SetHighlight(false);
        }

        // 포인터 아래에 있는 슬롯. 없으면 null
        private SkillSlotView FindSlotAt(Vector2 screenPosition)
        {
            foreach (SkillSlotView slot in slots)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(slot.Rect, screenPosition, null))
                    return slot;
            }

            return null;
        }

        #endregion

        // Screen Space - Overlay 캔버스 전제라 카메라 없이(null) 변환한다 (AugmentationChipPanel과 같은 방식)
        private static Vector2 ScreenToLocal(RectTransform rect, Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out Vector2 local);
            return local;
        }
    }
}
