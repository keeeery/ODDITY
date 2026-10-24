namespace TeamOdd.Combat
{
    // 공격이 피격 대상에게 주는 반응, 실제 경직/다운 처리는 피격 대상 쪽에서 담당
    public enum HitReaction
    {
        None,
        Stagger,
        Knockdown
    }
}
