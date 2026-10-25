using System.Collections.Generic;
using TeamOdd.Superpower;
using TMPro;
using UnityEngine;

namespace TeamOdd.UI
{
    // 화면 하단 중앙의 액티브 스킬 슬롯(1~5번), 장착한 스킬 이름과 남은 쿨타임을 보여준다.
    // 슬롯 수가 고정이라 슬롯은 런타임에 자식으로 만들고, 이 오브젝트 크기를 슬롯 줄에 맞춘다.
    // 장착·해제·순서 변경은 인벤토리의 SkillSlotsChanged로 다시 그리고, 쿨타임은 매 프레임 스킬에서 읽는다.
    public class SkillSlotHUD : MonoBehaviour
    {
        // 슬롯 하나에 붙는 그래픽과 글자
        private class Slot
        {
            public SkillSlotGraphic Graphic;
            public TMP_Text Key;
            public TMP_Text Name;
            public TMP_Text Cooldown;
            public OwnedChip Chip;
            public bool WasCoolingDown;
        }

        [Header("References")]
        [Tooltip("비어 있으면 씬에서 찾는다.")]
        [SerializeField] private AugmentationChipInventory inventory;
        [SerializeField] private TMP_FontAsset font;

        [Header("Layout")]
        [SerializeField] private Vector2 slotSize = new(54f, 54f);
        [SerializeField, Min(0f)] private float slotGap = 8f;

        [Header("Style")]
        [SerializeField] private SkillSlotStyle style = new();
        [SerializeField] private Color textColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        [SerializeField, Min(1f)] private float keyFontSize = 11f;
        [SerializeField, Min(1f)] private float nameFontSize = 13f;
        [SerializeField, Min(1f)] private float cooldownFontSize = 15f;
        [Tooltip("이름 앞에서부터 보여줄 글자 수, 아이콘이 생기기 전까지 이름 첫 단어로 슬롯을 구분한다.")]
        [SerializeField, Min(1)] private int nameLength = 3;

        [Header("Ready Flash")]
        [Tooltip("쿨타임이 끝나는 순간 슬롯이 하얗게 번쩍였다 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float readyFlashDuration = 0.3f;

        private readonly List<Slot> slots = new();

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<AugmentationChipInventory>();
            if (inventory == null) Debug.LogWarning($"{name}: 씬에 AugmentationChipInventory가 없어 스킬 슬롯을 비워 둡니다.", this);

            Build();
        }

        // 인벤토리는 플레이어에 있어 이 HUD보다 오래 살 수 있으므로 꺼질 때 구독을 끊는다.
        private void OnEnable()
        {
            if (inventory == null) return;

            inventory.SkillSlotsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.SkillSlotsChanged -= Refresh;
        }

        private void Update()
        {
            foreach (Slot slot in slots)
                UpdateCooldown(slot);
        }

        private void Build()
        {
            int count = AugmentationChipInventory.SkillSlotCount;
            ((RectTransform)transform).sizeDelta = new Vector2(count * slotSize.x + (count - 1) * slotGap, slotSize.y);

            for (int index = 0; index < count; index++)
            {
                RectTransform rect = CreateRect($"SkillSlot_{index + 1}", transform);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = slotSize;
                rect.anchoredPosition = new Vector2(index * (slotSize.x + slotGap), 0f);

                SkillSlotGraphic graphic = rect.gameObject.AddComponent<SkillSlotGraphic>();
                graphic.raycastTarget = false;
                graphic.SetStyle(style);

                Slot slot = new Slot
                {
                    Graphic = graphic,
                    Key = CreateText("Key", rect, keyFontSize, TextAlignmentOptions.BottomLeft, new Vector4(4f, 2f, 4f, 2f)),
                    Name = CreateText("Name", rect, nameFontSize, TextAlignmentOptions.Center, Vector4.zero),
                    Cooldown = CreateText("Cooldown", rect, cooldownFontSize, TextAlignmentOptions.Center, Vector4.zero),
                };
                slot.Key.text = SkillCaster.GetKeyLabel(index);
                slots.Add(slot);
            }
        }

        private void Refresh()
        {
            for (int index = 0; index < slots.Count; index++)
            {
                Slot slot = slots[index];
                slot.Chip = inventory.SkillSlots[index];
                slot.Graphic.HasSkill = slot.Chip != null;
                slot.Name.text = slot.Chip != null ? GetShortName(slot.Chip.Def.Name) : "";
                // 장착하는 순간 쿨타임이 이미 끝나 있으면 번쩍이는 연출 없이 바로 사용 가능 상태로 둔다.
                slot.WasCoolingDown = slot.Chip != null && slot.Chip.Skill.IsReady == false;
                slot.Graphic.FlashAlpha = 0f;
                UpdateCooldown(slot);
            }
        }

        private void UpdateCooldown(Slot slot)
        {
            SuperpowerSkill skill = slot.Chip?.Skill;
            float remaining = skill != null ? skill.RemainingCooldown : 0f;
            bool coolingDown = remaining > 0f;

            slot.Graphic.CooldownRatio = skill != null && skill.Cooldown > 0f ? remaining / skill.Cooldown : 0f;
            slot.Cooldown.text = coolingDown ? Mathf.CeilToInt(remaining).ToString() : "";

            // 쿨타임 중에는 이름을 흐리게 깔고 남은 초를 위에 띄운다.
            slot.Name.alpha = coolingDown ? 0.45f : 1f;
            slot.Key.alpha = slot.Chip != null ? 0.55f : 0.3f;

            if (slot.WasCoolingDown && coolingDown == false) slot.Graphic.FlashAlpha = 1f;
            else if (slot.Graphic.FlashAlpha > 0f) slot.Graphic.FlashAlpha -= Time.deltaTime / readyFlashDuration;
            slot.WasCoolingDown = coolingDown;
        }

        // "시간 정지 칩" → "시간", 첫 단어를 nameLength 글자까지
        private string GetShortName(string chipName)
        {
            string firstWord = chipName.Trim().Split(' ')[0];
            return firstWord.Length > nameLength ? firstWord[..nameLength] : firstWord;
        }

        private TMP_Text CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment, Vector4 margin)
        {
            RectTransform rect = CreateRect(objectName, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = alignment;
            text.margin = margin;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
