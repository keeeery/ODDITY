namespace TeamOdd.Reward
{
    // BossReward.tsv 한 줄은 "칸 수가 MinCell~MaxCell인 칩 중에서 Count종"이다
    // 같은 RewardId의 줄이 모여 보스 하나의 보상이 되고, 보스는 인스펙터에서 RewardId를 고른다

    // BossReward.tsv 한 줄
    public class BossRewardDTO
    {
        public int RewardId;
        public int MinCell;
        public int MaxCell;
        public int Count;
    }

    public class BossRewardDef
    {
        public int RewardId;
        public int MinCell;
        public int MaxCell;
        public int Count;

        public bool Contains(int cellCount) => MinCell <= cellCount && cellCount <= MaxCell;
    }
}
