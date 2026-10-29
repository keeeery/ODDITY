using TeamOdd.Superpower;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    [DisallowMultipleComponent]
    public sealed class RewardIconView : MonoBehaviour, ITooltipSource, ITooltipAccessory
    {
        [SerializeField] private Image icon;

        public OwnedChip Chip { get; private set; }

        public void Show(OwnedChip chip)
        {
            Chip = chip;
            icon.sprite = chip != null ? chip.Def.UnitSprite : null;
            gameObject.SetActive(chip != null);
        }

        public bool TryGetTooltip(out TooltipContent content)
        {
            if (Chip == null)
            {
                content = default;
                return false;
            }

            AugmentationChipDef def = Chip.Def;
            string type = $"{(def is SkillChipDef ? "스킬" : "스탯")} 칩 · {def.CellCount}등급";
            string body = def is SkillChipDef skill
                ? $"{def.Description}\n\n쿨타임 {skill.Cooldown:0.#}초 · 스태미나 {skill.Cost:0.#}\n{Chip.Skill.Describe()}"
                : $"{def.Description}\n\n{((StatChipDef)def).StatType} +{((StatChipDef)def).Value}";
            content = new TooltipContent(def.Name, type, body, def.UnitSprite);
            return true;
        }

        // 지급할 때 정해진 모양을 툴팁 헤더 오른쪽에 그린다.
        public bool FillAccessory(RectTransform slot)
        {
            if (Chip == null || Chip.Cells == null || Chip.Cells.Length == 0) return false;

            ChipShapeGraphic shape = slot.GetComponentInChildren<ChipShapeGraphic>(true);
            if (shape == null)
            {
                Debug.LogWarning("[RewardIconView] 툴팁 슬롯에 ChipShapeGraphic이 없어 칩 모양을 표시하지 못했습니다.", slot);
                return false;
            }

            shape.gameObject.SetActive(true);
            shape.Cells = Chip.Cells;
            shape.CellSprite = Chip.Def.UnitSprite;
            return true;
        }
    }
}
