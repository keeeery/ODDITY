using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 칩 한 장과 그 모양, 장착 기록.
    /// 같은 칩(Def)을 여러 장 가질 수 있고 장마다 모양이 다르므로, 모양과 장착 기록은 Def가 아니라 이 인스턴스에 둔다.
    /// 장착 기록은 스탯 반영과 함께 바뀌어야 하므로 AugmentationChipInventory.Equip/Unequip으로만 바꾼다.
    /// </summary>
    public class OwnedChip
    {
        public AugmentationChipDef Def { get; }

        // 얻을 때 정해진 모양 (회전 전, 좌상단 (0,0), x는 오른쪽, y는 아래로 증가)
        public Vector2Int[] Cells { get; }

        // 스킬 칩이면 이 칩의 스킬, 능력치 칩이면 null. 칩마다 따로 만들어서 쿨타임이 칩을 따라다닌다
        public SuperpowerSkill Skill { get; }

        // 몇 번째로 얻은 칩인지 (0부터). 트레이의 최신순 정렬에 쓴다
        public int AcquiredOrder { get; }

        public bool IsEquipped;
        public Vector2Int Origin; // 장착 위치. 칩 좌상단이 놓인 그리드 칸
        public int Rotation;      // 장착 방향. 시계 방향 90도 회전 횟수 (0~3)

        // 얻은 뒤 아직 확인하지 않은 칩. 트레이에 NEW 표시가 붙고, 마우스를 올리거나 집으면 꺼진다
        public bool IsNew = true;

        public OwnedChip(AugmentationChipDef def, Vector2Int[] cells, SuperpowerSkill skill, int acquiredOrder)
        {
            Def = def;
            Cells = cells;
            Skill = skill;
            AcquiredOrder = acquiredOrder;
        }
    }
}