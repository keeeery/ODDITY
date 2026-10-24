using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 증강칩 목록과 장착 기록. Player 오브젝트(CombatEntity가 있는 곳)에 붙인다.
    /// 칩을 얻는 쪽(보상, 드랍 등)이 Add로 넣고, 장착 UI가 Equip/Unequip을 부른다.
    /// 장착/해제할 때 칩의 StatType/Value만큼 같은 오브젝트의 CombatEntity.CurrentStats에 보너스를 더하고 뺀다.
    /// 그리드 칸 밖이나 겹치는 자리인지는 칩을 놓는 쪽(ChipGridView)이 확인한다.
    /// </summary>
    public class AugmentationChipInventory : MonoBehaviour
    {
        private readonly List<OwnedChip> chips = new();
        private CombatEntity owner;

        public IReadOnlyList<OwnedChip> Chips => chips;

        // 패널이 현재 스탯과 칩 보너스를 보여줄 때 읽는다
        public RuntimeStats Stats => owner.CurrentStats;

        private void Awake()
        {
            owner = GetComponent<CombatEntity>();
        }

        public void Add(AugmentationChipDef def)
        {
            chips.Add(new OwnedChip(def));
        }

        // 장착 기록을 남기고 스탯 보너스를 더한다
        public void Equip(OwnedChip chip, Vector2Int origin, int rotation)
        {
            if (chip.IsEquipped)
                return;

            chip.IsEquipped = true;
            chip.Origin = origin;
            chip.Rotation = rotation;
            owner.CurrentStats.Add(chip.Def.StatType, chip.Def.Value);
        }

        public void Unequip(OwnedChip chip)
        {
            if (chip.IsEquipped == false)
                return;

            chip.IsEquipped = false;
            owner.CurrentStats.Remove(chip.Def.StatType, chip.Def.Value);
        }
    }
}
