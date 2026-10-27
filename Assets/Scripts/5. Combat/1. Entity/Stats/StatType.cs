namespace TeamOdd.Combat
{
    public enum StatType
    {
        MaxHp,
        Atk,
        Def,

        // 플레이어만 가진 스탯. 기본값은 PlayerStats 에셋에 있고, 증강칩 보너스만 RuntimeStats에 쌓인다
        PsychoMaxMass,
        MaxStamina,
    }
}
