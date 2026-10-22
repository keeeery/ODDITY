using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    public class AugmentationChipDTO
    {
        public int ID;
        public string Name;
        public string Description;
        public string StatType;
        public int Value;
        public string UnitSpriteKey;
        public string GridData;
    }

    public class AugmentationChipDef
    {
        public int ID;
        public string Name;
        public string Description;
        public StatType StatType;
        public int Value;
        public Sprite UnitSprite;

        // 칩이 차지하는 칸 좌표 (좌상단 (0,0), x는 오른쪽, y는 아래로 증가)
        public Vector2Int[] Cells;
        public int Width;
        public int Height;
    }
}
