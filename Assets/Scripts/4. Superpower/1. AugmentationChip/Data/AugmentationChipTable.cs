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
                    chipDto.GridData = cells[6];

                    rows.Add(chipDto);
                }
            }

            // List<AugmentationChipDTO> rows 를 이용해서 def 객체를 생성한다
            foreach (AugmentationChipDTO chipDto in rows)
            {
                StatType statType = Enum.Parse<StatType>(chipDto.StatType);
                Sprite unitSprite = Resources.Load<Sprite>(chipDto.UnitSpriteKey);
                Vector2Int[] gridCells = ParseGrid(chipDto.GridData, out int width, out int height);

                defs[chipDto.ID] = new AugmentationChipDef
                {
                    ID = chipDto.ID,
                    Name = chipDto.Name,
                    Description = chipDto.Description,
                    StatType = statType,
                    Value = chipDto.Value,
                    UnitSprite = unitSprite,
                    Cells = gridCells,
                    Width = width,
                    Height = height,
                };
            }

            Debug.Log(defs.Count);
        }

        // GridData 문자열을 칸 좌표 배열로 바꾼다
        // "10/11" -> '/'로 자른 덩어리가 한 행(위에서부터 y), 각 글자가 열(x), '1'인 칸만 사용
        //   ■□
        //   ■■   => (1,0) (1,1)
        private Vector2Int[] ParseGrid(string gridData, out int width, out int height)
        {
            string[] gridRows = gridData.Split('/');
            width = gridRows[0].Length;
            height = gridRows.Length;

            List<Vector2Int> gridCells = new List<Vector2Int>();
            for (int y = 0; y < gridRows.Length; y++)
            {
                for (int x = 0; x < gridRows[y].Length; x++)
                {
                    if (gridRows[y][x] == '1')
                        gridCells.Add(new Vector2Int(x, y));
                }
            }

            return gridCells.ToArray();
        }
    }
}
