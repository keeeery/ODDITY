using System;
using System.Collections.Generic;

namespace TeamOdd.Combat
{
    // 런타임에 바뀌는 스탯. 기본값(SO)은 건드리지 않고, 그 위에 보너스를 더해서 돌려준다
    // SO는 여러 엔티티가 공유하는 에셋이라 직접 고치면 모두에게 반영되고 에디터에서는 값이 남는다
    public class RuntimeStats : IReadOnlyStats
    {
        private readonly IReadOnlyStats baseStats;
        private readonly Dictionary<StatType, int> bonus = new();

        // 보너스가 바뀌면 알린다 (MaxHp가 줄었을 때 현재 hp를 맞추는 용도 등)
        public event Action Changed;

        public RuntimeStats(IReadOnlyStats baseStats)
        {
            this.baseStats = baseStats;
        }

        public int MaxHp => baseStats.MaxHp + GetBonus(StatType.MaxHp);
        public int Atk => baseStats.Atk + GetBonus(StatType.Atk);
        public int Def => baseStats.Def + GetBonus(StatType.Def);

        public int GetBonus(StatType type) => bonus.TryGetValue(type, out int value) ? value : 0;

        public void Add(StatType type, int value)
        {
            bonus[type] = GetBonus(type) + value;
            Changed?.Invoke();
        }

        public void Remove(StatType type, int value) => Add(type, -value);
    }
}
