namespace TeamOdd.Player
{
    // 회피(구르기)를 누가 시작했는지. 구르기를 시작하는 쪽이 넘기고, 회피 피드백(DodgeEffect)이 이걸 보고 무엇을 보여줄지 정한다
    public enum DodgeType
    {
        Manual, // 플레이어가 직접 눌러 구름
        Auto,   // 조건 반사 칩이 대신 구름
    }
}
