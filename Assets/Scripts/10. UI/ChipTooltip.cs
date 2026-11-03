using System.Collections.Generic;
using System.Text.RegularExpressions;
using TeamOdd.Superpower;
using UnityEngine;

namespace TeamOdd.UI
{
    // 증강 칩 하나의 툴팁 내용과 헤더 오른쪽의 칩 모양, 보상 화면과 증강칩 패널이 같이 쓴다.
    // 헤더 왼쪽 아이콘에는 칩 아이콘(IconSprite)을, 칩 모양에는 단위 칸 스프라이트(UnitSprite)를 쓴다.
    public sealed class ChipTooltip : ITooltipAccessory
    {
        private readonly OwnedChip chip;

        public ChipTooltip(OwnedChip chip) => this.chip = chip;

        // status는 부제목 끝에 붙는다(장착 상태 등). 비워 두면 종류·등급만 쓴다.
        public static void Show(TooltipView tooltip, object owner, OwnedChip chip, Vector2 screenPosition, string status = null)
        {
            if (tooltip == null || chip == null) return;
            tooltip.Show(owner, CreateContent(chip, status), screenPosition, new ChipTooltip(chip));
        }

        public static TooltipContent CreateContent(OwnedChip chip, string status = null)
        {
            AugmentationChipDef def = chip.Def;
            string type = $"{(def is SkillChipDef ? "스킬" : "스탯")} 칩 · {def.CellCount}등급";
            if (string.IsNullOrEmpty(status) == false) type += $" · {status}";
            List<string> lines = new List<string>();
            if (def is SkillChipDef skill)
            {
                List<string> costs = new List<string>();
                if (IsZero(skill.Cooldown) == false) costs.Add($"쿨타임 {Accent($"{skill.Cooldown:0.#}초")}");
                if (IsZero(skill.Cost) == false) costs.Add($"스태미나 {Accent($"{skill.Cost:0.#}")}");
                if (costs.Count > 0) lines.Add(string.Join(" · ", costs));
                string detail = WithoutZeroParts(chip.Skill.Describe());
                if (string.IsNullOrEmpty(detail) == false) lines.Add(detail);
            }
            // 능력치 칩은 설명에 오르는 스탯과 수치가 이미 적혀 있어 따로 쓰지 않는다

            string body = lines.Count > 0 ? $"{def.Description}\n\n{string.Join("\n", lines)}" : def.Description;
            return new TooltipContent(def.Name, type, body, def.IconSprite);
        }

        private static readonly Regex Number = new Regex(@"\d+(\.\d+)?");

        // 툴팁에 찍히는 자릿수(소수 한 자리)로 0이면 0으로 본다.
        private static bool IsZero(float value) => $"{value:0.#}" == "0";

        // 스킬 설명은 " · "로 이은 항목들이라, 숫자가 있는데 모두 0인 항목(간격 0초 등)만 뺀다. 숫자가 없는 문장은 그대로 둔다.
        private static string WithoutZeroParts(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            List<string> kept = new List<string>();
            foreach (string part in text.Split(" · "))
            {
                MatchCollection numbers = Number.Matches(part);
                bool allZero = numbers.Count > 0;
                foreach (Match number in numbers)
                    if (float.Parse(number.Value, System.Globalization.CultureInfo.InvariantCulture) != 0f) allZero = false;
                if (allZero == false) kept.Add(part);
            }
            return string.Join(" · ", kept);
        }

        // 본문 속 수치를 HUD 강조 금색(0.769, 0.576, 0.290)으로 칠한다.
        private static string Accent(string value) => $"<color=#C4934A>{value}</color>";

        // 증강칩 패널에서 부제목에 붙이는 장착 상태, 스킬 칩이면 슬롯 번호까지 쓴다.
        public static string GetEquipStatus(OwnedChip chip, AugmentationChipInventory inventory)
        {
            if (chip.IsEquipped == false) return "미장착";
            int skillSlot = inventory != null ? inventory.GetSkillSlot(chip) : -1;
            return skillSlot >= 0 ? $"장착됨 · 스킬 {skillSlot + 1}번" : "장착됨";
        }

        public bool FillAccessory(RectTransform slot) => FillShape(chip, slot);

        // 지급할 때 정해진 모양을 툴팁 헤더 오른쪽에 그린다.
        public static bool FillShape(OwnedChip chip, RectTransform slot)
        {
            if (chip == null || chip.Cells == null || chip.Cells.Length == 0) return false;

            ChipShapeGraphic shape = slot.GetComponentInChildren<ChipShapeGraphic>(true);
            if (shape == null)
            {
                Debug.LogWarning("[ChipTooltip] 툴팁 슬롯에 ChipShapeGraphic이 없어 칩 모양을 표시하지 못했습니다.", slot);
                return false;
            }

            shape.gameObject.SetActive(true);
            shape.Cells = chip.Cells;
            shape.CellSprite = chip.Def.UnitSprite;
            return true;
        }
    }
}
