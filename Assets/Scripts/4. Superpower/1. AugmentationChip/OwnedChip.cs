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

        public bool IsEquipped;
        public Vector2Int Origin; // 장착 위치. 칩 좌상단이 놓인 그리드 칸
        public int Rotation;      // 장착 방향. 시계 방향 90도 회전 횟수 (0~3)

        public OwnedChip(AugmentationChipDef def, Vector2Int[] cells)
        {
            Def = def;
            Cells = cells;
        }
    }
}