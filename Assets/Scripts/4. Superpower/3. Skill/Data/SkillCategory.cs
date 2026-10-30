namespace TeamOdd.Superpower
{
    // 테이블 Category 칸에 적는 스킬 분류. UI에 표시·정렬·필터할 때만 쓰고, 스킬을 만드는 방법(SkillFactory)과는 상관없다
    // 애매한 스킬(Blink, TimeStop 등)은 기획 판단이라 코드가 아니라 표에서 정한다
    public enum SkillCategory
    {
        Attack,  // 공격
        Defense, // 방어
        Utility, // 유틸
        Passive, // 패시브 (키 대신 트리거로 발동)
    }
}
