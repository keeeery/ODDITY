using TeamOdd.Combat;

namespace TeamOdd.Player
{
    // 플레이어의 런타임 스탯. 전투 스탯은 RuntimeStats가, 플레이어 전용 스탯은 여기서 같은 방식(기본값 + 보너스)으로 계산한다
    // 증강칩 보너스는 전투 스탯과 똑같이 Add(StatType, value)로 쌓인다
    // 쓰는 쪽은 칩을 끼우고 빼면 바로 반영되도록 값을 저장해 두지 말고 쓸 때마다 읽는다
    public class PlayerRuntimeStats : RuntimeStats
    {
        private readonly PlayerStats baseStats;

        public PlayerRuntimeStats(PlayerStats baseStats) : base(baseStats)
        {
            this.baseStats = baseStats;
        }

        public int PsychoMaxMass => baseStats.PsychoMaxMass + GetBonus(StatType.PsychoMaxMass);
    }
}
