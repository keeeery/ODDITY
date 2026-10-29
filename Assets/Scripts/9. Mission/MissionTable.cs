using System.Collections.Generic;
using System.IO;
using Jay.FileIO;
using UnityEngine;

namespace Study.MiniDefence
{
    // "미션 데이터의 로드 / 키 해석 / 보관 / 조회"의 책임을 갖는 객체
    // 새로운 데이터 종류가 생기면 이 클래스를 참고해서 만들면 됩니다.
    public class MissionTable
    {
        private const string TableName = "missions.tsv";

        private Dictionary<int, MissionDef> defs = new();

        public MissionDef Get(int id) => defs[id];

        public IReadOnlyCollection<MissionDef> GetDefs => defs.Values;

        public int Count => defs.Count;

        public void Load()
        {
            defs.Clear();

            // 특정 경로에 있는 파일을 읽어온다
            string path = Application.streamingAssetsPath + "/" + TableName;
            List<MissionDTO> rows = TSVReader.ReadTable<MissionDTO>(path);
    
            // List<MissionDTO> rows 를 이용해서 def 객체를 생성한다
            foreach (MissionDTO missionDto in rows)
            {
                Sprite bossSprite = Resources.Load<Sprite>(missionDto.BossSpriteKey);

                defs[missionDto.ID] = new MissionDef
                {
                    ID = missionDto.ID,
                    MissionName = missionDto.MissionName,
                    MissionDescription = missionDto.MissionDescription,
                    BossName = missionDto.BossName,
                    Reward = missionDto.Reward,
                    BossSprite = bossSprite,
                    NextStageName = missionDto.NextStageName,
                    QuestName = missionDto.QuestName
                };
            }
        }

    }
}
