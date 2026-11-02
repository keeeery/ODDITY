using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TeamOdd.Reward
{
    // 보스 보상 표를 읽어 RewardId별로 줄을 모아 둔다. 지급하는 쪽은 Get으로 한 보상의 줄을 표에 적힌 순서대로 꺼낸다
    public class BossRewardTable
    {
        private const string TableName = "Table/BossReward.tsv";

        private Dictionary<int, List<BossRewardDef>> defs = new();

        public bool TryGet(int rewardId, out IReadOnlyList<BossRewardDef> rows)
        {
            bool found = defs.TryGetValue(rewardId, out List<BossRewardDef> list);
            rows = list;
            return found;
        }

        public void Load()
        {
            defs.Clear();

            foreach (BossRewardDTO rewardDto in ReadRewards())
            {
                // 범위가 뒤집혔거나 줄 게 없는 줄은 지급할 때가 아니라 표를 읽을 때 알 수 있도록 여기서 뺀다
                if (rewardDto.MinCell < 1 || rewardDto.MaxCell < rewardDto.MinCell || rewardDto.Count <= 0)
                {
                    Debug.LogError($"[BossRewardTable] {rewardDto.RewardId}번 보상의 {rewardDto.MinCell}~{rewardDto.MaxCell}칸 {rewardDto.Count}종 줄이 잘못되어 테이블에서 뺍니다.");
                    continue;
                }

                if (defs.TryGetValue(rewardDto.RewardId, out List<BossRewardDef> rows) == false)
                {
                    rows = new List<BossRewardDef>();
                    defs[rewardDto.RewardId] = rows;
                }

                rows.Add(new BossRewardDef
                {
                    RewardId = rewardDto.RewardId,
                    MinCell = rewardDto.MinCell,
                    MaxCell = rewardDto.MaxCell,
                    Count = rewardDto.Count,
                });
            }
        }

        // BossReward.tsv: RewardId, MinCell, MaxCell, Count
        // 첫 줄(열 이름)은 건너뛰고 한 줄씩 칸으로 나눈다. 파일 끝 빈 줄은 건너뛴다
        private static List<BossRewardDTO> ReadRewards()
        {
            string path = Application.streamingAssetsPath + "/" + TableName;
            List<BossRewardDTO> rows = new List<BossRewardDTO>();
            using (StreamReader reader = new StreamReader(path))
            {
                reader.ReadLine();
                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] cells = line.Split('\t');
                    rows.Add(new BossRewardDTO
                    {
                        RewardId = int.Parse(cells[0]),
                        MinCell = int.Parse(cells[1]),
                        MaxCell = int.Parse(cells[2]),
                        Count = int.Parse(cells[3]),
                    });
                }
            }

            return rows;
        }
    }
}
