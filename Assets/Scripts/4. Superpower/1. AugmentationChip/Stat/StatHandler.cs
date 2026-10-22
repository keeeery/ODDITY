using System.Collections.Generic;

namespace TeamOdd.Superpower
{
    // 기본 스탯 위에 장착된 칩 효과를 겹겹이 덧씌워 계산한다
    public class StatHandler
    {
        public IStat GetBase() => baseStat;
        public IStat GetAdded() => Calculate();
        public IStat GetCurrent() => baseStat + Calculate();

        public int Count => decoratorList.Count;

        private Stat baseStat;
        private List<IStat> decoratorList = new List<IStat>();

        public StatHandler(Stat baseStat)
        {
            this.baseStat = baseStat;
        }

        private Stat Calculate()
        {
            Stat stat = new Stat(0, 0, 0);

            for (int i = 0; i < decoratorList.Count; i++)
                stat = decoratorList[i].GetStat(stat);

            return stat;
        }

        public void Add(IStat decorator)
        {
            decoratorList.Add(decorator);
        }

        // 같은 칩이 여러 개 장착될 수 있으므로 타입이 아니라 인스턴스로 지운다
        public void Remove(IStat decorator)
        {
            decoratorList.Remove(decorator);
        }
    }
}
