namespace TeamOdd.Superpower
{
    // 테이블 SkillType 칸에 적는 이름. SkillFactory가 이 값으로 어떤 스킬을 만들지 고른다
    // 읽기 쉽게 분류(SkillCategory)별로 묶어 두었을 뿐, 실제 분류는 테이블 Category 칸이 정한다
    public enum SkillType
    {
        // 공격
        Shockwave,
        PsychoSlam,
        PsychoRapidFire,
        PsychoTempest,
        PsychoOrb,

        // 방어
        Heal,
        Barrier,
        TimeReverse,

        // 유틸
        Blink,
        TimeStop,
        TimeSlow,
        Resize,
    }
}
