using System.Collections.Generic;
using TeamOdd.Reward;
using UnityEngine;

namespace Study.MiniDefence
{
    public class MissionDTO
    {
        public int ID;
        public string MissionName;
        public string MissionDescription;
        public string BossName;
        public int Reward;          // BossReward.tsv의 RewardId
        public string BossSpriteKey;
        public string NextStageName;
        public string QuestName;    // 퀘스트 목록(Quest List UI)에 표시되는 이름
    }

    [System.Serializable]
    public class MissionDef
    {
        public int ID;
        public string MissionName;
        public string MissionDescription;
        public string BossName;
        public int Reward;          // BossReward.tsv의 RewardId
        public IReadOnlyList<BossRewardDef> RewardRows; // Reward에 해당하는 보상 줄 (표에 없으면 null)
        public Sprite BossSprite;
        public string NextStageName;
        public string QuestName;    // 퀘스트 목록(Quest List UI)에 표시되는 이름

        // 임무 창에 보여 줄 보상 글 (보상 줄마다 한 줄)
        public string GetRewardText()
        {
            if (RewardRows == null || RewardRows.Count == 0)
                return "보상 정보 없음";

            string[] labels = new string[RewardRows.Count];
            for (int i = 0; i < RewardRows.Count; i++)
                labels[i] = $"· {RewardRows[i].Label}";
            return string.Join("\n", labels);
        }
    }
}
