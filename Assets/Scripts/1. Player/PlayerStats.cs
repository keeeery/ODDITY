using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Player
{
    // 플레이어 기본 스탯. 전투 스탯(MaxHp/Atk/Def)에 플레이어만 가진 스탯을 더한다
    // 몬스터 스탯 에셋에는 이 필드들이 생기지 않는다
    // 에셋 값은 기본값으로 읽기만 한다. 증강칩 보너스를 얹은 최종값은 PlayerRuntimeStats가 계산한다
    // 증강칩 보너스가 정수라서 칩으로 오르는 스탯은 전투 스탯처럼 모두 int로 둔다
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Scriptable/Player/Stats", order = 0)]
    public class PlayerStats : CombatStats
    {
        [field: Title("Player")]
        [field: SerializeField, Min(0)] public int PsychoMaxMass { get; private set; } = 20; // 염동력으로 들 수 있는 최대 질량 (kg)

        // 플레이어의 CombatEntity.CurrentStats는 플레이어 전용 스탯까지 계산하는 PlayerRuntimeStats가 된다
        public override RuntimeStats CreateRuntime() => new PlayerRuntimeStats(this);
    }
}
