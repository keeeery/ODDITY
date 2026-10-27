using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TeamOdd.EditorTools
{
    // 밀폐형 아레나 배치 메뉴들이 함께 쓰는 배치 도구
    // 프리팹마다 피벗이 제각각이라 렌더러 바운드를 재서 위치를 맞추고, 타일과 벽 모듈 간격도 잰 크기로 정한다.
    // 모든 아레나는 같은 크기(안쪽 HalfSize*2)와 천장 높이(CeilingHeight)를 쓰며,
    // 추후 중력 반전 스킬을 고려해 천장은 바닥을 뒤집은 형태로, 캣워크는 높이 가운데에 둔다.
    internal sealed class ArenaKit
    {
        public const string SciFiPrefabs = "Assets/Sci-Fi Base/Prefabs";
        public const float CeilingHeight = 15.34f;
        // 천장판 가장자리와 벽 윗단 사이로 빛이 새지 않도록 벽은 천장보다 이만큼 더 올림
        public const float CeilingInset = 0.3f;

        public enum Anchor { Bottom, Top, Center }

        public readonly Vector3 Center;
        public readonly float HalfSize;
        public readonly int GroundLayer = LayerMask.NameToLayer("Ground");
        public readonly int WallLayer = LayerMask.NameToLayer("Wall");
        public readonly Random Random;
        private readonly Dictionary<string, Vector3> sizes = new();

        public ArenaKit(Vector3 center, float halfSize, int seed)
        {
            Center = center;
            HalfSize = halfSize;
            Random = new Random(seed);
        }

        public float WallTop => CeilingHeight + CeilingInset;
        public float MidHeight => CeilingHeight * 0.5f;

        // 이전에 만든 같은 이름의 아레나를 지우고 새 루트를 만든다. parent가 없으면 씬 최상위에 둔다.
        public static Transform CreateRoot(Scene scene, Transform parent, string name)
        {
            Transform previous = null;
            if (parent != null) previous = parent.Find(name);
            else
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                    if (rootObject.name == name) previous = rootObject.transform;
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);

            Transform root = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Arena");
            if (parent != null) root.SetParent(parent, false);
            else SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            return root;
        }

        public static Transform Group(Transform root, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        public string Pick(IReadOnlyList<string> names) => names[Random.Next(names.Count)];

        // side 0이 남쪽 벽(-z)이고 90도씩 돌며 서·북·동 벽, along은 변을 따라간 거리, depth는 가운데 기준 변 방향(-) 거리
        public Vector3 SidePoint(int side, float along, float height, float depth)
        {
            return Center + Quaternion.Euler(0f, side * 90f, 0f) * new Vector3(along, height, depth);
        }

        // 프리팹의 긴 쪽이 벽을 따라가도록 하는 회전값
        public float YawAlongWall(string prefabName)
        {
            Vector3 size = Measure(prefabName, 0f);
            return size.x >= size.z ? 0f : 90f;
        }

        // 바닥(또는 flip이면 천장)을 타일로 채움. 바닥은 윗면을, 천장은 뒤집어서 아랫면을 height에 맞춘다.
        // 크기가 첫 번째와 다른 후보는 틈이 생기므로 뺀다.
        public void TileArea(Transform parent, string[] candidates, float height, bool flip, int layer)
        {
            Vector3 step = Measure(candidates[0], 0f);
            List<string> tiles = new List<string>(candidates).FindAll(name => Approximately(Measure(name, 0f), step));
            int countX = Mathf.CeilToInt(HalfSize * 2f / step.x);
            int countZ = Mathf.CeilToInt(HalfSize * 2f / step.z);
            Vector3 origin = Center - new Vector3((countX - 1) * step.x, 0f, (countZ - 1) * step.z) * 0.5f;
            bool square = Mathf.Abs(step.x - step.z) < 0.05f;

            for (int x = 0; x < countX; x++)
            for (int z = 0; z < countZ; z++)
            {
                float yaw = square ? Random.Next(4) * 90f : Random.Next(2) * 180f;
                Quaternion rotation = Quaternion.Euler(flip ? 180f : 0f, yaw, 0f);
                Vector3 position = origin + new Vector3(x * step.x, height, z * step.z);
                Place(Pick(tiles), parent, position, rotation, flip ? Anchor.Bottom : Anchor.Top, layer);
            }
        }

        // 바깥 지붕을 타일로 덮음 (중력 반전과 무관한 외관용)
        public void Roof(Transform parent, string[] tiles, float bottom, float outer)
        {
            Vector3 step = Measure(tiles[0], 0f);
            int count = Mathf.CeilToInt(outer * 2f / step.x);
            Vector3 origin = Center - new Vector3((count - 1) * step.x, 0f, (count - 1) * step.z) * 0.5f;
            for (int x = 0; x < count; x++)
            for (int z = 0; z < count; z++)
            {
                Vector3 position = origin + new Vector3(x * step.x, bottom, z * step.z);
                Place(Pick(tiles), parent, position, Quaternion.Euler(0f, Random.Next(4) * 90f, 0f), Anchor.Bottom, -1);
            }
        }

        // 벽 모듈 한 줄을 네 변에 두름. outward만큼 바깥으로 물려 세울 수 있고, pick으로 자리마다 다른 모듈을 고를 수 있다.
        // 반환값은 이 줄의 높이
        public float WallRing(Transform parent, string module, float bottom, int layer, float outward = 0f, Func<int, float, string> pick = null)
        {
            Vector3 size = Measure(module, 0f);
            float baseYaw = YawAlongWall(module);
            float length = Mathf.Max(size.x, size.z);
            float thickness = Mathf.Min(size.x, size.z);
            float edge = HalfSize + outward + thickness * 0.5f;
            int count = Mathf.CeilToInt((HalfSize + outward) * 2f / length);

            for (int side = 0; side < 4; side++)
            for (int index = 0; index < count; index++)
            {
                float along = (index - (count - 1) * 0.5f) * length;
                string prefab = pick?.Invoke(side, along) ?? module;
                EnsureCollider(Place(prefab, parent, SidePoint(side, along, bottom, -edge), side * 90f + baseYaw, Anchor.Bottom, layer));
            }

            return size.y;
        }

        // 벽 모듈을 아래부터 WallTop까지 쌓음. 윗단이 고르지 않은 모듈은 overlap만큼 겹쳐 이음매로 바깥이 비치지 않게 한다.
        // 반환값은 각 줄의 바닥 높이
        public List<float> StackWalls(Transform parent, string module, float overlap, int layer, Func<int, int, float, string> pick = null)
        {
            float height = Measure(module, 0f).y;
            List<float> bottoms = new();
            for (float bottom = 0f; ; bottom += height - overlap)
            {
                int row = bottoms.Count;
                bottoms.Add(bottom);
                WallRing(parent, module, bottom, layer, 0f, pick == null ? null : (side, along) => pick(row, side, along));
                if (bottom + height >= WallTop - 0.01f) break;
            }
            return bottoms;
        }

        // 네 모서리 틈에 기둥을 세움
        public void CornerPosts(Transform parent, string prefab, float bottom, float edge, int layer)
        {
            for (int side = 0; side < 4; side++)
            {
                Vector3 position = Center + Quaternion.Euler(0f, side * 90f, 0f) * new Vector3(edge, bottom, edge);
                EnsureCollider(Place(prefab, parent, position, side * 90f, Anchor.Bottom, layer));
            }
        }

        // 벽 안쪽 면에 붙여 놓음. bottom은 바운드 아랫면 높이, flip이면 위아래를 뒤집어 천장 쪽 대칭으로 쓴다.
        public GameObject PlaceOnWall(string prefabName, Transform parent, int side, float along, float bottom, int layer, bool flip = false, float scale = 1f, float protrude = -1f)
        {
            float yaw = YawAlongWall(prefabName);
            Vector3 size = Measure(prefabName, yaw) * scale;
            // protrude를 주면 그만큼만 안쪽으로 튀어나오고 나머지는 벽 속에 묻힌다.
            float inward = protrude >= 0f ? protrude : size.z;
            Vector3 position = SidePoint(side, along, bottom, -HalfSize + inward - size.z * 0.5f);
            // 벽을 바라보는 면이 그대로 안쪽을 향하도록 앞뒤축(z)으로 굴려서 뒤집음
            Quaternion rotation = Quaternion.Euler(0f, side * 90f + yaw, 0f) * Quaternion.Euler(0f, 0f, flip ? 180f : 0f);
            return Place(prefabName, parent, position, rotation, Anchor.Bottom, layer, scale);
        }

        // 높이 가운데에 캣워크와 난간을 두름, 모서리는 정사각형 판으로 이음. skip이 true인 자리는 비운다.
        public void Catwalk(Transform parent, string catwalk, string corner, string railing, Func<int, float, bool> skip = null)
        {
            Vector3 size = Measure(catwalk, 0f);
            float length = Mathf.Max(size.x, size.z);
            float depth = Mathf.Min(size.x, size.z);
            float height = MidHeight - size.y * 0.5f;
            int count = Mathf.RoundToInt((HalfSize - depth) * 2f / length);

            for (int side = 0; side < 4; side++)
            for (int index = 0; index < count; index++)
            {
                float along = (index - (count - 1) * 0.5f) * length;
                if (skip != null && skip(side, along)) continue;
                PlaceOnWall(catwalk, parent, side, along, height, GroundLayer);
                Place(railing, parent, SidePoint(side, along, height, -HalfSize + depth), side * 90f + YawAlongWall(railing), Anchor.Bottom, -1);
            }

            float cornerOffset = HalfSize - depth * 0.5f;
            for (int side = 0; side < 4; side++)
            {
                Vector3 position = Center + Quaternion.Euler(0f, side * 90f, 0f) * new Vector3(cornerOffset, height, cornerOffset);
                Place(corner, parent, position, side * 90f, Anchor.Bottom, GroundLayer);
            }
        }

        // 바닥·천장의 밟히는 면을 맡는 넓은 콜라이더, top이면 윗면을, 아니면 아랫면을 height에 맞춤
        public void Slab(Transform parent, string name, float height, bool top, float halfExtent)
        {
            GameObject slab = new(name) { layer = GroundLayer };
            slab.transform.SetParent(parent, false);
            slab.transform.position = Center + Vector3.up * (top ? height - 0.5f : height + 0.5f);
            slab.AddComponent<BoxCollider>().size = new Vector3(halfExtent * 2f, 1f, halfExtent * 2f);
        }

        public Light PointLight(Transform parent, Vector3 position, Color color, float range, float intensity)
        {
            Light light = new GameObject("Arena Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // 밀폐 공간이라 하늘빛 대신 단색 환경광과 반사 세기로 분위기를 잡는다.
        public static void SetMood(Color ambient, float reflection, bool fog, Color fogColor = default, float fogDensity = 0f)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.reflectionIntensity = reflection;
            RenderSettings.fog = fog;
            if (fog)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }
            DynamicGI.UpdateEnvironment();
        }

        public GameObject Place(string prefabName, Transform parent, Vector3 position, float yaw, Anchor anchor, int layer, float scale = 1f)
        {
            return Place(prefabName, parent, position, Quaternion.Euler(0f, yaw, 0f), anchor, layer, scale);
        }

        // 프리팹을 놓고 렌더러 바운드 기준으로 밑면·윗면·가운데가 position에 오도록 옮긴다.
        public GameObject Place(string prefabName, Transform parent, Vector3 position, Quaternion rotation, Anchor anchor, int layer, float scale = 1f)
        {
            GameObject prefab = LoadPrefab(prefabName);
            if (prefab == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            if (Mathf.Approximately(scale, 1f) == false) instance.transform.localScale *= scale;

            if (TryGetBounds(instance, out Bounds bounds))
            {
                float y = anchor switch
                {
                    Anchor.Top => bounds.max.y,
                    Anchor.Center => bounds.center.y,
                    _ => bounds.min.y,
                };
                instance.transform.position += position - new Vector3(bounds.center.x, y, bounds.center.z);
            }

            if (layer >= 0)
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = layer;

            return instance;
        }

        // 벽은 파편 생성과 보스 충돌에 콜라이더가 꼭 필요하므로, 없으면 바운드 크기의 BoxCollider를 붙인다.
        public static GameObject EnsureCollider(GameObject instance)
        {
            if (instance == null || instance.GetComponentInChildren<Collider>() != null || TryGetBounds(instance, out Bounds bounds) == false) return instance;

            BoxCollider box = instance.AddComponent<BoxCollider>();
            Transform transform = instance.transform;
            box.center = transform.InverseTransformPoint(bounds.center);
            Vector3 size = transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            return instance;
        }

        // 회전한 상태의 월드 바운드 크기, 임시로 놓았다가 바로 지운다.
        public Vector3 Measure(string prefabName, float yaw)
        {
            string key = prefabName + "@" + yaw;
            if (sizes.TryGetValue(key, out Vector3 cached)) return cached;

            GameObject prefab = LoadPrefab(prefabName);
            if (prefab == null) return Vector3.zero;

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            Vector3 size = TryGetBounds(probe, out Bounds bounds) ? bounds.size : Vector3.zero;
            Object.DestroyImmediate(probe);
            sizes[key] = size;
            return size;
        }

        public static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer is not MeshRenderer and not SkinnedMeshRenderer) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }
            return found;
        }

        // '/'가 있으면 Assets 기준 경로, 없으면 Sci-Fi Base 프리팹 이름
        private static GameObject LoadPrefab(string prefabName)
        {
            string path = prefabName.Contains("/") ? prefabName : $"{SciFiPrefabs}/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogWarning($"[Arena] 프리팹을 찾지 못했습니다: {prefabName}");
            return prefab;
        }

        private static bool Approximately(Vector3 a, Vector3 b) => Mathf.Abs(a.x - b.x) < 0.05f && Mathf.Abs(a.z - b.z) < 0.05f;
    }
}
