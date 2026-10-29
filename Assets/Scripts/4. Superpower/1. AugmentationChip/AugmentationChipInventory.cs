using System;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 증강칩 목록과 장착 기록. Player 오브젝트(CombatEntity가 있는 곳)에 붙인다.
    /// 칩을 얻는 쪽(보상, 드랍 등)이 Add로 넣고, 장착 UI가 Equip/Unequip을 부른다.
    /// 능력치 칩은 장착/해제할 때 칩의 StatType/Value만큼 같은 오브젝트의 CombatEntity.CurrentStats에 보너스를 더하고 뺀다.
    /// 스킬 칩은 장착하면 스킬 슬롯(1~5번 키)의 가장 앞 빈 자리에 들어가고, 해제하면 그 자리를 비워 둔다. 다른 스킬의 번호는 바뀌지 않는다.
    /// 그리드 칸 밖이나 겹치는 자리인지는 칩을 놓는 쪽(ChipGridView)이 확인한다.
    /// </summary>
    public class AugmentationChipInventory : MonoBehaviour
    {
        // 스킬 칩은 이 개수까지만 장착한다. 슬롯 번호(0부터)가 곧 발동 키 순서다
        public const int SkillSlotCount = 5;

        private static readonly List<OwnedChip> chips = new();
        private static readonly OwnedChip[] skillSlots = new OwnedChip[SkillSlotCount]; // 빈 슬롯은 null
        private CombatEntity owner;

        public IReadOnlyList<OwnedChip> Chips => chips;
        public IReadOnlyList<OwnedChip> SkillSlots => skillSlots;

        // 패널이 현재 스탯과 칩 보너스를 보여줄 때 읽는다
        public RuntimeStats Stats => owner.CurrentStats;

        // 스킬 슬롯이 바뀌면 알린다 (패널의 스킬 슬롯 줄, 게임 화면의 스킬 바가 다시 그린다)
        public event Action SkillSlotsChanged;

        private void Awake()
        {
            owner = GetComponent<CombatEntity>();
        }

        // 장착된 스킬 칩만 쿨타임이 돈다. 해제해 둔 칩은 해제한 순간의 쿨타임에서 멈춰 있다
        // 패널이 열려 있어도 흐르도록 커서 상태와 상관없이 여기서 돌린다
        private void Update()
        {
            foreach (OwnedChip chip in skillSlots)
            {
                if (chip != null)
                    chip.Skill.Tick(Time.deltaTime);
            }
        }

        // cells는 칩을 주는 쪽이 ChipShapeLibrary.Roll로 정한 모양
        // 스킬 칩이면 칩마다 스킬을 새로 만들어 넣는다
        public void Add(AugmentationChipDef def, Vector2Int[] cells)
        {
            SuperpowerSkill skill = def is SkillChipDef skillDef ? SkillFactory.Create(skillDef) : null;
            chips.Add(new OwnedChip(def, cells, skill));
        }

        // 스킬 칩은 빈 스킬 슬롯이 있어야 장착할 수 있다
        public bool CanEquip(OwnedChip chip)
        {
            if (chip.IsEquipped)
                return false;

            return chip.Def is StatChipDef || FindEmptySkillSlot() >= 0;
        }

        // 장착 기록을 남기고 스탯 보너스를 더하거나 스킬 슬롯에 넣는다. 스킬 슬롯이 가득 차 있으면 장착하지 않고 false
        // preferredSlot: 스킬 칩을 넣고 싶은 슬롯. 비어 있으면 그 슬롯에, 아니면 가장 앞의 빈 슬롯에 넣는다
        //   (그리드 위에서 칩을 옮길 때 원래 번호를 지키려고 패널이 넘긴다)
        public bool Equip(OwnedChip chip, Vector2Int origin, int rotation, int preferredSlot = -1)
        {
            if (CanEquip(chip) == false)
                return false;

            chip.IsEquipped = true;
            chip.Origin = origin;
            chip.Rotation = rotation;

            if (chip.Def is StatChipDef stat)
            {
                owner.CurrentStats.Add(stat.StatType, stat.Value);
            }
            else
            {
                int slot = IsEmptySkillSlot(preferredSlot) ? preferredSlot : FindEmptySkillSlot();
                skillSlots[slot] = chip;
                SkillSlotsChanged?.Invoke();
            }

            return true;
        }

        public void Unequip(OwnedChip chip)
        {
            if (chip.IsEquipped == false)
                return;

            chip.IsEquipped = false;

            if (chip.Def is StatChipDef stat)
            {
                owner.CurrentStats.Remove(stat.StatType, stat.Value);
            }
            else
            {
                // 자리를 비워 두기만 하고 뒤의 스킬을 당기지 않는다
                skillSlots[GetSkillSlot(chip)] = null;
                SkillSlotsChanged?.Invoke();
            }
        }

        // 장착 여부와 상관없이 모든 스킬 칩의 쿨타임을 끝낸다
        // 칩은 정비소에서만 바꾸므로 정비를 마칠 때(패널을 닫을 때) 부른다. 해제해 둔 칩도 멈춘 쿨타임을 들고 나가지 않게 한다
        public void ResetAllCooldowns()
        {
            foreach (OwnedChip chip in chips)
            {
                if (chip.Skill != null)
                    chip.Skill.ResetCooldown();
            }
        }

        // 칩이 들어 있는 스킬 슬롯 번호. 없으면 -1
        public int GetSkillSlot(OwnedChip chip) => Array.IndexOf(skillSlots, chip);

        // 스킬 슬롯 순서 바꾸기 (패널의 드래그앤드롭). 옮길 자리에 스킬이 있으면 서로 바꾸고, 비어 있으면 그 번호로 옮긴다
        public void SwapSkillSlots(int from, int to)
        {
            if (from == to)
                return;

            (skillSlots[from], skillSlots[to]) = (skillSlots[to], skillSlots[from]);
            SkillSlotsChanged?.Invoke();
        }

        private int FindEmptySkillSlot() => Array.IndexOf(skillSlots, null);

        private bool IsEmptySkillSlot(int slot) => slot >= 0 && slot < SkillSlotCount && skillSlots[slot] == null;
    }
}
