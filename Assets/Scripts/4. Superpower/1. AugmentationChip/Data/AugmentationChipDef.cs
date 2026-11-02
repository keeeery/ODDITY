using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 칩 표는 종류마다 하나다. 능력치 칩은 StatChip.tsv, 스킬 칩은 SkillChip.tsv에 적는다
    // 두 표 모두 앞의 여섯 칸(Id, Name, Description, CellCount, UnitSpriteKey, IconSpriteKey)이 같고, 그 뒤에 종류마다 필요한 칸만 있다

    // 두 표에 공통인 칸
    public abstract class AugmentationChipDTO
    {
        public int ID;
        public string Name;
        public string Description;
        public int CellCount;
        public string UnitSpriteKey;
        public string IconSpriteKey;
    }

    // StatChip.tsv 한 줄
    public class StatChipDTO : AugmentationChipDTO
    {
        public string StatType;
        public int Value;
    }

    // SkillChip.tsv 한 줄
    public class SkillChipDTO : AugmentationChipDTO
    {
        public string Category;
        public string SkillType;
        public float Cooldown;
        public float Cost;
        public string Params;
        public string PrefabKey;
    }

    // 모든 칩에 공통인 값. 칩 종류마다 자식 클래스가 자기 값만 더한다
    public abstract class AugmentationChipDef
    {
        public int ID;
        public string Name;
        public string Description;
        public Sprite UnitSprite;
        public Sprite IconSprite; // 칩을 대표하는 그림

        // 칩이 차지하는 칸 수. 모양은 칩을 얻을 때마다 ChipShapeLibrary가 정해서 OwnedChip에 넣는다
        public int CellCount;
    }

    // 능력치 칩. 장착하는 동안 StatType 스탯이 Value만큼 오른다
    public class StatChipDef : AugmentationChipDef
    {
        public StatType StatType;
        public int Value;
    }

    // 스킬 칩. 장착하면 스킬 슬롯(1~5번 키)에 들어간다
    // 쿨타임과 스태미나 소모량(Cost)은 모든 스킬에 공통이라 칸을 따로 두고, 스킬마다 다른 값은 Params에 모은다
    public class SkillChipDef : AugmentationChipDef
    {
        public SkillCategory Category; // 공격/방어/유틸. 표시·정렬용
        public SkillType SkillType;
        public float Cooldown;
        public float Cost; // 스킬을 쓸 때마다 줄어드는 스태미나
        public SkillParams Params;
        public GameObject Prefab; // 스킬을 쓰면 띄우는 프리팹 (겉모습과 진행을 맡는다). 프리팹이 없는 스킬은 null
    }
}
