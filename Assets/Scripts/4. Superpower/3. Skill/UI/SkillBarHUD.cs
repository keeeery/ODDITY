using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 게임 화면의 스킬 바 (읽기 전용). 1~5번 슬롯의 칩 이름과 남은 쿨타임을 보여준다.
    /// Screen Space - Overlay 캔버스 아래 빈 오브젝트에 붙이고 앵커를 원하는 곳(예: 아래 가운데)에 둔다. 크기는 슬롯 수에 맞춰 여기서 정한다.
    /// 증강칩 패널(AugmentationChipDemo) 아래에 두면 패널이 닫힐 때 같이 꺼지므로 패널 밖에 둔다.
    /// 순서 변경은 증강칩 패널의 스킬 슬롯 줄에서 하고, 이쪽은 인벤토리의 SkillSlotsChanged를 받아 다시 그리기만 한다.
    /// </summary>
    public class SkillBarHUD : MonoBehaviour
    {
        [SerializeField] private AugmentationChipInventory inventory; // 비워두면 씬에서 찾는다
        [SerializeField] private Font font;                            // 비워두면 Unity 기본 폰트
        [SerializeField] private Vector2 slotSize = new(150f, 56f);
        [SerializeField] private float slotGap = 8f;

        private readonly List<SkillSlotView> slots = new();

        private void Awake()
        {
            if (inventory == null)
                inventory = FindAnyObjectByType<AugmentationChipInventory>();
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (inventory == null)
                Debug.LogWarning("[SkillBarHUD] 씬에 AugmentationChipInventory가 없습니다.", this);

            Build();
        }

        // 인벤토리는 플레이어에 있어 이 HUD보다 오래 살 수 있으므로, 꺼질 때 반드시 구독을 끊는다
        private void OnEnable()
        {
            if (inventory == null)
                return;

            inventory.SkillSlotsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.SkillSlotsChanged -= Refresh;
        }

        private void Update()
        {
            foreach (SkillSlotView slot in slots)
                slot.RefreshCooldown();
        }

        // 슬롯을 왼쪽부터 한 줄로 깔고, 이 오브젝트 크기를 그 줄에 맞춘다
        private void Build()
        {
            int count = AugmentationChipInventory.SkillSlotCount;
            ((RectTransform)transform).sizeDelta = new Vector2(count * slotSize.x + (count - 1) * slotGap, slotSize.y);

            for (int i = 0; i < count; i++)
            {
                SkillSlotView slot = SkillSlotView.Create(i, transform, slotSize, font, interactive: false);
                slot.Rect.anchoredPosition = new Vector2(i * (slotSize.x + slotGap), 0f);
                slots.Add(slot);
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].Show(inventory.SkillSlots[i]);
        }
    }
}
