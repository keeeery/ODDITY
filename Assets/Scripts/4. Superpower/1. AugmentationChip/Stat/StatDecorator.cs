using TeamOdd.Combat;

namespace TeamOdd.Superpower
{
    public abstract class StatDecorator : IStat
    {
        public abstract int MaxHp { get; }
        public abstract int Atk { get; }
        public abstract int Def { get; }

        // 기본은 합연산. 곱연산 같은 다른 규칙이 필요하면 하위 클래스에서 재정의한다
        public virtual Stat GetStat(Stat stat)
        {
            return new Stat(stat.MaxHp + MaxHp, stat.Atk + Atk, stat.Def + Def);
        }
    }

    // 증강칩 하나의 장착 효과
    // 효과마다 클래스를 만들지 않고, 테이블의 StatType / Value로 어떤 스탯을 얼마나 올릴지 정한다
    public class AugmentationChipStat : StatDecorator
    {
        public override int MaxHp { get; }
        public override int Atk { get; }
        public override int Def { get; }

        public AugmentationChipStat(StatType statType, int value)
        {
            switch (statType)
            {
                case StatType.MaxHp: MaxHp = value; break;
                case StatType.Atk: Atk = value; break;
                case StatType.Def: Def = value; break;
            }
        }
    }
}
