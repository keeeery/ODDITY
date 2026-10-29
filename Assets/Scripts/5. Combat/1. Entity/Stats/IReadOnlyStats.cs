namespace TeamOdd.Combat
{
    // 스탯을 읽기만 하는 쪽이 쓰는 인터페이스
    // CombatStats(기본값 SO)와 RuntimeStats(기본값 + 보너스)가 같은 모양으로 읽힌다
    public interface IReadOnlyStats
    {
        int MaxHp { get; }
        int Atk { get; }
        int Def { get; }

        // 이름표(StatType)로 읽는다. 증강칩도 같은 이름표로 보너스를 쌓으므로 쓰기와 읽기가 같은 말을 쓴다
        // 이 대상에 없는 스탯은 0
        int Get(StatType type);
    }
}
