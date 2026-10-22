namespace TeamOdd.Superpower
{
    public interface IStat
    {
        int MaxHp { get; }
        int Atk { get; }
        int Def { get; }

        // 들어온 스탯에 자신의 효과를 덧씌운 결과를 돌려준다
        Stat GetStat(Stat stat);
    }

    public class Stat : IStat
    {
        public int MaxHp { get; private set; }
        public int Atk { get; private set; }
        public int Def { get; private set; }

        public Stat(int maxHp, int atk, int def)
        {
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
        }

        public Stat GetStat(Stat current) { return this; }

        public static Stat operator +(Stat a, Stat b)
        {
            return new Stat(a.MaxHp + b.MaxHp, a.Atk + b.Atk, a.Def + b.Def);
        }
    }
}
