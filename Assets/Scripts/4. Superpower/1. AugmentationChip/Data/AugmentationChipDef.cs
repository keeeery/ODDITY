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
        public int CellCount;
    }

    public class AugmentationChipDef
    {
        public int ID;
        public string Name;
        public string Description;
        public StatType StatType;
        public int Value;
        public Sprite UnitSprite;

        // 칩이 차지하는 칸 수. 모양은 칩을 얻을 때마다 ChipShapeLibrary가 정해서 OwnedChip에 넣는다
        public int CellCount;
    }
}
