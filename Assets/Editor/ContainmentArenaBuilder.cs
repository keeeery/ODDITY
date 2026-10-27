using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd.EditorTools
{
    // 괴생명체 격리 시설 컨셉의 밀폐형 보스 아레나를 지금 열린 씬의 원점에 배치하는 메뉴 (스타일 A·B·C)
    // 세 스타일 모두 크기·천장 높이·캣워크 높이가 같고(ArenaKit 공통값), 바닥과 천장은 Ground, 벽은 Wall 레이어다.
    // 같은 씬에서 다시 실행하면 이전에 만든 아레나(RootName)를 지우고 새로 만든다.
    internal static class ContainmentArenaBuilder
    {
        private const string RootName = "Containment Arena";
        private const string MenuRoot = "Tools/Team Odd/Containment Arena/";
        private const float HalfSize = 24f;
        private const string KnifePrefabs = "Assets/Knife/Customizable Hologramm Shader/Prefabs/";
        // Knife 데모 머티리얼은 셰이더 기능 키워드가 꺼진 채로 들어 있어, 복사본을 만들어 키워드를 켜고 색을 바꿔 쓴다.
        private const string HologramSourceMaterial = "Assets/Knife/Customizable Hologramm Shader/Materials/Hologram KM Body.mat";
        private const string ConeSourceMaterial = "Assets/Knife/Customizable Hologramm Shader/Materials/Cone Red.mat";
        private const string BarrierMaterialPath = "Assets/Materials/Environment/Containment_HologramBarrier.mat";
        private const string EnergyConeMaterialPath = "Assets/Materials/Environment/Containment_EnergyCone.mat";
        private const string CorridorMaterial = "Assets/Sci-Fi Base/Models/Materials/Ground_Plates1_a.mat";

        private const string HangarWall = "Wall_Hangar1";
        private const string HangarPost = "Wall_Hangar1_Courner1";
        private const string HangarDoor = "Wall_Hangar1_Doors";
        // 출입문은 남쪽·북쪽 벽, 패널 이음매(0)에서 한 칸(6m) 사이 가운데
        private static readonly int[] DoorSides = { 0, 2 };
        private const float DoorAlong = 3f;
        private static readonly string[] RoofTiles = { "Concrete_Ground1", "Concrete_Ground2", "Concrete_Ground3", "Concrete_Ground4" };

        // ─── A. 관찰형 격리 실험동 ───────────────────────────────────────────────
        // 남색 패널 벽, 3단째에 관찰창과 그 뒤 관찰 통로, 차가운 흰색 조명, 홀로그램 연구 데이터
        [MenuItem(MenuRoot + "A. Observation Lab")]
        private static void BuildObservationLab()
        {
            Build("A. Observation Lab", (kit, root) =>
            {
                string[] plates = { "Ground_Plate1", "Ground_Plate1", "Ground_Plate2" };
                BuildFloorAndCeiling(kit, root, plates, RoofTiles);

                Transform walls = ArenaKit.Group(root, "Walls");
                const int WindowRow = 2;
                List<float> rows = kit.StackWalls(walls, HangarWall, 0f, kit.WallLayer, (row, side, along) =>
                    row == WindowRow ? "Wall_Hangar1_Window" : null);
                AddHangarPosts(kit, walls, rows, 0f);
                AddSealedDoors(kit, walls);
                BuildObservationCorridor(kit, ArenaKit.Group(root, "Observation Corridor"), rows[WindowRow]);

                kit.Catwalk(ArenaKit.Group(root, "Catwalk"), "Wall_Hangar1_Catwalk1", "Wall_Hangar1_Catwalk3", "Wall_Hangar1_Barrier1");

                Transform lights = ArenaKit.Group(root, "Lights");
                Color cold = new(0.82f, 0.93f, 1f);
                foreach (float height in MirroredHeights())
                for (int side = 0; side < 4; side++)
                foreach (float along in new[] { -12f, 0f, 12f })
                {
                    kit.PlaceOnWall("Lamp_07", lights, side, along, height, -1, scale: 2f);
                    kit.PointLight(lights, kit.SidePoint(side, along, height, -kit.HalfSize + 2.5f), cold, 20f, 4f);
                }
                AddFillLights(kit, lights);
                AddDoorWarningLights(kit, lights);

                // 연구 데이터 홀로그램: 마주 보는 두 모서리에 인체 스캔 (지구 홀로그램은 궤도 고리가 아레나를 가로질러 뺐다)
                // 원형 투사대 위에 세우고, 발밑과 그 바로 위 천장에 청록 빛을 깔아 모서리에 색을 더한다.
                Transform holograms = ArenaKit.Group(root, "Holograms");
                Color teal = new(0.3f, 0.9f, 1f);
                const float PedestalScale = 8f;
                foreach (int corner in new[] { 1, 3 })
                {
                    Vector3 position = kit.Center + Quaternion.Euler(0f, corner * 90f + 45f, 0f) * Vector3.forward * (kit.HalfSize - 5f) * 1.41f;
                    kit.Place("Lamp_Position", holograms, position, 0f, ArenaKit.Anchor.Bottom, -1, PedestalScale);
                    float top = position.y + kit.Measure("Lamp_Position", 0f).y * PedestalScale;
                    kit.Place(KnifePrefabs + "KnifeMan_t_pose Variant.prefab", holograms, new Vector3(position.x, top, position.z), corner * 90f + 225f, ArenaKit.Anchor.Bottom, -1, 2.2f);
                    kit.PointLight(lights, new Vector3(position.x, top + 0.6f, position.z), teal, 7f, 3f);
                    kit.PointLight(lights, new Vector3(position.x, ArenaKit.CeilingHeight - top - 0.6f, position.z), teal, 7f, 3f);
                }

                BuildLabFixtures(kit, ArenaKit.Group(root, "Lab Fixtures"));
                BuildLabProps(kit, ArenaKit.Group(root, "Psychokinesis Props"));

                ArenaKit.SetMood(new Color(0.27f, 0.31f, 0.37f), 0.5f, fog: false);
            });
        }

        // 높이 가운데에 띄운 약한 따뜻한 흰빛 보조광. 바닥이 묻히지 않을 만큼만 밝히고, 가운데는 벽 쪽보다 어둡게 남겨
        // 밀폐 시설의 명암을 살린다.
        // 높이 가운데라 바닥과 천장을 똑같이 비추고(중력 반전 대비), 바닥·천장 가까이에 광원이 없어
        // 그 위를 걷는 캐릭터가 과하게 밝아지지 않는다. 반지름 13m 고리에 8개
        // 고리가 닿지 않는 소품 모서리(북동·남서)는 벽 쪽 작업등으로 위아래 대칭으로 따로 밝힌다.
        private static void AddFillLights(ArenaKit kit, Transform parent)
        {
            Color warm = new(1f, 0.97f, 0.9f);
            foreach (float corner in new[] { 45f, 225f })
            foreach (float height in new[] { 5f, ArenaKit.CeilingHeight - 5f })
                kit.PointLight(parent, kit.Center + Quaternion.Euler(0f, corner, 0f) * Vector3.forward * 27f + Vector3.up * height, warm, 12f, 8f);
            for (int index = 0; index < 8; index++)
            {
                Quaternion turn = Quaternion.Euler(0f, index * 45f, 0f);
                kit.PointLight(parent, kit.Center + turn * Vector3.forward * 13f + Vector3.up * kit.MidHeight, warm, 20f, 12f);
            }
        }

        // 봉인된 출입문 위·아래(천장 쪽 대칭)에 호박색 경고등. 넓게 퍼지지 않도록 범위를 좁게 잡는다.
        private static void AddDoorWarningLights(ArenaKit kit, Transform parent)
        {
            Color amber = new(1f, 0.55f, 0.15f);
            const float Height = 4.6f;
            foreach (int side in DoorSides)
            foreach (float height in new[] { Height, ArenaKit.CeilingHeight - Height })
                kit.PointLight(parent, kit.SidePoint(side, DoorAlong, height, -kit.HalfSize + 1.2f), amber, 9f, 2.5f);
        }

        // 벽에 붙은 실험 설비(고정 장식): 키 큰 장비함, 제어 콘솔, 환기관, 바닥 점검 해치
        // 출입문(변 가운데에서 0~6m) 자리는 피하고, 벽 조명 띠(±12, 0)와 겹치지 않는 위치에 둔다.
        private static void BuildLabFixtures(ArenaKit kit, Transform parent)
        {
            for (int side = 0; side < 4; side++)
            {
                foreach (float along in new[] { -15f, 15f })
                    kit.PlaceOnWall("Wall3_Prop_05", parent, side, along, 0f, -1);
                foreach (float along in new[] { -9f, 9f })
                    kit.PlaceOnWall("Powerbox_15", parent, side, along, 0.8f, -1);
                foreach (float along in new[] { -21f, 21f })
                    kit.PlaceOnWall("Prop_vents_08", parent, side, along, 0f, -1);
            }

            foreach (Vector2 spot in new[] { new Vector2(-8f, -15f), new Vector2(8f, 15f), new Vector2(-15f, 8f), new Vector2(15f, -8f) })
                kit.Place("Cover Panel2A", parent, kit.Center + new Vector3(spot.x, 0f, spot.y), kit.Random.Next(4) * 90f, ArenaKit.Anchor.Bottom, -1);

            // 캣워크 바로 아래·위로 벽을 한 바퀴 두르는 전선 다발 (위쪽은 뒤집어 처짐까지 대칭)
            const string Cable = "Cables1";
            float cableHeight = kit.Measure(Cable, 0f).y;
            float below = kit.MidHeight - 1.5f - cableHeight;
            float length = kit.Measure(Cable, 0f).x;
            for (int side = 0; side < 4; side++)
            for (float along = -kit.HalfSize + length * 0.5f; along < kit.HalfSize; along += length)
            {
                kit.PlaceOnWall(Cable, parent, side, along, below, -1);
                kit.PlaceOnWall(Cable, parent, side, along, ArenaKit.CeilingHeight - below - cableHeight, -1, flip: true);
            }
        }

        // 염동력으로 잡아 던질 수 있는 실험 장비·상자·가스통. 보스가 움직이는 가운데(반지름 약 12m)는 비우고
        // 모서리와 벽을 따라 무리 지어 두며, 인체 스캔 홀로그램이 선 두 모서리(남동·북서)는 피한다.
        // 캣워크 위에도 몇 개 올려 중력 반전 때 떨어지는 물체로 쓸 수 있게 한다.
        private static void BuildLabProps(ArenaKit kit, Transform parent)
        {
            (string prefab, float x, float z, float yaw)[] floorProps =
            {
                // 북동 모서리
                ("Prop2", 19f, 17f, 0f), ("Prop6", 15.5f, 20.5f, 90f), ("Prop_Cylinder1", 20.8f, 20.8f, 0f), ("Prop_Cylinder2", 19.4f, 21.2f, 40f),
                // 남서 모서리
                ("Prop3", -19f, -17f, 90f), ("Prop8", -15.5f, -20.8f, 0f), ("Prop_Barrel1 yellow", -21f, -21f, 0f), ("Prop_Barrel1", -19.6f, -21.3f, 70f),
                // 남동·북서 모서리 (홀로그램 옆)
                ("Prop5", 12f, -20.8f, 0f), ("Tank_01", 20.8f, -11f, 0f), ("Prop_Barrel1 gray", 14.6f, -21.2f, 20f),
                ("Prop4", -12f, 20.8f, 180f), ("Prop7", -20.8f, 11f, 90f), ("Prop_Cylinder2", -14.6f, 21.2f, 0f),
                // 벽 가운데
                ("Prop1", 20.8f, 3f, 270f), ("Prop8", -20.8f, -3f, 90f), ("Prop9", 9f, 20.8f, 180f), ("Prop6", -9f, -20.8f, 0f),
            };
            foreach ((string prefab, float x, float z, float yaw) in floorProps)
                PlaceProp(kit, parent, prefab, new Vector3(x, 0f, z), yaw);

            float catwalkTop = kit.MidHeight + kit.Measure("Wall_Hangar1_Catwalk1", 0f).y * 0.5f;
            float onCatwalk = kit.HalfSize - 1.5f;
            PlaceProp(kit, parent, "Prop_Cylinder1", new Vector3(-6f, catwalkTop, onCatwalk), 0f);
            PlaceProp(kit, parent, "Prop9", new Vector3(onCatwalk, catwalkTop, -8f), 90f);
            PlaceProp(kit, parent, "Prop_Barrel1", new Vector3(10f, catwalkTop, -onCatwalk), 0f);
        }

        private static void PlaceProp(ArenaKit kit, Transform parent, string sourceName, Vector3 offset, float yaw)
        {
            string prefab = PsychokinesisPropFactory.GetOrCreate(sourceName);
            if (prefab != null) kit.Place(prefab, parent, kit.Center + offset, yaw, ArenaKit.Anchor.Bottom, -1);
        }

        // ─── B. 중격리 벙커 ──────────────────────────────────────────────────────
        // 콘크리트 벽 2단, 바닥·천장 쪽 경고 줄무늬 띠, 대형 격벽 기둥, 북쪽 원형 봉인 해치, 붉은 경고등
        [MenuItem(MenuRoot + "B. Heavy Containment Bunker")]
        private static void BuildBunker()
        {
            Build("B. Heavy Containment Bunker", (kit, root) =>
            {
                string[] tiles = { "Concrete_Ground1", "Concrete_Ground2", "Concrete_Ground3", "Concrete_Ground4" };
                BuildFloorAndCeiling(kit, root, tiles, RoofTiles);

                Transform walls = ArenaKit.Group(root, "Walls");
                const string Module = "Concrete_wall2";
                List<float> rows = kit.StackWalls(walls, Module, 0.5f, kit.WallLayer);
                float thickness = kit.Measure(Module, 0f).z;
                foreach (float bottom in rows)
                    kit.CornerPosts(walls, "Concrete_wall2_courA", bottom, kit.HalfSize + thickness * 0.5f, kit.WallLayer);

                // 경고 줄무늬 방폭 띠를 바닥 쪽에 두르고, 천장 쪽에는 뒤집어 대칭으로 두름
                const string Skirt = "Concrete_wall1 Stripes Variant";
                float skirtHeight = kit.Measure(Skirt, 0f).y;
                for (int side = 0; side < 4; side++)
                for (float along = -kit.HalfSize + 2f; along < kit.HalfSize; along += 4f)
                {
                    ArenaKit.EnsureCollider(kit.PlaceOnWall(Skirt, walls, side, along, 0f, kit.WallLayer));
                    ArenaKit.EnsureCollider(kit.PlaceOnWall(Skirt, walls, side, along, ArenaKit.CeilingHeight - skirtHeight, kit.WallLayer, flip: true));
                }

                // 바닥부터 천장까지 닿는 격벽 기둥
                const string Pillar = "Wall_Large_04";
                float pillarScale = kit.WallTop / kit.Measure(Pillar, 0f).y;
                for (int side = 0; side < 4; side++)
                foreach (float along in new[] { -18f, -6f, 6f, 18f })
                    ArenaKit.EnsureCollider(kit.PlaceOnWall(Pillar, walls, side, along, 0f, kit.WallLayer, scale: pillarScale, protrude: 1.6f));

                // 북쪽 벽 가운데 높이에 원형 봉인 해치, 해치 자리의 캣워크는 비운다.
                const string Hatch = KnifePrefabs + "boiler Variant.prefab";
                const float HatchScale = 0.32f;
                float hatchSize = kit.Measure(Hatch, 0f).y * HatchScale;
                ArenaKit.EnsureCollider(kit.PlaceOnWall(Hatch, walls, 2, 0f, kit.MidHeight - hatchSize * 0.5f, kit.WallLayer, scale: HatchScale, protrude: 1.2f));

                kit.Catwalk(ArenaKit.Group(root, "Catwalk"), "Wall_Hangar1_Catwalk1", "Wall_Hangar1_Catwalk3", "Wall_Hangar1_Barrier1",
                    (side, along) => side == 2 && Mathf.Abs(along) < 4f);

                Transform lights = ArenaKit.Group(root, "Lights");
                Color alert = new(1f, 0.16f, 0.1f);
                Color work = new(0.95f, 0.88f, 0.78f);
                foreach (float height in MirroredHeights())
                for (int side = 0; side < 4; side++)
                {
                    foreach (float along in new[] { -12f, 12f })
                    {
                        kit.PlaceOnWall("Lamp_Wall", lights, side, along, height - 1f, -1);
                        kit.PointLight(lights, kit.SidePoint(side, along, height, -kit.HalfSize + 3f), alert, 18f, 4f);
                    }
                    if (side != 2) kit.PointLight(lights, kit.SidePoint(side, 0f, height, -kit.HalfSize + 4f), work, 24f, 3f);
                }

                // 가운데 바닥과 천장이 보이도록 높이 가운데에 어두운 작업등
                for (int index = 0; index < 4; index++)
                    kit.PointLight(lights, kit.Center + Quaternion.Euler(0f, index * 90f + 45f, 0f) * Vector3.forward * 9f + Vector3.up * kit.MidHeight, work, 22f, 1.5f);

                // 모서리에 이동식 방벽
                Transform props = ArenaKit.Group(root, "Props");
                for (int corner = 0; corner < 4; corner++)
                {
                    Quaternion rotation = Quaternion.Euler(0f, corner * 90f, 0f);
                    kit.Place("Barrier_Solid_01", props, kit.Center + rotation * new Vector3(-kit.HalfSize + 6f, 0f, -kit.HalfSize + 3.5f), corner * 90f + 90f, ArenaKit.Anchor.Bottom, -1);
                    kit.Place("Barrier_Solid_01", props, kit.Center + rotation * new Vector3(-kit.HalfSize + 3.5f, 0f, -kit.HalfSize + 6f), corner * 90f, ArenaKit.Anchor.Bottom, -1);
                }

                ArenaKit.SetMood(new Color(0.17f, 0.13f, 0.12f), 0.3f, fog: true, new Color(0.1f, 0.07f, 0.06f), 0.01f);
            });
        }

        // ─── C. 중력 제어 격리로 ─────────────────────────────────────────────────
        // 개체를 중력 제어 장치로 띄워 가두는 반응로. 링 발전기와 홀로그램 에너지 기둥을 바닥과 천장에 대칭으로 두고,
        // 출입문은 홀로그램 격벽으로 막는다. 청록 조명과 주황 표시등 대비
        [MenuItem(MenuRoot + "C. Gravity Reactor")]
        private static void BuildGravityReactor()
        {
            Build("C. Gravity Reactor", (kit, root) =>
            {
                string[] tiles = { "Platform_01", "Platform_02" };
                BuildFloorAndCeiling(kit, root, tiles, RoofTiles);

                Transform walls = ArenaKit.Group(root, "Walls");
                List<float> rows = kit.StackWalls(walls, HangarWall, 0f, kit.WallLayer);
                AddHangarPosts(kit, walls, rows, 0f);
                AddSealedDoors(kit, walls);

                // 바닥 쪽과 천장 쪽(뒤집어서)에 설비 벽 띠, 출입문 앞은 비움
                const string Band = "Wall_Grid_Interior";
                float bandHeight = kit.Measure(Band, 0f).y;
                for (int side = 0; side < 4; side++)
                for (float along = -kit.HalfSize + 2f; along < kit.HalfSize; along += 4f)
                {
                    if (System.Array.IndexOf(DoorSides, side) >= 0 && Mathf.Abs(along - DoorAlong) < 4f) continue;
                    ArenaKit.EnsureCollider(kit.PlaceOnWall(Band, walls, side, along, 0f, kit.WallLayer));
                    ArenaKit.EnsureCollider(kit.PlaceOnWall(Band, walls, side, along, ArenaKit.CeilingHeight - bandHeight, kit.WallLayer, flip: true));
                }

                // 출입문 앞 홀로그램 격벽
                Material barrierMaterial = GetOrCreateMaterial(BarrierMaterialPath, HologramSourceMaterial, SetupBarrierMaterial);
                Vector3 doorSize = kit.Measure(HangarDoor, 0f);
                foreach (int side in DoorSides)
                {
                    GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    barrier.name = "Hologram Barrier";
                    Object.DestroyImmediate(barrier.GetComponent<Collider>());
                    barrier.transform.SetParent(walls, false);
                    barrier.transform.position = kit.SidePoint(side, DoorAlong, doorSize.y * 0.5f, -kit.HalfSize + DoorDepth(kit) + 0.05f);
                    // Quad는 -z 쪽이 앞면이라 아레나 안쪽에서 보이도록 반 바퀴 돌림
                    barrier.transform.rotation = Quaternion.Euler(0f, side * 90f + 180f, 0f);
                    barrier.transform.localScale = new Vector3(doorSize.x, doorSize.y, 1f);
                    barrier.GetComponent<MeshRenderer>().sharedMaterial = barrierMaterial;
                }

                kit.Catwalk(ArenaKit.Group(root, "Catwalk"), "Wall_Hangar1_Catwalk1", "Wall_Hangar1_Catwalk3", "Wall_Hangar1_Barrier1");

                // 네 모서리에 링 발전기를 바닥과 천장(뒤집어서)에 대칭으로 두고, 그 앞에 주황 표시등
                Transform reactor = ArenaKit.Group(root, "Reactor");
                Transform lights = ArenaKit.Group(root, "Lights");
                Color amber = new(1f, 0.55f, 0.2f);
                for (int corner = 0; corner < 4; corner++)
                {
                    string generator = corner % 2 == 0 ? "Generator_01" : "Generator_02";
                    Vector3 position = kit.Center + Quaternion.Euler(0f, corner * 90f + 45f, 0f) * Vector3.forward * 17f * 1.41f;
                    float yaw = corner * 90f + 45f;
                    kit.Place(generator, reactor, position, Quaternion.Euler(0f, yaw, 0f), ArenaKit.Anchor.Bottom, -1, 0.6f);
                    kit.Place(generator, reactor, position + Vector3.up * ArenaKit.CeilingHeight, Quaternion.Euler(180f, yaw, 0f), ArenaKit.Anchor.Top, -1, 0.6f);
                    Vector3 inward = (kit.Center - position).normalized * 5f;
                    kit.PointLight(lights, position + inward + Vector3.up * 3f, amber, 10f, 3f);
                    kit.PointLight(lights, position + inward + Vector3.up * (ArenaKit.CeilingHeight - 3f), amber, 10f, 3f);
                }

                // 가운데를 둘러싼 홀로그램 에너지 기둥: 바닥에서 위로, 천장에서 아래로 뿜는 원뿔
                const string Cone = KnifePrefabs + "Conus_2 Variant.prefab";
                Material coneMaterial = GetOrCreateMaterial(EnergyConeMaterialPath, ConeSourceMaterial, material => material.SetColor("_Color", new Color(0.35f, 1.5f, 2.2f, 0.6f)));
                for (int index = 0; index < 4; index++)
                {
                    Vector3 position = kit.Center + Quaternion.Euler(0f, index * 90f + 45f, 0f) * Vector3.forward * 11f;
                    SetMaterial(kit.Place(Cone, reactor, position, Quaternion.Euler(-90f, 0f, 0f), ArenaKit.Anchor.Bottom, -1, 1.6f), coneMaterial);
                    SetMaterial(kit.Place(Cone, reactor, position + Vector3.up * ArenaKit.CeilingHeight, Quaternion.Euler(90f, 0f, 0f), ArenaKit.Anchor.Top, -1, 1.6f), coneMaterial);
                }

                Color teal = new(0.25f, 0.85f, 1f);
                foreach (float height in MirroredHeights())
                for (int side = 0; side < 4; side++)
                foreach (float along in new[] { -12f, 0f, 12f })
                    kit.PointLight(lights, kit.SidePoint(side, along, height, -kit.HalfSize + 3f), teal, 16f, 2.5f);

                // 가운데 바닥과 천장이 보이도록 높이 가운데에 넓은 조명
                for (int index = 0; index < 4; index++)
                    kit.PointLight(lights, kit.Center + Quaternion.Euler(0f, index * 90f, 0f) * Vector3.forward * 8f + Vector3.up * kit.MidHeight, new Color(0.6f, 0.9f, 1f), 26f, 5f);

                ArenaKit.SetMood(new Color(0.16f, 0.21f, 0.25f), 0.35f, fog: true, new Color(0.04f, 0.07f, 0.08f), 0.008f);
            });
        }

        // ─── 공통 ────────────────────────────────────────────────────────────────

        private static void Build(string style, System.Action<ArenaKit, Transform> buildStyle)
        {
            Scene scene = SceneManager.GetActiveScene();
            ArenaKit kit = new(Vector3.zero, HalfSize, 1026);
            PsychokinesisPropFactory.ResetSession();
            Transform root = ArenaKit.CreateRoot(scene, null, RootName);

            buildStyle(kit, root);

            EditorSceneManager.MarkSceneDirty(scene);
            List<string> counts = new();
            foreach (Transform group in root) counts.Add($"{group.name} {group.childCount}");
            Debug.Log($"[Arena] {style} 배치 완료: {string.Join(", ", counts)}");
        }

        // 바닥 타일과 그 아래 바닥 콜라이더, 뒤집은 천장 타일과 천장 콜라이더, 바깥 지붕
        private static void BuildFloorAndCeiling(ArenaKit kit, Transform root, string[] tiles, string[] roofTiles)
        {
            Transform floor = ArenaKit.Group(root, "Floor");
            kit.TileArea(floor, tiles, 0f, flip: false, kit.GroundLayer);
            kit.Slab(floor, "Floor Collider", 0f, top: true, kit.HalfSize + 4f);

            Transform ceiling = ArenaKit.Group(root, "Ceiling");
            kit.TileArea(ceiling, tiles, ArenaKit.CeilingHeight, flip: true, kit.GroundLayer);
            kit.Slab(ceiling, "Ceiling Collider", ArenaKit.CeilingHeight, top: false, kit.HalfSize + 4f);
            kit.Roof(ceiling, roofTiles, kit.WallTop, kit.HalfSize + 4f);
        }

        // 격납고 패널 이음매마다 세로 기둥을 덧대고 모서리 틈도 같은 기둥으로 막음
        private static void AddHangarPosts(ArenaKit kit, Transform parent, List<float> rows, float outward)
        {
            float length = kit.Measure(HangarWall, 0f).x;
            float thickness = kit.Measure(HangarWall, 0f).z;
            foreach (float bottom in rows)
            {
                kit.CornerPosts(parent, HangarPost, bottom, kit.HalfSize + outward + thickness * 0.5f, kit.WallLayer);
                for (int side = 0; side < 4; side++)
                for (float along = -kit.HalfSize + length; along < kit.HalfSize - 0.1f; along += length)
                    kit.PlaceOnWall(HangarPost, parent, side, along, bottom, kit.WallLayer);
            }
        }

        // 남쪽·북쪽 벽에 출입문을 덧붙임. 문틀 안쪽으로 막힌 벽이 보여 닫힌 문처럼 보이고 밀폐도 유지된다.
        private static void AddSealedDoors(ArenaKit kit, Transform parent)
        {
            foreach (int side in DoorSides)
                kit.PlaceOnWall(HangarDoor, parent, side, DoorAlong, 0f, kit.WallLayer);
        }

        private static float DoorDepth(ArenaKit kit) => kit.Measure(HangarDoor, kit.YawAlongWall(HangarDoor)).z;

        // 높이 가운데를 기준으로 위아래 대칭인 조명 높이
        private static float[] MirroredHeights() => new[] { ArenaKit.CeilingHeight * 0.28f, ArenaKit.CeilingHeight * 0.72f };

        // 관찰창 줄 뒤에 바깥 벽을 한 겹 더 세우고, 그 사이 바닥·천장을 막아 어두운 관찰 통로를 만든다.
        private static void BuildObservationCorridor(ArenaKit kit, Transform parent, float bottom)
        {
            const float Depth = 3f;
            Vector3 wall = kit.Measure(HangarWall, 0f);
            kit.WallRing(parent, HangarWall, bottom, kit.WallLayer, Depth);
            kit.CornerPosts(parent, HangarPost, bottom, kit.HalfSize + Depth + wall.z * 0.5f, kit.WallLayer);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(CorridorMaterial);
            float inner = kit.HalfSize + wall.z;
            float outer = kit.HalfSize + Depth;
            float length = (outer + wall.z) * 2f;
            foreach (float height in new[] { bottom - 0.1f, bottom + wall.y + 0.1f })
            for (int side = 0; side < 4; side++)
            {
                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Corridor Slab";
                strip.transform.SetParent(parent, false);
                strip.transform.position = kit.SidePoint(side, 0f, height, -(inner + outer) * 0.5f);
                strip.transform.rotation = Quaternion.Euler(0f, side * 90f, 0f);
                strip.transform.localScale = new Vector3(length, 0.2f, outer - inner);
                strip.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            Color dim = new(0.6f, 0.85f, 1f);
            for (int side = 0; side < 4; side++)
            foreach (float along in new[] { -12f, 12f })
                kit.PointLight(parent, kit.SidePoint(side, along, bottom + wall.y * 0.5f, -(inner + outer) * 0.5f), dim, 6f, 1.5f);
        }

        // 원본 머티리얼을 복사해 Assets/Materials/Environment에 두고, 처음 만들 때만 setup으로 값을 바꾼다.
        private static Material GetOrCreateMaterial(string path, string sourcePath, System.Action<Material> setup)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CopyAsset(sourcePath, path);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            setup(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // 홀로그램 격벽: 평평한 판이라 가장자리 기준인 프레넬과 사라지는 디졸브는 끄고, 스캔라인과 노이즈만 청록색으로 켠다.
        private static void SetupBarrierMaterial(Material material)
        {
            material.SetColor("_MainColor", new Color(0.3f, 1.4f, 2f, 0.35f));
            material.SetFloat("_FresnelFeature", 0f);
            material.SetFloat("_DissolveFeature", 0f);
            material.SetFloat("_DissolveHide", -1f);
            material.DisableKeyword("_FRESNELFEATURE_ON");
            material.DisableKeyword("_DISSOLVEFEATURE_ON");
        }

        private static void SetMaterial(GameObject instance, Material material)
        {
            if (instance == null) return;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }
    }
}
