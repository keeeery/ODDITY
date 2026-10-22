namespace TeamOdd.Combat
{
    // 스탯을 읽기만 하는 쪽이 쓰는 인터페이스
    // CombatStats(기본값 SO)와 RuntimeStats(기본값 + 보너스)가 같은 모양으로 읽힌다
    public interface IReadOnlyStats
    {
        int MaxHp { get; }
        int Atk { get; }
        int Def { get; }
    }
}
