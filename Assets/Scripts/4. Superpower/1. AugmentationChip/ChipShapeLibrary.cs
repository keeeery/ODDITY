using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 칸 수만 정해진 칩에 무작위 모양을 준다. 칩을 얻을 때 한 번 불러서 그 결과를 OwnedChip에 넣는다.
    /// n칸 모양은 (n-1)칸 모양마다 빈 이웃 칸을 하나씩 붙여 보는 식으로 전부 만들어 두고, 그중 하나를 고른다.
    /// 회전해서 같아지는 모양은 하나로 치므로 모든 모양이 같은 확률로 나온다. 칸 수별 목록은 처음 쓸 때 한 번만 만든다.
    /// 모양은 테이블 GridData와 같은 문자열("110/011")로 다룬다. 좌상단 (0,0), x는 오른쪽, y는 아래로 증가
    /// </summary>
    public static class ChipShapeLibrary
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // 칸 수 -> 그 칸 수로 만들 수 있는 모든 모양. 1칸은 하나뿐이므로 미리 넣어 둔다
        private static readonly Dictionary<int, List<string>> shapesByCount = new() { [1] = new List<string> { "1" } };

        // gridSize x gridSize 그리드에 들어가는 cellCount칸 모양 중 하나를 고른다
        public static Vector2Int[] Roll(int cellCount, int gridSize)
        {
            List<string> candidates = new List<string>();
            foreach (string shape in GetShapes(cellCount))
            {
                if (Fits(shape, gridSize))
                    candidates.Add(shape);
            }

            if (candidates.Count == 0)
            {
                Debug.LogError($"[ChipShapeLibrary] {gridSize}x{gridSize} 그리드에 들어가는 {cellCount}칸 모양이 없습니다.");
                return null;
            }

            string picked = candidates[Random.Range(0, candidates.Count)];
            return ToCells(picked);
        }

        // cellCount칸으로 만들 수 있는 모든 모양 (회전해서 같은 모양은 하나만)
        public static List<string> GetShapes(int cellCount)
        {
            if (shapesByCount.TryGetValue(cellCount, out List<string> shapes))
                return shapes;

            if (cellCount < 1)
            {
                Debug.LogError($"[ChipShapeLibrary] 칩은 1칸 이상이어야 합니다. (cellCount: {cellCount})");
                return new List<string>();
            }

            shapes = new List<string>();
            HashSet<string> added = new HashSet<string>(); // 이미 넣은 모양인지 빨리 확인하려고 따로 둔다

            // 한 칸 작은 모양들의 빈 이웃 칸에 하나씩 붙여 본다
            foreach (string smaller in GetShapes(cellCount - 1))
            {
                Vector2Int[] cells = ToCells(smaller);
                foreach (Vector2Int cell in cells)
                {
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int next = cell + direction;
                        if (Contains(cells, next))
                            continue;

                        // 돌리면 같아지는 모양은 대표 문자열이 같으므로 한 번만 들어간다
                        string shape = Canonical(AddCell(cells, next));
                        if (added.Contains(shape))
                            continue;

                        added.Add(shape);
                        shapes.Add(shape);
                    }
                }
            }

            shapesByCount[cellCount] = shapes;
            return shapes;
        }

        // "10/11" -> (0,0) (0,1) (1,1). '/'로 자른 덩어리가 한 행(위에서부터 y), 각 글자가 열(x), '1'인 칸만 사용
        public static Vector2Int[] ToCells(string shape)
        {
            string[] rows = shape.Split('/');
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    if (rows[y][x] == '1')
                        cells.Add(new Vector2Int(x, y));
                }
            }

            return cells.ToArray();
        }

        // 칸 좌표 -> "10/11". ToCells의 반대. 좌상단에 붙어 있지 않아도 된다
        public static string ToShape(Vector2Int[] cells)
        {
            cells = Normalize(cells);

            int width = 0;
            int height = 0;
            foreach (Vector2Int cell in cells)
            {
                width = Mathf.Max(width, cell.x + 1);
                height = Mathf.Max(height, cell.y + 1);
            }

            string shape = "";
            for (int y = 0; y < height; y++)
            {
                if (y > 0)
                    shape += "/";
                for (int x = 0; x < width; x++)
                    shape += Contains(cells, new Vector2Int(x, y)) ? "1" : "0";
            }

            return shape;
        }

        // 같은 모양이라도 돌린 방향마다 문자열이 다르므로, 네 방향 문자열 중 사전순으로 가장 앞서는 것을 대표로 삼는다
        private static string Canonical(Vector2Int[] cells)
        {
            string best = ToShape(cells);
            for (int i = 0; i < 3; i++)
            {
                cells = RotateClockwise(cells);
                string rotated = ToShape(cells);
                if (string.CompareOrdinal(rotated, best) < 0)
                    best = rotated;
            }

            return best;
        }

        // ChipItemView.RotateClockwise와 같은 시계 방향 90도 회전. (x, y) -> (Height-1-y, x)
        private static Vector2Int[] RotateClockwise(Vector2Int[] cells)
        {
            cells = Normalize(cells);

            int height = 0;
            foreach (Vector2Int cell in cells)
                height = Mathf.Max(height, cell.y + 1);

            Vector2Int[] rotated = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                rotated[i] = new Vector2Int(height - 1 - cells[i].y, cells[i].x);

            return rotated;
        }

        // 가장 왼쪽 칸이 x=0, 가장 위쪽 칸이 y=0이 되도록 옮긴다
        private static Vector2Int[] Normalize(Vector2Int[] cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
            }

            Vector2Int[] moved = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                moved[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);

            return moved;
        }

        // 기존 칸 배열 끝에 칸 하나를 더한 새 배열
        private static Vector2Int[] AddCell(Vector2Int[] cells, Vector2Int cell)
        {
            Vector2Int[] result = new Vector2Int[cells.Length + 1];
            for (int i = 0; i < cells.Length; i++)
                result[i] = cells[i];
            result[cells.Length] = cell;
            return result;
        }

        private static bool Contains(Vector2Int[] cells, Vector2Int target)
        {
            foreach (Vector2Int cell in cells)
            {
                if (cell == target)
                    return true;
            }

            return false;
        }

        // 정사각형 그리드라 돌려도 들어가는 여부는 같다. 가로/세로 모두 gridSize 이하이면 들어간다
        private static bool Fits(string shape, int gridSize)
        {
            string[] rows = shape.Split('/');
            return rows[0].Length <= gridSize && rows.Length <= gridSize;
        }
    }
}
