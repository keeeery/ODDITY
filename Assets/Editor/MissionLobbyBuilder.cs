using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd.EditorTools
{
    // OZEA STUDIO 프리팹으로 미션 수락 로비(가운데 홀 + 서쪽 서버실 + 동쪽 온실)를 지금 열린 씬에 배치하는 메뉴
    // 로비 가운데는 기존 테스트 로비 스테이지 원점(LobbyCenter)이며, 같은 씬에서 다시 실행하면 이전 로비를 지우고 새로 만든다.
    // OZEA 벽 모듈은 높이 2m라 플레이어(약 1.9m)에 맞춰 구조물은 StructureScale배로, 소품은 거의 원래 크기로 놓는다.
    // 씬에 Player, Mission NPC, Chip NPC, Stage Move NPC가 있으면 로비 배치에 맞는 자리로 옮기고, 예전 임시 바닥(Floor)은 끈다.
    internal static class MissionLobbyBuilder
    {
        private const string RootName = "Mission Lobby";
        private const string Ozea = "Assets/OZEA_STUDIO_ULTIMATE/OZEA_STUDIO_Bundle_Serie_";
        private const float StructureScale = 2f;
        // 벽 모듈 한 칸(2m)과 바닥 타일 한 칸(1m)을 키운 크기
        private const float Module = 4f;
        private const float Tile = 2f;
        // 벽 모듈의 꾸며진 면이 로컬 +x를 보도록 맞추는 회전 보정
        private const float WallFaceOffset = 0f;

        private static readonly Vector3 LobbyCenter = new(4.24262f, 0f, 11.40296f);

        private static readonly string[] WallModules =
        {
            Path("A", "A_001+", "SM_Wall_V1"), Path("A", "A_001+", "SM_Wall_V1"), Path("A", "A_001+", "SM_Wall_V2"),
            Path("A", "A_001+", "SM_Wall_V3"), Path("A", "A_001+", "SM_Wall_V5"), Path("A", "A_005", "SM_Wall_V0"),
        };
        private static readonly string WindowModule = Path("A", "A_003", "SM_WallPanel_Large_Window");
        private static readonly string DoorFrame = Path("A", "A_002", "SM_DoorFrame_Double");
        private static readonly string CornerPost = Path("A", "A_001+", "SM_Corner_Cover_Post");
        private static readonly string FloorPlain = Path("A", "A_005", "SM_Floor_V1");
        private static readonly string FloorInlay = Path("A", "A_005", "SM_Floor_V2");

        private enum Wall { Solid, Window, Doorway, Door }

        private static ArenaKit kit;
        private static readonly HashSet<Vector2Int> posts = new();

        [MenuItem("Tools/Team Odd/Build Mission Lobby")]
        private static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            kit = new ArenaKit(LobbyCenter, 10f, 7);
            posts.Clear();
            Transform root = ArenaKit.CreateRoot(scene, null, RootName);
            root.position = LobbyCenter;

            BuildFloors(ArenaKit.Group(root, "Floor"));
            BuildWalls(ArenaKit.Group(root, "Walls"));
            BuildHall(ArenaKit.Group(root, "Hall Props"));
            BuildServerRoom(ArenaKit.Group(root, "Server Room Props"));
            BuildGreenhouse(ArenaKit.Group(root, "Greenhouse Props"));
            BuildLights(ArenaKit.Group(root, "Lights"));
            MoveActors(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Lobby] 미션 로비를 배치했습니다.");
        }

        // 홀 x[-10,10] z[-8,8], 서버실 x[-18,-10] z[-8,4], 온실 x[10,18] z[-8,4] (로비 가운데 기준)
        private static void BuildFloors(Transform parent)
        {
            TileRoom(parent, "Hall", -10f, 10f, -8f, 8f);
            TileRoom(parent, "Server Room", -18f, -10f, -8f, 4f);
            TileRoom(parent, "Greenhouse", 10f, 18f, -8f, 4f);
        }

        // 2m 타일을 깔고 네 칸에 한 칸씩 가운데가 어두운 타일을 섞는다. 밟는 면은 방마다 하나의 Ground 콜라이더가 맡는다.
        private static void TileRoom(Transform parent, string name, float minX, float maxX, float minZ, float maxZ)
        {
            Transform room = ArenaKit.Group(parent, name);
            for (int x = 0; x < Mathf.RoundToInt((maxX - minX) / Tile); x++)
                for (int z = 0; z < Mathf.RoundToInt((maxZ - minZ) / Tile); z++)
                {
                    string tile = x % 2 == 1 && z % 2 == 1 ? FloorInlay : FloorPlain;
                    kit.Place(tile, room, Local(minX + (x + 0.5f) * Tile, 0f, minZ + (z + 0.5f) * Tile), 0f, ArenaKit.Anchor.Top, kit.GroundLayer, StructureScale);
                }

            GameObject slab = new("Ground Collider") { layer = kit.GroundLayer };
            slab.transform.SetParent(room, false);
            slab.transform.position = Local((minX + maxX) * 0.5f, -0.5f, (minZ + maxZ) * 0.5f);
            slab.AddComponent<BoxCollider>().size = new Vector3(maxX - minX, 1f, maxZ - minZ);
        }

        private static void BuildWalls(Transform parent)
        {
            // 홀: 남쪽 가운데가 출격 문, 서·동쪽 가운데 아래 칸이 옆방으로 이어지는 통로
            WallRun(parent, -10f, 8f, 10f, 8f, 90f, Wall.Solid, Wall.Solid, Wall.Solid, Wall.Solid, Wall.Solid);
            WallRun(parent, -10f, -8f, 10f, -8f, 270f, Wall.Solid, Wall.Solid, Wall.Door, Wall.Solid, Wall.Solid);
            WallRun(parent, -10f, -8f, -10f, 8f, 0f, Wall.Window, Wall.Doorway, Wall.Solid, Wall.Solid);
            WallRun(parent, 10f, -8f, 10f, 8f, 180f, Wall.Window, Wall.Doorway, Wall.Solid, Wall.Solid);

            // 서버실
            WallRun(parent, -18f, 4f, -10f, 4f, 90f, Wall.Solid, Wall.Solid);
            WallRun(parent, -18f, -8f, -10f, -8f, 270f, Wall.Solid, Wall.Solid);
            WallRun(parent, -18f, -8f, -18f, 4f, 0f, Wall.Solid, Wall.Solid, Wall.Solid);

            // 온실: 바깥쪽 벽은 창으로 열어 둠
            WallRun(parent, 10f, 4f, 18f, 4f, 90f, Wall.Solid, Wall.Solid);
            WallRun(parent, 10f, -8f, 18f, -8f, 270f, Wall.Window, Wall.Window);
            WallRun(parent, 18f, -8f, 18f, 4f, 180f, Wall.Window, Wall.Solid, Wall.Window);
        }

        // (fromX,fromZ)에서 (toX,toZ)까지 4m 모듈을 kinds 순서대로 세우고, 모듈 이음매마다 기둥을 하나씩 둔다.
        // inwardYaw는 꾸며진 면(로컬 +x)이 방 안쪽을 보게 하는 회전이다.
        private static void WallRun(Transform parent, float fromX, float fromZ, float toX, float toZ, float inwardYaw, params Wall[] kinds)
        {
            Vector3 from = new(fromX, 0f, fromZ);
            Vector3 step = (new Vector3(toX, 0f, toZ) - from) / kinds.Length;
            float yaw = inwardYaw + WallFaceOffset;

            for (int index = 0; index < kinds.Length; index++)
            {
                Vector3 middle = from + step * (index + 0.5f);
                Vector3 position = Local(middle.x, 0f, middle.z);
                switch (kinds[index])
                {
                    case Wall.Solid:
                        ArenaKit.EnsureCollider(kit.Place(kit.Pick(WallModules), parent, position, yaw, ArenaKit.Anchor.Bottom, kit.WallLayer, StructureScale));
                        break;
                    case Wall.Window:
                        ArenaKit.EnsureCollider(kit.Place(WindowModule, parent, position, yaw, ArenaKit.Anchor.Bottom, kit.WallLayer, StructureScale));
                        break;
                    case Wall.Doorway:
                        DoorFrameWithColliders(parent, position, yaw);
                        break;
                    case Wall.Door:
                        DoorFrameWithColliders(parent, position, yaw);
                        // 닫힌 출격 문, 두 짝을 문틀 가운데 기준 양옆에 둔다.
                        Vector3 along = step.normalized;
                        GameObject left = kit.Place(Path("A", "A_002", "SM_Door_Double_V1L"), parent, position - along * Module * 0.25f, yaw, ArenaKit.Anchor.Bottom, kit.WallLayer, StructureScale);
                        GameObject right = kit.Place(Path("A", "A_002", "SM_Door_Double_V1R"), parent, position + along * Module * 0.25f, yaw, ArenaKit.Anchor.Bottom, kit.WallLayer, StructureScale);
                        ArenaKit.EnsureCollider(left);
                        ArenaKit.EnsureCollider(right);
                        break;
                }
            }

            for (int index = 0; index <= kinds.Length; index++)
            {
                Vector3 joint = from + step * index;
                if (posts.Add(new Vector2Int(Mathf.RoundToInt(joint.x), Mathf.RoundToInt(joint.z))) == false) continue;
                ArenaKit.EnsureCollider(kit.Place(CornerPost, parent, Local(joint.x, 0f, joint.z), 0f, ArenaKit.Anchor.Bottom, kit.WallLayer, StructureScale));
            }
        }

        // 문틀 바운드 전체에 콜라이더를 달면 통로가 막히므로, 양쪽 기둥 자리에만 얇은 콜라이더를 둔다.
        private static void DoorFrameWithColliders(Transform parent, Vector3 position, float yaw)
        {
            GameObject frame = kit.Place(DoorFrame, parent, position, yaw, ArenaKit.Anchor.Bottom, kit.WallLayer, StructureScale);
            if (frame == null) return;
            foreach (float side in new[] { -1f, 1f })
            {
                BoxCollider post = frame.AddComponent<BoxCollider>();
                post.center = frame.transform.InverseTransformPoint(position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, Module * 0.5f, side * (Module * 0.5f - 0.2f)));
                Vector3 size = frame.transform.InverseTransformVector(new Vector3(0.4f, Module, 0.4f));
                post.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
        }

        private static void BuildHall(Transform parent)
        {
            // 가운데 반응로와 그 둘레의 에너지 기둥 여섯 개
            Vector3 core = new(0f, 0f, 1.5f);
            Solid(Path("B", "B_001", "SM_Reactor"), parent, core.x, core.z, 0f, 2f);
            for (int index = 0; index < 6; index++)
            {
                float angle = (30f + index * 60f) * Mathf.Deg2Rad;
                Solid(Path("B", "B_001", "SM_Energy_Cell"), parent, core.x + Mathf.Cos(angle) * 3.6f, core.z + Mathf.Sin(angle) * 3.6f, index * 60f, 1.5f);
            }

            // 서쪽: 미션 접수 데스크와 뒷벽 대형 화면
            Solid(Path("C", "C_002", "SM_Large_Control_Desk"), parent, -6.3f, 2.5f, 0f, 1f);
            Solid(Path("C", "C_002", "SM_Operator_Command_Chair"), parent, -7.6f, 4.6f, 90f, 1f);
            Decor(Path("C", "C_001", "SM_Large_Screen_Pannel"), parent, new Vector3(-9.75f, 2.2f, 2.5f), 0f, 1.6f, ArenaKit.Anchor.Center);
            Solid(Path("C", "C_003", "SM_Data_Server_Rack"), parent, -9.2f, 6.9f, 0f, 1.2f);

            // 동쪽: 증강칩 홀로 테이블과 서버
            Solid(Path("C", "C_001", "SM_Holo"), parent, 6.4f, 2.5f, 0f, 1.3f);
            Decor(Path("C", "C_001", "SM_Large_Screen_Pannel"), parent, new Vector3(9.75f, 2.2f, 2.5f), 180f, 1.6f, ArenaKit.Anchor.Center);
            Solid(Path("C", "C_001", "SM_Server"), parent, 9.2f, 5.6f, 180f, 1.2f);
            Solid(Path("C", "C_001", "SM_Server_V2"), parent, 9.2f, 6.9f, 180f, 1.2f);

            // 북쪽 벽: 냉동 캡슐 줄과 가운데 감시 콘솔
            foreach (float x in new[] { -6.5f, -4f, 4f, 6.5f })
                Solid(Path("E", "E_001", "SM_Cryogenic_Stasis_Chamber"), parent, x, 7.1f, 270f, 1.2f);
            Solid(Path("C", "C_002", "SM_MultiScreen_Surveillance_Console"), parent, 0f, 7f, 90f, 1.2f);

            // 남쪽: 출격 문 옆 이동 콘솔, 대기 벤치와 자판기
            Solid(Path("C", "C_003", "SM_Control_Console_Standalone"), parent, -4.3f, -7.1f, 90f, 1.1f);
            Solid(Path("F", "F_001", "SM_Bench"), parent, 6f, -5.2f, 0f, 1.2f);
            Solid(Path("F", "F_001", "SM_Bench"), parent, 6f, -3.2f, 0f, 1.2f);
            Solid(Path("F", "F_001", "SM_Vending_Machine"), parent, 9.1f, -7.1f, 180f, 1.1f);
            Solid(Path("G", "G_001", "SM_Tall_Rectangular_Planter"), parent, -9.1f, -7.1f, 0f, 1.3f);
            Solid(Path("G", "G_001", "SM_Tall_Rectangular_Planter"), parent, 3f, -7.1f, 0f, 1.3f);
        }

        private static void BuildServerRoom(Transform parent)
        {
            // 서쪽 벽과 가운데에 서버 두 줄, 통로 쪽에는 작업용 데스크
            for (int index = 0; index < 5; index++)
            {
                float z = -6.8f + index * 2.2f;
                Solid(Path("C", "C_003", "SM_Data_Server_Rack"), parent, -17.2f, z, 0f, 1.2f);
                Solid(Path("C", "C_001", index % 2 == 0 ? "SM_Server" : "SM_Server_V2"), parent, -14.2f, z, 0f, 1.2f);
            }
            Solid(Path("C", "C_003", "SM_Data_Workstation"), parent, -11.6f, -6.6f, 180f, 1.1f);
            Solid(Path("C", "C_001", "SM_Chair"), parent, -12.6f, -6.6f, 90f, 1f);
            Solid(Path("C", "C_001", "SM_Core_Processor"), parent, -11.4f, 3f, 180f, 1.3f);
        }

        private static void BuildGreenhouse(Transform parent)
        {
            // 북쪽 벽 수경 재배대, 가운데 긴 유리 화분 두 줄, 바닥 화분은 군데군데
            Solid(Path("G", "G_002", "SM_Botanical_HydroponicStation"), parent, 12.4f, 3.4f, 0f, 1.2f);
            Solid(Path("G", "G_002", "SM_Botanical_HydroponicStation"), parent, 15.6f, 3.4f, 0f, 1.2f);
            foreach (float x in new[] { 13.2f, 16f })
                foreach (float z in new[] { -5.6f, -2.2f })
                    Solid(Path("G", "G_002", "SM_Botanical_GlassPlanter_Long"), parent, x, z, 0f, 1.2f);
            Solid(Path("G", "G_002", "SM_Botanical_SpecimenChamber_Tall"), parent, 17.2f, 0.8f, 0f, 1.3f);
            Solid(Path("G", "G_001", "SM_Bio_Dome_Planter"), parent, 11.3f, -7f, 0f, 1.2f);
            Solid(Path("G", "G_001", "SM_Vertical_Glass_Planter"), parent, 17.2f, -7.1f, 0f, 1.2f);
            Solid(Path("G", "G_001", "SM_Rectangular_Floor_Planter"), parent, 11.4f, 1.2f, 0f, 1.4f);
        }

        // 지붕이 없어 기본 방향광이 밝혀 주므로, 반응로 둘레와 방마다 색을 더하는 점광원만 둔다.
        private static void BuildLights(Transform parent)
        {
            Color cyan = new(0.35f, 0.9f, 1f);
            Color white = new(0.95f, 0.97f, 1f);
            kit.PointLight(parent, Local(0f, 2.5f, 1.5f), cyan, 9f, 6f);
            kit.PointLight(parent, Local(-6.5f, 3f, 2.5f), white, 8f, 3f);
            kit.PointLight(parent, Local(6.5f, 3f, 2.5f), cyan, 8f, 3f);
            kit.PointLight(parent, Local(0f, 3f, -5f), white, 9f, 3f);
            kit.PointLight(parent, Local(-14f, 3f, -2f), cyan, 9f, 4f);
            kit.PointLight(parent, Local(14f, 3f, -2f), new Color(0.75f, 1f, 0.7f), 9f, 4f);
        }

        // 로비 배치에 맞춰 플레이어 시작 위치와 NPC 자리를 옮기고, 예전 임시 바닥은 끈다. 높이(y)는 그대로 둔다.
        private static void MoveActors(Scene scene)
        {
            MoveActor(scene, "Player", 0f, -4.5f, 0f);
            MoveActor(scene, "Mission NPC", -8.2f, 2.5f, 90f);
            MoveActor(scene, "Chip NPC", 8.2f, 2.5f, 270f);
            MoveActor(scene, "Stage Move NPC", -2.8f, -6.3f, 0f);

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name != "Floor") continue;
                Undo.RecordObject(rootObject, "Build Mission Lobby");
                rootObject.SetActive(false);
            }
        }

        private static void MoveActor(Scene scene, string name, float x, float z, float yaw)
        {
            Transform actor = Find(scene, name);
            if (actor == null) return;
            Undo.RecordObject(actor, "Build Mission Lobby");
            actor.SetPositionAndRotation(Local(x, actor.position.y, z), Quaternion.Euler(0f, yaw, 0f));
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == name) return rootObject.transform;
                foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
                    if (child.name == name && child.GetComponentInParent<RectTransform>() == null) return child;
            }
            return null;
        }

        // 바닥에 세우고 바운드 크기 콜라이더를 붙이는 소품
        private static void Solid(string prefab, Transform parent, float x, float z, float yaw, float scale)
        {
            ArenaKit.EnsureCollider(kit.Place(prefab, parent, Local(x, 0f, z), yaw, ArenaKit.Anchor.Bottom, -1, scale));
        }

        // 벽에 붙이는 장식, 콜라이더 없이 놓는다.
        private static void Decor(string prefab, Transform parent, Vector3 local, float yaw, float scale, ArenaKit.Anchor anchor)
        {
            kit.Place(prefab, parent, Local(local.x, local.y, local.z), yaw, anchor, -1, scale);
        }

        private static Vector3 Local(float x, float y, float z) => LobbyCenter + new Vector3(x, y, z);

        private static string Path(string series, string pack, string name) => $"{Ozea}{series}/OZEA_Studio_{pack}/Prefabs/{name}.prefab";
    }
}
