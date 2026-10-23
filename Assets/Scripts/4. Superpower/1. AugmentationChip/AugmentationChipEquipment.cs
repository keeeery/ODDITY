using System.Collections.Generic;
using TeamOdd.Combat;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 장착된 증강칩 목록과 그에 따른 스탯 반영. UI 밖에서 장착 상태를 들고 있다.
    /// 장착/해제할 때 칩의 StatType/Value만큼 플레이어의 RuntimeStats에 보너스를 더하고 뺀다.
    /// UI(AugmentationChipPanel)는 칩을 그리드에 놓거나 뺄 때 Equip/Unequip만 호출한다. 그리드 위 위치·회전은 UI가 관리한다.
    /// </summary>
    public class AugmentationChipEquipment
    {
        private readonly RuntimeStats stats;
        // 같은 칩을 여러 개 장착할 수 있으므로 집합이 아니라 목록으로 둔다
        private readonly List<AugmentationChipDef> equipped = new();

        public AugmentationChipEquipment(RuntimeStats stats)
        {
            this.stats = stats;
        }

        public IReadOnlyList<AugmentationChipDef> Equipped => equipped;
        public IReadOnlyStats Stats => stats;
        public int GetBonus(StatType type) => stats.GetBonus(type);

        public void Equip(AugmentationChipDef def)
        {
            equipped.Add(def);
            stats.Add(def.StatType, def.Value);
        }

        public void Unequip(AugmentationChipDef def)
        {
            if (equipped.Remove(def) == false)
                return;

            stats.Remove(def.StatType, def.Value);
        }
    }
}
