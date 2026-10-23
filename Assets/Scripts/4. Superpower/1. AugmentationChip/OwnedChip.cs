namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 칩 한 장. 같은 칩(Def)을 여러 장 가질 수 있으므로 장착 상태는 Def가 아니라 이 인스턴스를 기준으로 한다.
    /// </summary>
    public class OwnedChip
    {
        public AugmentationChipDef Def { get; }

        public OwnedChip(AugmentationChipDef def)
        {
            Def = def;
        }
    }
}
