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

                    string[] cells = line.Split('\t');

                    chipDto.ID = int.Parse(cells[0]);
                    chipDto.Name = cells[1];
                    chipDto.Description = cells[2];
                    chipDto.StatType = cells[3];
                    chipDto.Value = int.Parse(cells[4]);
                    chipDto.UnitSpriteKey = cells[5];
                    chipDto.CellCount = int.Parse(cells[6]);

                    rows.Add(chipDto);
                }
            }

            // List<AugmentationChipDTO> rows 를 이용해서 def 객체를 생성한다
            foreach (AugmentationChipDTO chipDto in rows)
            {
                StatType statType = Enum.Parse<StatType>(chipDto.StatType);
                Sprite unitSprite = Resources.Load<Sprite>(chipDto.UnitSpriteKey);

                defs[chipDto.ID] = new AugmentationChipDef
                {
                    ID = chipDto.ID,
                    Name = chipDto.Name,
                    Description = chipDto.Description,
                    StatType = statType,
                    Value = chipDto.Value,
                    UnitSprite = unitSprite,
                    CellCount = chipDto.CellCount,
                };
            }

            Debug.Log(defs.Count);
        }
    }
}
