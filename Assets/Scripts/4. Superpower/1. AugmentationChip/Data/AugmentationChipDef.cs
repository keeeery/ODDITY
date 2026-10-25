using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 능력치 칩은 장착하면 스탯이 오르고, 스킬 칩은 장착하면 스킬 슬롯(1~5번 키)에 들어간다
    public enum ChipType
    {
        Stat,
        Skill,
    }

    public class AugmentationChipDTO
    {
        public int ID;
        public string Name;
        public string Description;
        public string ChipType;
        public string StatType;
        public int Value;
        public string SkillType;
        public float Cooldown;
        public string SkillParams;
        public string UnitSpriteKey;
        public int CellCount;
    }

    public class AugmentationChipDef
    {
        public int ID;
        public string Name;
        public string Description;
        public ChipType ChipType;
        public Sprite UnitSprite;

        // 칩이 차지하는 칸 수. 모양은 칩을 얻을 때마다 ChipShapeLibrary가 정해서 OwnedChip에 넣는다
        public int CellCount;

        // 능력치 칩만 쓴다
        public StatType StatType;
        public int Value;

        // 스킬 칩만 쓴다. 쿨타임은 모든 스킬에 공통이라 칸을 따로 두고, 스킬마다 다른 값은 SkillParams에 모은다
        public SkillType SkillType;
        public float Cooldown;
        public SkillParams SkillParams;
    }
}
