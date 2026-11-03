using TeamOdd.Combat;
using TeamOdd.Superpower;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    [DisallowMultipleComponent]
    public sealed class RewardIconView : MonoBehaviour, ITooltipSource, ITooltipAccessory
    {
        // 칩 계열의 밝은 색. 스탯 아이콘 색(빨강·파랑·노랑)에 맞추고, 보라는 칸 색(ChipUnit_Purple)을 같은 밝기로 올렸다.
        private static readonly Color Red = new(0.92f, 0.35f, 0.38f, 1f);
        private static readonly Color Blue = new(0.48f, 0.5f, 0.92f, 1f);
        private static readonly Color Yellow = new(0.92f, 0.92f, 0.38f, 1f);
        private static readonly Color Purple = new(0.7f, 0.36f, 0.92f, 1f);
        // 계열이 없는 칩(방어력 등)은 HUD 글자색으로 두른다.
        private static readonly Color Neutral = new(0.93f, 0.91f, 0.87f, 1f);

        [SerializeField] private Image icon;
        [Tooltip("칩 대표 아이콘(IconSprite), 비워 두면 칸 그림만 보인다.")]
        [SerializeField] private Image chipIcon;
        [Tooltip("칩 계열색 외곽선, 연결하면 칸 그림(Icon)은 그리지 않는다.")]
        [SerializeField] private RawImage colorBorder;
        [Tooltip("외곽선 안쪽 바탕, 스킬 칩은 칸 그림(UnitSprite)으로 칠하고 스탯 칩은 검게 둔다.")]
        [SerializeField] private Image colorBack;

        public OwnedChip Chip { get; private set; }

        // 스킬 칩: 계열색 외곽선 + 칸 색 바탕 + 흰 아이콘, 스탯 칩: 계열색 외곽선 + 검은 바탕 + 계열색 아이콘
        public void Show(OwnedChip chip)
        {
            Chip = chip;
            Sprite unit = chip != null ? chip.Def.UnitSprite : null;
            if (colorBorder != null)
            {
                icon.sprite = null;
                colorBorder.texture = null;
                colorBorder.color = chip != null ? GetFamilyColor(chip.Def) : Neutral;
                colorBorder.enabled = chip != null;
            }
            else icon.sprite = unit;
            if (colorBack != null)
            {
                bool isSkill = chip != null && chip.Def is SkillChipDef;
                colorBack.sprite = isSkill ? unit : null;
                colorBack.color = isSkill && unit != null ? Color.white : Color.black;
            }
            if (chipIcon != null)
            {
                chipIcon.sprite = chip != null ? chip.Def.IconSprite : null;
                chipIcon.enabled = chipIcon.sprite != null;
            }
            gameObject.SetActive(chip != null);
        }

        public bool TryGetTooltip(out TooltipContent content)
        {
            if (Chip == null)
            {
                content = default;
                return false;
            }

            content = ChipTooltip.CreateContent(Chip);
            return true;
        }

        // 스킬 칩은 분류(공격 빨강, 방어 파랑, 유틸 노랑, 패시브 보라), 스탯 칩은 아이콘 색(공격력·사거리 빨강, 체력 파랑, 염동력·스태미나 노랑)을 따른다.
        private static Color GetFamilyColor(AugmentationChipDef def)
        {
            return def switch
            {
                SkillChipDef skill => skill.Category switch
                {
                    SkillCategory.Attack => Red,
                    SkillCategory.Defense => Blue,
                    SkillCategory.Utility => Yellow,
                    SkillCategory.Passive => Purple,
                    _ => Neutral,
                },
                StatChipDef stat => stat.StatType switch
                {
                    StatType.Atk or StatType.AttackRange => Red,
                    StatType.MaxHp => Blue,
                    StatType.PsychoMaxMass or StatType.MaxStamina => Yellow,
                    _ => Neutral,
                },
                _ => Neutral,
            };
        }

        // 지급할 때 정해진 모양을 툴팁 헤더 오른쪽에 그린다.
        public bool FillAccessory(RectTransform slot) => ChipTooltip.FillShape(Chip, slot);
    }
}
