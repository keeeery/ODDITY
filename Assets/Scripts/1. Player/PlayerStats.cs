using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Player
{
    // 플레이어 기본 스탯. 전투 스탯(MaxHp/Atk/Def)에 플레이어만 가진 스탯을 더한다
    // 몬스터 스탯 에셋에는 이 필드들이 생기지 않는다
    // 에셋 값은 기본값으로 읽기만 한다. 증강칩 보너스를 얹은 최종값은 CombatEntity.CurrentStats.Get(StatType)으로 읽는다
    // 증강칩 보너스가 정수라서 칩으로 오르는 스탯은 전투 스탯처럼 모두 int로 둔다
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Scriptable/Player/Stats", order = 0)]
    public class PlayerStats : CombatStats
    {
        [field: Title("Player")]
        [field: SerializeField, Min(0)] public int PsychoMaxMass { get; private set; } = 20; // 염동력으로 들 수 있는 최대 질량 (kg)
        [field: SerializeField, Min(0)] public int MaxStamina { get; private set; } = 100;   // 최대 스태미나. 염동력으로 물체를 들고 있는 동안 줄어든다

        // 플레이어 전용 스탯을 이름표로 꺼낼 수 있게 덧붙인다. 플레이어 전용 스탯을 추가하면 여기에도 한 줄 추가한다
        public override int Get(StatType type) => type switch
        {
            StatType.PsychoMaxMass => PsychoMaxMass,
            StatType.MaxStamina => MaxStamina,
            _ => base.Get(type),
        };
    }
}
