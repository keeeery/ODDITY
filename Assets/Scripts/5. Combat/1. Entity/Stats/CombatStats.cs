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

        // 이 기본값 위에 보너스를 얹을 런타임 스탯을 만든다. 스탯이 더 있는 자식 SO는 그 스탯까지 계산하는 런타임 스탯을 돌려준다
        // (99. Study Factory Method: 무엇을 만들지는 자식 클래스가 정한다)
        public virtual RuntimeStats CreateRuntime() => new RuntimeStats(this);
    }
}