using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 칩 모양(칸 좌표 배열) 계산. 장착 규칙(AugmentationChipEquipment)과 칩 그림(AugmentationChip)이 같은 계산을 쓰도록 한 곳에 둔다.
    /// 칸 좌표는 좌상단 (0,0), x는 오른쪽, y는 아래로 증가한다.
    /// </summary>
    public static class ChipShape
    {
        // 시계 방향 90도 회전. (x, y) -> (Height-1-y, x)
        // 칸의 순서는 그대로 두므로 i번째 칸은 회전 후에도 같은 칸을 따라간다
        public static Vector2Int[] RotateClockwise(Vector2Int[] cells)
        {
            int height = GetSize(cells).y;
            Vector2Int[] rotated = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                rotated[i] = new Vector2Int(height - 1 - cells[i].y, cells[i].x);

            return rotated;
        }

        // 원래 모양을 시계 방향으로 rotation번(0~3) 돌린 모양
        public static Vector2Int[] Rotate(Vector2Int[] cells, int rotation)
        {
            Vector2Int[] rotated = (Vector2Int[])cells.Clone();
            for (int i = 0; i < Normalize(rotation); i++)
                rotated = RotateClockwise(rotated);

            return rotated;
        }

        // 칩이 차지하는 가로/세로 칸 수
        public static Vector2Int GetSize(Vector2Int[] cells)
        {
            Vector2Int size = Vector2Int.zero;
            foreach (Vector2Int cell in cells)
                size = Vector2Int.Max(size, cell + Vector2Int.one);

            return size;
        }

        public static int Normalize(int rotation) => ((rotation % 4) + 4) % 4;
    }
}
