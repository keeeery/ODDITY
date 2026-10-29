using UnityEngine;

namespace Study.MiniDefence
{
    public class MissionDTO
    {
        public int ID;
        public string MissionName;
        public string MissionDescription;
        public string BossName;
        public int Reward;          // 보상 클래스(Reward)가 프로젝트에 없어 int(골드 수량)로 대체
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
        public int Reward;          // 보상 클래스(Reward)가 프로젝트에 없어 int(골드 수량)로 대체
        public Sprite BossSprite;
        public string NextStageName;
        public string QuestName;    // 퀘스트 목록(Quest List UI)에 표시되는 이름
    }
}
