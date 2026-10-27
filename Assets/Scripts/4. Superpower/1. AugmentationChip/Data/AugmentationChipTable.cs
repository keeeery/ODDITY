using System;
using System.Collections.Generic;
using System.IO;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 능력치 칩 표와 스킬 칩 표를 읽어 칩 Def를 ID로 모아 둔다. 칩을 주는 쪽은 종류와 상관없이 Get/GetDefs로 꺼낸다
    public class AugmentationChipTable
    {
        private const string StatTableName = "Table/StatChip.tsv";
        private const string SkillTableName = "Table/SkillChip.tsv";

        private Dictionary<int, AugmentationChipDef> defs = new();

        public AugmentationChipDef Get(int id) => defs[id];

        public IReadOnlyCollection<AugmentationChipDef> GetDefs() => defs.Values;

        public int Count => defs.Count;

        public void Load()
        {
            defs.Clear();

            foreach (StatChipDTO chipDto in ReadStatChips())
            {
                StatChipDef def = new StatChipDef
                {
                    StatType = Enum.Parse<StatType>(chipDto.StatType),
                    Value = chipDto.Value,
                };
                SetCommon(def, chipDto);
                Register(def);
            }

            foreach (SkillChipDTO chipDto in ReadSkillChips())
            {
                SkillChipDef def = new SkillChipDef
                {
                    SkillType = Enum.Parse<SkillType>(chipDto.SkillType),
                    Cooldown = chipDto.Cooldown,
                    Params = new SkillParams(chipDto.Params),
                };
                SetCommon(def, chipDto);

                // 매개변수가 틀린 칩을 줍는 순간이 아니라 테이블을 읽을 때 알 수 있도록 한 번 만들어 본다
                // 매개변수가 틀리면 여기서 예외가 나고, 만드는 코드가 없는 스킬은 지급되지 않게 빼 둔다
                if (SkillFactory.Create(def) == null)
                {
                    Debug.LogError($"[AugmentationChipTable] {chipDto.ID}번 스킬 칩의 값이 잘못되어 테이블에서 뺍니다.");
                    continue;
                }

                Register(def);
            }

            Debug.Log(defs.Count);
        }

        // StatChip.tsv: 공통 다섯 칸 + StatType, Value
        private static List<StatChipDTO> ReadStatChips()
        {
            List<StatChipDTO> rows = new List<StatChipDTO>();
            foreach (string[] cells in ReadRows(StatTableName))
            {
                StatChipDTO chipDto = new();
                ReadCommon(chipDto, cells);
                chipDto.StatType = cells[5];
                chipDto.Value = int.Parse(cells[6]);
                rows.Add(chipDto);
            }

            return rows;
        }

        // SkillChip.tsv: 공통 다섯 칸 + SkillType, Cooldown, Params
        private static List<SkillChipDTO> ReadSkillChips()
        {
            List<SkillChipDTO> rows = new List<SkillChipDTO>();
            foreach (string[] cells in ReadRows(SkillTableName))
            {
                SkillChipDTO chipDto = new();
                ReadCommon(chipDto, cells);
                chipDto.SkillType = cells[5];
                // 소수점은 '.'으로 적는다.
                chipDto.Cooldown = float.Parse(cells[6]);
                // 매개변수가 없는 스킬은 비워 둔다. 줄 끝의 빈 칸이 잘려 있어도 빈 칸으로 본다
                chipDto.Params = cells.Length > 7 ? cells[7] : "";
                rows.Add(chipDto);
            }

            return rows;
        }

        // 첫 줄(열 이름)은 건너뛰고 한 줄씩 칸으로 나눈다. 파일 끝 빈 줄은 건너뛴다
        private static List<string[]> ReadRows(string tableName)
        {
            string path = Application.streamingAssetsPath + "/" + tableName;
            List<string[]> rows = new List<string[]>();
            using (StreamReader reader = new StreamReader(path))
            {
                reader.ReadLine();
                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    rows.Add(line.Split('\t'));
                }
            }

            return rows;
        }

        // 두 표에 공통인 앞 다섯 칸
        private static void ReadCommon(AugmentationChipDTO chipDto, string[] cells)
        {
            chipDto.ID = int.Parse(cells[0]);
            chipDto.Name = cells[1];
            chipDto.Description = cells[2];
            chipDto.CellCount = int.Parse(cells[3]);
            chipDto.UnitSpriteKey = cells[4];
        }

        // 공통 값을 Def로 옮긴다. 그림은 표에 적힌 경로로 찾아온다
        private static void SetCommon(AugmentationChipDef def, AugmentationChipDTO chipDto)
        {
            def.ID = chipDto.ID;
            def.Name = chipDto.Name;
            def.Description = chipDto.Description;
            def.UnitSprite = Resources.Load<Sprite>(chipDto.UnitSpriteKey);
            def.CellCount = chipDto.CellCount;
        }

        // 두 표의 ID가 겹치면 앞의 칩을 덮어쓰지 않고 뒤의 칩을 뺀다
        private void Register(AugmentationChipDef def)
        {
            if (defs.ContainsKey(def.ID))
            {
                Debug.LogError($"[AugmentationChipTable] {def.ID}번 칩이 이미 있어 {def.Name}을(를) 테이블에서 뺍니다.");
                return;
            }

            defs[def.ID] = def;
        }
    }
}
