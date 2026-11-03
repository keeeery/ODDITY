using System.Collections.Generic;
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
                // 자동 발동 스킬은 키로 다시 쓰는 게 아니라 다시 발동하기까지의 시간이다
                string cooldownLabel = chip.Skill is PassiveSkill ? "재발동 대기시간" : "재사용 대기시간";
                if (chip.Skill.IsOncePerBattle) costs.Add($"전투당 {Accent("1회")}만 발동");
                else if (IsZero(skill.Cooldown) == false) costs.Add($"{cooldownLabel} {Accent($"{skill.Cooldown:0.#}초")}");
                if (IsZero(skill.Cost) == false) costs.Add($"스태미나 {Accent($"{skill.Cost:0.#}")}");
                if (costs.Count > 0) lines.Add(string.Join(" · ", costs));
            }
            // 세부 수치는 스킬 설명에 이미 적혀 있어 따로 쓰지 않는다.
            // 능력치 칩은 설명에 오르는 스탯과 수치가 이미 적혀 있어 따로 쓰지 않는다

            string body = lines.Count > 0 ? $"{def.Description}\n\n{string.Join("\n", lines)}" : def.Description;
            return new TooltipContent(def.Name, type, body, def.IconSprite);
        }

        // 툴팁에 찍히는 자릿수(소수 한 자리)로 0이면 0으로 본다.
        private static bool IsZero(float value) => $"{value:0.#}" == "0";

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
