using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 칩과 그리드가 같이 쓰는 칸 크기. 한쪽만 바뀌어 칸이 어긋나지 않도록 한 곳에서 정의한다
    /// </summary>
    public static class ChipGridMetrics
    {
        public const float CellSize = 72f;
        public const float CellGap = 4f;
        public const float Step = CellSize + CellGap;

        // 칩 모양을 뽑을 때 이 크기 그리드에 들어가는 모양만 고른다. 프리팹 그리드 크기(AugmentationChipLayoutBuilder.gridSize)와 맞춘다
        public const int GridSize = 6;

        // 칸 좌표 -> 좌상단 기준 위치 (y는 아래로 증가하므로 음수)
        public static Vector2 CellPosition(Vector2Int cell) => new(cell.x * Step, -cell.y * Step);

        // 칸 n개가 차지하는 길이 (마지막 칸 뒤 틈은 제외)
        public static float Length(int cells) => cells * Step - CellGap;
    }
}
