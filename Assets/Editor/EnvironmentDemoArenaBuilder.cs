using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd.EditorTools
{
    // EnvironmentDemo 씬에 Sci-Fi Base 에셋으로 밀폐된 사막 기지 콘크리트 아레나를 데모 배치하는 메뉴
    // 공통 배치는 ArenaKit이 맡고, 여기서는 쓸 에셋과 배치 위치만 정한다.
    // 다시 실행하면 이전에 만든 아레나(ArenaRootName)를 지우고 새로 만든다.
    internal static class EnvironmentDemoArenaBuilder
    {
        private const string ScenePath = "Assets/Scenes/8. Environmental Mechanics/EnvironmentDemo.unity";
        private const string SandMaterialPath = "Assets/Materials/Environment/EnvironmentDemo_Sand.mat";
        private const string ArenaRootName = "Sci-Fi Arena";

        private static readonly string[] FloorPlates = { "Hangar_Ground_Plate1", "Hangar_Ground_Plate1", "Hangar_Ground_Plate3", "Hangar_Ground_Plate5" };
        private static readonly string[] RoofTiles = { "Concrete_Ground1", "Concrete_Ground2", "Concrete_Ground3", "Concrete_Ground4" };
        private const string WallModule = "Concrete_wall2";
        // 벽 모듈 윗단이 고르지 않아 그대로 쌓으면 이음매로 바깥이 비치므로 위 단을 이만큼 내려 겹침
        private const float WallRowOverlap = 0.5f;

        // 벽 한 변에서 받침과 벽등을 둘 위치(변 가운데 기준 거리), 캣워크 이음매에 맞춘 값
        private static readonly float[] ButtressSpots = { -15f, -3f, 3f, 15f };
        private static readonly float[] LampSpots = { -9f, 9f };

        [MenuItem("Tools/Team Odd/Build Environment Demo Arena")]
        private static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            // 플레이어(z -5)와 보스(z 9) 사이가 가운데 오도록 잡은 아레나
            ArenaKit kit = new(new Vector3(0f, 0f, 2f), 24f, 1026);
            Transform root = ArenaKit.CreateRoot(scene, FindInScene(scene, "Stage"), ArenaRootName);

            // 바닥 아래는 기존 Floor 큐브가 받치므로 따로 콜라이더를 두지 않는다.
            kit.TileArea(ArenaKit.Group(root, "Floor Plates"), FloorPlates, 0f, flip: false, kit.GroundLayer);
            BuildWalls(kit, ArenaKit.Group(root, "Walls"));
            BuildCeiling(kit, ArenaKit.Group(root, "Ceiling"));
            kit.Catwalk(ArenaKit.Group(root, "Catwalk"), "Wall_Hangar1_Catwalk1", "Wall_Hangar1_Catwalk3", "Wall_Hangar1_Barrier1");
            BuildLights(kit, ArenaKit.Group(root, "Lights"));
            BuildInteriorProps(kit, ArenaKit.Group(root, "Interior Props"));
            BuildBackdrop(kit, ArenaKit.Group(root, "Backdrop"));
            SetupStage(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            List<string> counts = new();
            foreach (Transform group in root) counts.Add($"{group.name} {group.childCount}");
            Debug.Log($"[Arena] 아레나 배치 완료: {string.Join(", ", counts)}");
        }

        // 네 변을 콘크리트 벽 2단으로 막고, 모서리에 기둥을, 1단 안쪽에 받침을, 남쪽에 출입문을 둔다.
        private static void BuildWalls(ArenaKit kit, Transform parent)
        {
            List<float> rows = kit.StackWalls(parent, WallModule, WallRowOverlap, kit.WallLayer);
            float thickness = kit.Measure(WallModule, 0f).z;
            foreach (float bottom in rows)
                kit.CornerPosts(parent, "Concrete_wall2_courA", bottom, kit.HalfSize + thickness * 0.5f, kit.WallLayer);

            for (int side = 0; side < 4; side++)
            foreach (float along in ButtressSpots)
                ArenaKit.EnsureCollider(kit.PlaceOnWall("Concrete_wall2_Support1B", parent, side, along, 0f, kit.WallLayer));

            // 플레이어 뒤쪽(남쪽) 벽 가운데에 출입문을 덧붙임
            kit.PlaceOnWall("Wall_Hangar1_Doors", parent, 0, 0f, 0f, kit.WallLayer);
        }

        // 천장은 바닥판을 뒤집어 붙이고, 밟히는 면은 슬래브 콜라이더 하나가 맡는다. 바깥 지붕은 콘크리트로 덮는다.
        private static void BuildCeiling(ArenaKit kit, Transform parent)
        {
            kit.TileArea(parent, FloorPlates, ArenaKit.CeilingHeight, flip: true, kit.GroundLayer);
            float outer = kit.HalfSize + kit.Measure(WallModule, 0f).z;
            kit.Roof(parent, RoofTiles, ArenaKit.CeilingHeight + kit.Measure(FloorPlates[0], 0f).y, outer);
            kit.Slab(parent, "Ceiling Collider", ArenaKit.CeilingHeight, top: false, outer);
        }

        // 벽등을 위아래 대칭 높이에 달고, 각 벽등 앞에 포인트 라이트를 둠 (천장이 막혀 햇빛이 들지 않음)
        private static void BuildLights(ArenaKit kit, Transform parent)
        {
            float[] heights = { ArenaKit.CeilingHeight * 0.28f, ArenaKit.CeilingHeight * 0.72f };
            for (int side = 0; side < 4; side++)
            foreach (float along in LampSpots)
            foreach (float height in heights)
            {
                kit.PlaceOnWall("Lamp_Wall", parent, side, along, height - 1f, -1);
                kit.PointLight(parent, kit.SidePoint(side, along, height, -kit.HalfSize + 3f), new Color(1f, 0.93f, 0.82f), 20f, 3f);
            }
        }

        // 보스가 돌진할 가운데는 비워 두고 모서리에 드럼통, 벽에 파워박스만 둔다.
        private static void BuildInteriorProps(ArenaKit kit, Transform parent)
        {
            string[] barrels = { "Prop_Barrel1", "Prop_Barrel1 gray", "Prop_Barrel1 yellow" };
            for (int side = 0; side < 4; side++)
            {
                Quaternion rotation = Quaternion.Euler(0f, side * 90f, 0f);
                Vector3 corner = kit.Center + rotation * new Vector3(-kit.HalfSize + 4.5f, 0f, -kit.HalfSize + 2f);
                for (int index = 0; index < 3; index++)
                {
                    Vector3 offset = rotation * new Vector3(index * 1.2f, 0f, (index % 2) * 1.2f);
                    kit.Place(barrels[index], parent, corner + offset, kit.Random.Next(360), ArenaKit.Anchor.Bottom, -1);
                }

                kit.PlaceOnWall("Powerbox_0" + (side + 1), parent, side, 6f, 1.2f, -1);
            }
        }

        // 벽 바깥에 큰 기지 시설만 둘러 세움, 작은 소품은 멀리서 점처럼 보여 뺐다.
        private static void BuildBackdrop(ArenaKit kit, Transform parent)
        {
            (string prefab, float angle, float distance)[] items =
            {
                ("Generator_Set_02", 15f, 44f), ("SupplyTower_01", 50f, 46f), ("Radiator_Big", 80f, 40f),
                ("Antenna_Big", 110f, 50f), ("Fuel_Tank_01", 140f, 42f), ("Building_04_B", 170f, 50f),
                ("Generator_Set_01", 200f, 44f), ("Crane_01", 230f, 44f), ("Building_04_A", 260f, 46f),
                ("Generator_Set_03", 295f, 46f), ("SupplyTower_02", 330f, 44f),
            };

            foreach ((string prefab, float angle, float distance) in items)
            {
                Vector3 size = kit.Measure(prefab, 0f);
                // 큰 시설이 벽을 파고들지 않도록 반지름만큼 더 밀어낸다.
                float radius = distance + Mathf.Max(size.x, size.z) * 0.5f;
                Vector3 position = kit.Center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                kit.Place(prefab, parent, position, angle + 180f, ArenaKit.Anchor.Bottom, -1);
            }
        }

        // 바깥 바닥을 모래로 바꾸고, 테스트용 Back Wall을 끄고, 하늘·안개·태양 색을 사막 톤으로 맞춘다.
        private static void SetupStage(Scene scene)
        {
            Transform floor = FindInScene(scene, "Stage/Floor");
            if (floor != null && floor.TryGetComponent(out MeshRenderer floorRenderer))
            {
                Undo.RecordObject(floorRenderer, "Build Environment Demo Arena");
                floorRenderer.sharedMaterial = GetOrCreateSandMaterial();
            }

            Transform backWall = FindInScene(scene, "Stage/Back Wall");
            if (backWall != null)
            {
                Undo.RecordObject(backWall.gameObject, "Build Environment Demo Arena");
                backWall.gameObject.SetActive(false);
            }

            Transform sun = FindInScene(scene, "Lights/Directional Light");
            if (sun != null && sun.TryGetComponent(out Light light))
            {
                Undo.RecordObject(light, "Build Environment Demo Arena");
                light.color = new Color(1f, 0.9f, 0.76f);
            }

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Sci-Fi Base/Scenes/Skybox.mat");
            if (skybox != null) RenderSettings.skybox = skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.78f, 0.69f, 0.56f);
            DynamicGI.UpdateEnvironment();
        }

        private static Material GetOrCreateSandMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SandMaterialPath);
            if (material != null) return material;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SandMaterialPath));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sci-Fi Base/Terrain/Textures/Sand1_Base_Color.tif"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sci-Fi Base/Terrain/Textures/Sand1_Normal.tif"));
            material.EnableKeyword("_NORMALMAP");
            // Floor 큐브(300m)의 한 면에 약 4m 간격으로 반복
            material.SetTextureScale("_BaseMap", new Vector2(75f, 75f));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, SandMaterialPath);
            return material;
        }

        private static Transform FindInScene(Scene scene, string path)
        {
            string[] parts = path.Split('/', 2);
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name != parts[0]) continue;
                return parts.Length == 1 ? rootObject.transform : rootObject.transform.Find(parts[1]);
            }
            return null;
        }
    }
}
