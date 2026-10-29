using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Combat
{
    [CreateAssetMenu(fileName = "Stats", menuName = "Scriptable/Combat/Stats", order = 0)]
    public class CombatStats : ScriptableObject, IReadOnlyStats
    {
        [field: Title("Settings")]
        [field: SerializeField] public int MaxHp { get; private set; }
        [field: SerializeField] public int Atk { get; private set; }
        [field: SerializeField] public int Def { get; private set; }

        // 기본값을 이름표로 꺼낸다. 스탯이 더 있는 자식 SO는 오버라이드해서 자기 스탯을 덧붙인다
        public virtual int Get(StatType type) => type switch
        {
            StatType.MaxHp => MaxHp,
            StatType.Atk => Atk,
            StatType.Def => Def,
            _ => 0,
        };
    }
}