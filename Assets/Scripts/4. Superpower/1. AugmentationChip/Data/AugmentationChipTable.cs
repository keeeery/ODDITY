using System;
using System.Collections.Generic;
using System.IO;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    public class AugmentationChipTable
    {
        private const string TableName = "Table/AugmentationChip.tsv";

        private Dictionary<int, AugmentationChipDef> defs = new();

        public AugmentationChipDef Get(int id) => defs[id];

        public IReadOnlyCollection<AugmentationChipDef> GetDefs() => defs.Values;

        public int Count => defs.Count;

        public void Load()
        {
            defs.Clear();

            string path = Application.streamingAssetsPath + "/" + TableName;
            List<AugmentationChipDTO> rows = new List<AugmentationChipDTO>();
            using (StreamReader reader = new StreamReader(path))
            {
                reader.ReadLine();
                while (reader.EndOfStream == false)
                {
                    AugmentationChipDTO chipDto = new();
                    string line = reader.ReadLine();

                    // 파일 끝 빈 줄은 건너뛴다
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] cells = line.Split('\t');

                    chipDto.ID = int.Parse(cells[0]);
                    chipDto.Name = cells[1];
                    chipDto.Description = cells[2];
                    chipDto.StatType = cells[3];
                    chipDto.Value = cells[4] == "" ? 0 : int.Parse(cells[4]); // 스킬 칩은 비워 둔다
                    chipDto.UnitSpriteKey = cells[5];
                    chipDto.CellCount = int.Parse(cells[6]);

                    // 스킬 칩용으로 뒤에 붙인 칸. 능력치 칩은 줄이 여기서 끝나도 된다
                    chipDto.ChipType = GetCell(cells, 7);
                    chipDto.SkillType = GetCell(cells, 8);
                    string cooldown = GetCell(cells, 9);
                    // 소수점은 '.'으로 적는다. ','는 천 단위 구분자로 읽혀 "1,5"가 15가 된다
                    chipDto.Cooldown = cooldown == "" ? 0f : float.Parse(cooldown);
                    chipDto.SkillParams = GetCell(cells, 10);

                    rows.Add(chipDto);
                }
            }

            // List<AugmentationChipDTO> rows 를 이용해서 def 객체를 생성한다
            foreach (AugmentationChipDTO chipDto in rows)
            {
                // ChipType 칸이 비어 있으면 원래부터 있던 능력치 칩
                ChipType chipType = chipDto.ChipType == "" ? ChipType.Stat : Enum.Parse<ChipType>(chipDto.ChipType);
                Sprite unitSprite = Resources.Load<Sprite>(chipDto.UnitSpriteKey);

                AugmentationChipDef def = new AugmentationChipDef
                {
                    ID = chipDto.ID,
                    Name = chipDto.Name,
                    Description = chipDto.Description,
                    ChipType = chipType,
                    UnitSprite = unitSprite,
                    CellCount = chipDto.CellCount,
                };

                // 칩 종류에 맞는 칸만 읽는다. 비워 둔 칸을 Enum.Parse하면 예외가 나므로 나눠서 읽는다
                if (chipType == ChipType.Stat)
                {
                    def.StatType = Enum.Parse<StatType>(chipDto.StatType);
                    def.Value = chipDto.Value;
                }
                else
                {
                    def.SkillType = Enum.Parse<SkillType>(chipDto.SkillType);
                    def.Cooldown = chipDto.Cooldown;
                    def.SkillParams = new SkillParams(chipDto.SkillParams);

                    // 매개변수가 틀린 칩을 줍는 순간이 아니라 테이블을 읽을 때 알 수 있도록 한 번 만들어 본다
                    // 매개변수가 틀리면 여기서 예외가 나고, 만드는 코드가 없는 스킬은 지급되지 않게 빼 둔다
                    if (SkillFactory.Create(def) == null)
                    {
                        Debug.LogError($"[AugmentationChipTable] {chipDto.ID}번 스킬 칩의 값이 잘못되어 테이블에서 뺍니다.");
                        continue;
                    }
                }

                defs[chipDto.ID] = def;
            }

            Debug.Log(defs.Count);
        }

        // 줄이 짧아 없는 칸은 빈 칸으로 본다
        private static string GetCell(string[] cells, int index) => index < cells.Length ? cells[index] : "";
    }
}
