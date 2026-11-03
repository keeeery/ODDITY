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

        // 임무 창에 보여 줄 한 줄 (예: "5~6등급 칩 3종", 범위가 한 등급이면 "2등급 칩 5종")
        public string Label => MinCell == MaxCell ? $"{MinCell}등급 칩 {Count}종" : $"{MinCell}~{MaxCell}등급 칩 {Count}종";
    }
}
