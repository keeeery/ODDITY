using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace TeamOdd.UI
{
    // 씬 시작 시 한 번, 플레이어가 실제로 설 수 있는 범위를 물리로 조사해서 위에서 내려다본 마스크 텍스처(설 수 있음 1, 없음 0)로 굽는다.
    // 1) 격자 칸마다 위에서 플레이어 반경의 구를 떨어뜨려 바닥을 찾고(플레이어보다 작은 구멍은 구가 걸려 바닥으로 잡힘)
    // 2) 그 자리에 플레이어 캡슐이 들어가는지 확인하고
    // 3) 플레이어 위치에서 이어진 칸만 남긴 뒤
    // 4) 플레이어 몸이 닿는 범위까지 반경만큼 넓힌다.
    public static class MinimapWalkableBaker
    {
        private const int MinCommandsPerJob = 256;
        // 시작 위치가 바닥이 아닐 때 주변에서 가장 가까운 바닥 칸을 찾을 거리(칸)
        private const int StartSearchCells = 8;

        public readonly struct Settings
        {
            public readonly LayerMask Layers;
            public readonly float TopHeight;
            public readonly float BottomHeight;
            public readonly float CellSize;
            public readonly int MaxCellsPerSide;
            public readonly float Radius;
            public readonly float Height;
            public readonly float StepOffset;
            public readonly bool ReachableOnly;

            public Settings(LayerMask layers, float topHeight, float bottomHeight, float cellSize, int maxCellsPerSide,
                float radius, float height, float stepOffset, bool reachableOnly)
            {
                Layers = layers;
                TopHeight = topHeight;
                BottomHeight = bottomHeight;
                CellSize = cellSize;
                MaxCellsPerSide = maxCellsPerSide;
                Radius = radius;
                Height = height;
                StepOffset = stepOffset;
                ReachableOnly = reachableOnly;
            }
        }

        public sealed class Result
        {
            public Texture2D Texture;
            // 텍스처 (0, 0) 모서리의 월드 xz와 텍스처 전체가 덮는 월드 크기
            public Vector2 Origin;
            public Vector2 Size;
        }

        private readonly struct Grid
        {
            public readonly Vector2 Origin;
            public readonly float CellSize;
            public readonly int Width;
            public readonly int Height;

            public Grid(Vector2 origin, float cellSize, int width, int height)
            {
                Origin = origin;
                CellSize = cellSize;
                Width = width;
                Height = height;
            }

            public int Count => Width * Height;

            public Vector3 CellCenter(int index, float y) =>
                new(Origin.x + (index % Width + 0.5f) * CellSize, y, Origin.y + (index / Width + 0.5f) * CellSize);
        }

        public static Result Bake(Settings settings, Vector3 startPosition)
        {
            if (TryGetBounds(settings, out Vector2 boundsMin, out Vector2 boundsMax) == false) return null;

            // 바깥 테두리 한 칸은 항상 비워서 텍스처 가장자리가 Clamp로 늘어나 보이지 않게 한다.
            Vector2 extent = boundsMax - boundsMin;
            float cellSize = Mathf.Max(settings.CellSize, Mathf.Max(extent.x, extent.y) / (settings.MaxCellsPerSide - 2));
            Grid grid = new Grid(boundsMin - Vector2.one * cellSize, cellSize,
                Mathf.CeilToInt(extent.x / cellSize) + 2, Mathf.CeilToInt(extent.y / cellSize) + 2);

            float[] floorHeights = FindFloors(settings, grid);
            bool[] standable = CheckClearance(settings, grid, floorHeights);
            if (settings.ReachableOnly) KeepReachable(settings, grid, standable, floorHeights, startPosition);
            bool[] walkable = Dilate(grid, standable, Mathf.RoundToInt(settings.Radius / cellSize));

            Texture2D texture = new Texture2D(grid.Width, grid.Height, TextureFormat.R8, false)
            {
                name = "MinimapWalkable",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            byte[] pixels = new byte[grid.Count];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = walkable[index] ? byte.MaxValue : byte.MinValue;
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);

            return new Result
            {
                Texture = texture,
                Origin = grid.Origin,
                Size = new Vector2(grid.Width * cellSize, grid.Height * cellSize),
            };
        }

        // 조사할 레이어의 콜라이더 중 높이 범위에 걸치는 것들로 굽는 범위(xz)를 정한다.
        private static bool TryGetBounds(Settings settings, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool found = false;
            foreach (Collider target in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (target.isTrigger || target.enabled == false) continue;
                if ((settings.Layers.value & (1 << target.gameObject.layer)) == 0) continue;

                Bounds bounds = target.bounds;
                if (bounds.max.y < settings.BottomHeight || bounds.min.y > settings.TopHeight) continue;

                min = Vector2.Min(min, new Vector2(bounds.min.x, bounds.min.z));
                max = Vector2.Max(max, new Vector2(bounds.max.x, bounds.max.z));
                found = true;
            }
            return found;
        }

        // 칸마다 topHeight 위에서 구를 떨어뜨려 처음 닿는 바닥 높이를 찾는다. 바닥이 없거나 bottomHeight보다 낮으면 NaN
        private static float[] FindFloors(Settings settings, Grid grid)
        {
            float startY = settings.TopHeight + settings.Radius;
            float distance = settings.TopHeight - settings.BottomHeight;
            QueryParameters query = new QueryParameters(settings.Layers, false, QueryTriggerInteraction.Ignore, false);

            float[] floorHeights = new float[grid.Count];
            NativeArray<SpherecastCommand> commands = new NativeArray<SpherecastCommand>(grid.Count, Allocator.TempJob);
            NativeArray<RaycastHit> hits = new NativeArray<RaycastHit>(grid.Count, Allocator.TempJob);
            try
            {
                for (int index = 0; index < grid.Count; index++)
                    commands[index] = new SpherecastCommand(grid.CellCenter(index, startY), settings.Radius, Vector3.down, query, distance);
                SpherecastCommand.ScheduleBatch(commands, hits, MinCommandsPerJob, 1).Complete();

                for (int index = 0; index < grid.Count; index++)
                {
                    RaycastHit hit = hits[index];
                    // 구가 멈춘 위치의 밑바닥 = 플레이어 발이 놓일 높이
                    floorHeights[index] = hit.collider != null ? startY - hit.distance - settings.Radius : float.NaN;
                }
            }
            finally
            {
                commands.Dispose();
                hits.Dispose();
            }
            return floorHeights;
        }

        // 바닥 위 계단 높이부터 키까지 플레이어 캡슐이 다른 콜라이더와 겹치지 않는 칸만 설 수 있다고 본다.
        // 구가 벽 안에서 출발해 벽을 뚫고 바닥에 닿은 칸도 여기서 걸러진다.
        private static bool[] CheckClearance(Settings settings, Grid grid, float[] floorHeights)
        {
            bool[] standable = new bool[grid.Count];
            List<int> candidates = new List<int>();
            for (int index = 0; index < grid.Count; index++)
                if (float.IsNaN(floorHeights[index]) == false) candidates.Add(index);
            if (candidates.Count == 0) return standable;

            // 벽에 붙어 선 칸까지 막히지 않도록 반경을 조금 줄인다.
            float radius = settings.Radius * 0.9f;
            float bottom = settings.StepOffset + radius;
            float top = Mathf.Max(bottom, settings.Height - radius);
            QueryParameters query = new QueryParameters(settings.Layers, false, QueryTriggerInteraction.Ignore, false);

            NativeArray<OverlapCapsuleCommand> commands = new NativeArray<OverlapCapsuleCommand>(candidates.Count, Allocator.TempJob);
            NativeArray<ColliderHit> hits = new NativeArray<ColliderHit>(candidates.Count, Allocator.TempJob);
            try
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    int cell = candidates[index];
                    Vector3 foot = grid.CellCenter(cell, floorHeights[cell]);
                    commands[index] = new OverlapCapsuleCommand(foot + Vector3.up * bottom, foot + Vector3.up * top, radius, query);
                }
                OverlapCapsuleCommand.ScheduleBatch(commands, hits, MinCommandsPerJob, 1).Complete();

                for (int index = 0; index < candidates.Count; index++)
                    standable[candidates[index]] = hits[index].collider == null;
            }
            finally
            {
                commands.Dispose();
                hits.Dispose();
            }
            return standable;
        }

        // 시작 위치에서 상하좌우로 이어진 칸만 남긴다. 이웃 칸과 높이 차가 계단 높이 + 한 칸(45도 경사)보다 크면 끊긴 것으로 본다.
        private static void KeepReachable(Settings settings, Grid grid, bool[] standable, float[] floorHeights, Vector3 startPosition)
        {
            int start = FindStartCell(grid, standable, startPosition);
            if (start < 0) return;

            float maxClimb = settings.StepOffset + grid.CellSize;
            bool[] reached = new bool[grid.Count];
            Queue<int> queue = new Queue<int>();
            reached[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                int x = cell % grid.Width;
                int z = cell / grid.Width;
                TryVisit(x + 1, z);
                TryVisit(x - 1, z);
                TryVisit(x, z + 1);
                TryVisit(x, z - 1);

                void TryVisit(int nextX, int nextZ)
                {
                    if (nextX < 0 || nextZ < 0 || nextX >= grid.Width || nextZ >= grid.Height) return;

                    int next = nextZ * grid.Width + nextX;
                    if (reached[next] || standable[next] == false) return;
                    if (Mathf.Abs(floorHeights[next] - floorHeights[cell]) > maxClimb) return;

                    reached[next] = true;
                    queue.Enqueue(next);
                }
            }

            for (int index = 0; index < grid.Count; index++)
                standable[index] = reached[index];
        }

        private static int FindStartCell(Grid grid, bool[] standable, Vector3 position)
        {
            int startX = Mathf.FloorToInt((position.x - grid.Origin.x) / grid.CellSize);
            int startZ = Mathf.FloorToInt((position.z - grid.Origin.y) / grid.CellSize);
            int best = -1;
            int bestDistance = int.MaxValue;
            for (int dz = -StartSearchCells; dz <= StartSearchCells; dz++)
            {
                for (int dx = -StartSearchCells; dx <= StartSearchCells; dx++)
                {
                    int x = startX + dx;
                    int z = startZ + dz;
                    if (x < 0 || z < 0 || x >= grid.Width || z >= grid.Height) continue;

                    int cell = z * grid.Width + x;
                    int distance = dx * dx + dz * dz;
                    if (standable[cell] == false || distance >= bestDistance) continue;

                    best = cell;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // 플레이어 중심이 설 수 있는 칸에서 반경만큼 넓혀 몸이 닿는 범위까지 바닥으로 표시한다.
        private static bool[] Dilate(Grid grid, bool[] source, int radiusCells)
        {
            if (radiusCells <= 0) return source;

            bool[] result = new bool[grid.Count];
            int radiusSquared = radiusCells * radiusCells;
            for (int cell = 0; cell < grid.Count; cell++)
            {
                if (source[cell] == false) continue;

                int x = cell % grid.Width;
                int z = cell / grid.Width;
                for (int dz = -radiusCells; dz <= radiusCells; dz++)
                {
                    int nextZ = z + dz;
                    if (nextZ < 0 || nextZ >= grid.Height) continue;

                    for (int dx = -radiusCells; dx <= radiusCells; dx++)
                    {
                        int nextX = x + dx;
                        if (nextX < 0 || nextX >= grid.Width || dx * dx + dz * dz > radiusSquared) continue;

                        result[nextZ * grid.Width + nextX] = true;
                    }
                }
            }
            return result;
        }
    }
}
