using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 보드에 장착된 칩 하나. 어떤 칩이 어느 방향으로 어느 칸에 끼워져 있는지.
    /// </summary>
    public class EquippedChip
    {
        public OwnedChip Chip { get; }
        public Vector2Int Origin { get; }  // 칩 좌상단이 놓인 보드 칸
        public int Rotation { get; }       // 시계 방향 90도 회전 횟수 (0~3)
        public Vector2Int[] Cells { get; } // 회전이 적용된 모양 (Origin 기준 상대 좌표)

        public EquippedChip(OwnedChip chip, Vector2Int origin, int rotation, Vector2Int[] cells)
        {
            Chip = chip;
            Origin = origin;
            Rotation = rotation;
            Cells = cells;
        }
    }

    /// <summary>
    /// 증강칩 장착 상태. UI 밖에서 보드 크기, 칸 점유, 배치 규칙, 장착된 칩(종류·회전·오리진)을 들고,
    /// 장착/해제할 때 칩의 StatType/Value만큼 플레이어의 RuntimeStats에 보너스를 더하고 뺀다.
    /// UI(AugmentationChipPanel)는 배치 가능 여부를 묻고 Equip/Unequip을 호출한 뒤 결과를 그리기만 한다.
    /// </summary>
    public class AugmentationChipEquipment
    {
        private readonly RuntimeStats stats;
        private readonly int boardSize;
        private readonly OwnedChip[,] occupied;
        private readonly List<EquippedChip> equipped = new();

        public AugmentationChipEquipment(int boardSize, RuntimeStats stats)
        {
            this.boardSize = boardSize;
            this.stats = stats;
            occupied = new OwnedChip[boardSize, boardSize];
        }

        public int BoardSize => boardSize;
        public IReadOnlyList<EquippedChip> Equipped => equipped;
        public IReadOnlyStats Stats => stats;
        public int GetBonus(StatType type) => stats.GetBonus(type);

        public EquippedChip Find(OwnedChip chip) => equipped.Find(e => e.Chip == chip);
        public bool IsEquipped(OwnedChip chip) => Find(chip) != null;

        // 보드 밖으로 나가거나 다른 칩과 겹치면 놓을 수 없다. 자기 자신이 차지한 칸은 빈 칸으로 본다
        public bool CanPlace(OwnedChip chip, int rotation, Vector2Int origin)
        {
            foreach (Vector2Int offset in ChipShape.Rotate(chip.Def.Cells, rotation))
            {
                Vector2Int p = origin + offset;
                if (IsInside(p) == false)
                    return false;
                if (occupied[p.x, p.y] != null && occupied[p.x, p.y] != chip)
                    return false;
            }

            return true;
        }

        // 위쪽 줄부터, 같은 줄에서는 왼쪽부터 훑어서 칩을 놓을 수 있는 첫 자리를 찾는다
        public bool TryFindOrigin(OwnedChip chip, int rotation, out Vector2Int origin)
        {
            Vector2Int size = ChipShape.GetSize(ChipShape.Rotate(chip.Def.Cells, rotation));
            for (int y = 0; y <= boardSize - size.y; y++)
                for (int x = 0; x <= boardSize - size.x; x++)
                {
                    origin = new Vector2Int(x, y);
                    if (CanPlace(chip, rotation, origin))
                        return true;
                }

            origin = default;
            return false;
        }

        // 이미 장착된 칩이거나 놓을 수 없는 자리면 false
        public bool Equip(OwnedChip chip, int rotation, Vector2Int origin)
        {
            if (IsEquipped(chip) || CanPlace(chip, rotation, origin) == false)
                return false;

            rotation = ChipShape.Normalize(rotation);
            Vector2Int[] cells = ChipShape.Rotate(chip.Def.Cells, rotation);
            foreach (Vector2Int offset in cells)
                occupied[origin.x + offset.x, origin.y + offset.y] = chip;

            equipped.Add(new EquippedChip(chip, origin, rotation, cells));
            stats.Add(chip.Def.StatType, chip.Def.Value);
            return true;
        }

        public void Unequip(OwnedChip chip)
        {
            EquippedChip target = Find(chip);
            if (target == null)
                return;

            foreach (Vector2Int offset in target.Cells)
                occupied[target.Origin.x + offset.x, target.Origin.y + offset.y] = null;

            equipped.Remove(target);
            stats.Remove(chip.Def.StatType, chip.Def.Value);
        }

        private bool IsInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < boardSize && p.y < boardSize;
    }
}
