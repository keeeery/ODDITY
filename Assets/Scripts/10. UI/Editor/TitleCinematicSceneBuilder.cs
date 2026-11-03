using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Study3D.FpsGame;
using TeamOdd.Boss;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeamOdd.UI
{
    // 시네마틱 메인 화면 씬(TitleCinematicDirector)을 새로 만든다.
    // - Hard Boss 씬의 맵·볼륨·조명 설정과 보스/플레이어 위치를 가져와 같은 아레나에서 1인칭으로 보스를 마주 보게 둔다
    // - 보스와 던질 물체는 프리팹에서 꺼내 게임 스크립트를 떼어 내고 애니메이터만 남긴다 (매니저 없이 연출만 돌도록)
    // - 씬 이동 연출(Scene Load)과 EventSystem은 기존 Title 씬에서 복사한다
    // - 메뉴 UI는 Arcanist 메인 화면처럼 왼쪽에 제목·글자 버튼, 오른쪽에 회전하는 홀로그램 보스와 스캔 표시, 먼지를 둔다
    // 다시 실행하면 같은 경로의 씬을 덮어쓴다. 빌드 설정에는 넣지 않는다.
    public static partial class TitleCinematicSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/10. Build/2. Final/00. Title Cinematic.unity";
        private const string SourceScenePath = "Assets/Scenes/10. Build/2. Final/05. Hard Boss.unity";
        private const string TitleScenePath = "Assets/Scenes/10. Build/2. Final/01. Title.unity";
        private const string BossPrefabPath = "Assets/Prefabs/6.5. Humanoid Boss/Humanoid Boss.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/1. Player/Player.prefab";
        // Hard Boss 씬의 염동력 소품 중 가장 많이 놓인 것
        private const string ProjectilePrefabPath = "Assets/Prefabs/8. Environmental Mechanics/PsychokinesisProps/Hangars/Cargocase.prefab";
        // 떠오르는 동작: 보스 컨트롤러에 없어 연출에서 쓰지 않는 상태(RiseState)의 클립을 점프 시작으로 덮어씀
        private const string JumpStartPath = "Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/6__Jump/M_Big_Sword@Jump_Start_ZeroHeight.FBX";
        private const string OverrideControllerPath = "Assets/Animations/Humanoid Boss/Title Cinematic Boss.overrideController";
        private const string RiseState = "PhaseShift";
        private const string StrikeState = "GuardAttack";
        private const string StrikeEvent = "OnDeflectRelease";
        // 로비 보스 홀로그램(BossHologramDisplay)이 쓰는 머티리얼, 메뉴 홀로그램도 모든 부위에 그대로 공유해 로비처럼 매끄럽게 보이게 함
        private const string HologramMaterialPath = "Assets/Knife/Customizable Hologramm Shader/Materials/Hologram KM Cloth.mat";
        // 로비 후처리, 메뉴가 뜰 때 아레나 볼륨 대신 씀 (아레나 노출 +1.44에선 홀로그램 표면이 하얗게 타 조각나 보임)
        private const string MenuProfilePath = "Assets/Scenes/9. Mission/Mission Lobby Profile.asset";
        private const string DustMaterialPath = "Assets/VFX/Skill/BlinkEffect.mat";
        private const string TitleFontPath = "Assets/Font/aquire-font/Aquire SDF.asset";
        private const string MenuFontPath = "Assets/Font/Pretendard/Pretendard-SemiBold SDF.asset";
        private const string SpriteFolder = "Assets/Materials/Title";
        // 제목 로고는 두 장을 겹침: 세 번째 D를 뺀 본체 + 같은 캔버스 크기의 D만 있는 그림(글리치 때 좌우로 뒤집힘)
        private const string LogoPath = "Assets/Materials/Title/Logo/Logo_ODDITY_SmoochExtraBold_Base.png";
        private const string LogoLetterPath = "Assets/Materials/Title/Logo/Logo_ODDITY_SmoochExtraBold_D.png";
        private const float LogoLetterPivotX = 0.4536f; // 로고 가로 중 세 번째 D 가운데 위치(0~1), 로고를 다시 뽑으면 같이 바꿈
        private const float LogoWidth = 700f;
        private const string FirstSceneName = "02. Lobby";
        private const string VersionLabel = "v 1.0.0"; // 메뉴 왼쪽 아래 버전 표기

        private const float EyeHeight = 1.7f;
        private const float BossLookHeight = 2.2f;
        // 중앙 홀(x -25~15, z 88~128) 오른쪽(+x) 실드 게이트 너머 구역(x 35~55, z 98~118)에서 홀 쪽을 등지고 마주 봄
        // 씬에서 보스·Camera Rig 위치만 옮기면 연출이 시작할 때 서로를 바라보게 맞춤
        private static readonly Vector3 BossSpot = new(47f, 0f, 108f);
        private static readonly Vector3 CameraSpot = new(40.5f, 0f, 108f);
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private static readonly Color Accent = new(0.55f, 0.85f, 1f, 1f);

        [MenuItem("Tools/Team Odd/Build Title Cinematic Scene")]
        private static void Build()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;
            if (File.Exists(ScenePath) && EditorUtility.DisplayDialog("Title Cinematic",
                    $"{ScenePath} 를 덮어씁니다.", "덮어쓰기", "취소") == false) return;

            // 1) 원본 씬에서 맵과 배치 정보를 읽음
            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            LightingSnapshot lighting = LightingSnapshot.Capture();
            GameObject map = FindRoot(source, "Map");
            Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Scene title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
            GameObject sceneLoad = FindRoot(title, "Scene Load");
            GameObject eventSystem = FindRoot(title, "EventSystem");

            // 2) 새 씬에 복사
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            Transform world = new GameObject("Cinematic World").transform;
            if (map != null) Copy(map, world);
            else Debug.LogWarning("[Title] Hard Boss 씬에서 Map을 찾지 못했습니다. 배경 없이 만듭니다.");

            Transform post = new GameObject("Post Processing").transform;
            foreach (Volume volume in volumes)
                if (map == null || volume.transform.IsChildOf(map.transform) == false) Copy(volume.gameObject, post);

            if (sceneLoad != null) Copy(sceneLoad, null);
            if (eventSystem != null) Copy(eventSystem, null);

            EditorSceneManager.CloseScene(title, true);
            EditorSceneManager.CloseScene(source, true);
            lighting.Apply();

            // 맵 안의 보스·카메라는 연출과 겹치므로 정리
            foreach (HumanoidBossCombatEntity copiedBoss in world.GetComponentsInChildren<HumanoidBossCombatEntity>(true))
                Object.DestroyImmediate(copiedBoss.gameObject);
            CleanUpWorld(world.gameObject);

            // 3) 연출 배우, 바닥 높이는 복사한 맵 콜라이더로 잼
            var director = new GameObject("Title Cinematic Director").AddComponent<TitleCinematicDirector>();
            Physics.SyncTransforms();
            Vector3 bossPosition = SnapToGround(BossSpot);
            Vector3 cameraGround = SnapToGround(CameraSpot);
            Vector3 toCamera = cameraGround - bossPosition;
            toCamera.y = 0f;

            Animator bossAnimator = CreateBoss(world, bossPosition, Quaternion.LookRotation(toCamera), director);
            Transform projectile = CreateProjectile(world);

            Vector3 eye = cameraGround + Vector3.up * EyeHeight;
            (Transform rig, Camera camera) = CreateCamera(eye, bossPosition + Vector3.up * BossLookHeight);
            projectile.position = rig.TransformPoint(new Vector3(0.7f, 0.35f, -1.2f)); // 연출이 카메라 뒤에서 발사함

            GameObject menuWorld = CreateMenuWorld();
            GameObject menuHologram = CreateBossHologram(bossPosition, Quaternion.LookRotation(toCamera));
            GameObject playerHologram = CreatePlayerHologram(bossPosition, Quaternion.LookRotation(toCamera));

            // 4) 메뉴 UI
            MenuUI ui = CreateMenuUI();
            CreateScanDisplay(ui.Pulse, camera);
            CreateTitlePopups(ui.Pulse.GetComponent<StartMenuUI>()); // 분석 HUD보다 위에 오도록 그 뒤에 만듦

            // 5) 연출 진행
            Wire(director,
                ("cameraRig", rig),
                ("viewCamera", camera),
                ("bossAnimator", bossAnimator),
                ("bossEvents", bossAnimator.GetComponent<AnimStateEventListener>()),
                ("takeoffDust", FindTakeoffDust(bossAnimator)),
                ("projectile", projectile),
                ("projectileTrail", projectile.GetComponent<TrailRenderer>()),
                ("blackout", ui.Blackout),
                ("cinematicWorld", world.gameObject),
                ("menuWorld", menuWorld),
                ("menuHologram", menuHologram),
                ("menuPlayerHologram", playerHologram),
                ("menuProfile", AssetDatabase.LoadAssetAtPath<VolumeProfile>(MenuProfilePath)),
                ("pulse", ui.Pulse),
                ("menuGroup", ui.MenuGroup),
                ("titleGroup", ui.TitleGroup),
                ("buttons", ui.Buttons),
                ("footer", ui.Footer));

            EditorSceneManager.SaveScene(scene, ScenePath);

            // 환경광(강도 8)·반사 프로브는 이 씬 이름으로 구운 조명 데이터가 있어야 원본처럼 보임
            // 원본 라이팅 설정은 라이트맵을 굽지 않아 환경광만 금방 구워지므로 다시 만들 때마다 자동으로 구움
            if (Lightmapping.Bake()) EditorSceneManager.SaveScene(scene);
            else Debug.LogWarning("[Title] 조명을 굽지 못했습니다. Lighting 창에서 Generate Lighting을 눌러 주세요.");
            Debug.Log($"[Title] 시네마틱 메인 화면 씬을 만들었습니다: {ScenePath}");
        }

        #region ACTORS

        // 바닥(위층 바닥에 걸리지 않도록 조금 위에서) 높이로 맞춤, 못 찾으면 그대로
        private static Vector3 SnapToGround(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }

        // 애니메이터와 신호 우체통(AnimStateEventListener)만 남긴 보스
        // 가드 반격 패턴의 되쳐 낼 때 터지는 입자 설정(releaseBurst)은 스크립트를 떼기 전에 연출로 옮김
        private static Animator CreateBoss(Transform parent, Vector3 position, Quaternion rotation, TitleCinematicDirector director)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(boss, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            boss.name = "Humanoid Boss (Cinematic)";
            boss.transform.SetParent(parent, false);
            boss.transform.SetPositionAndRotation(position, rotation);

            Animator animator = boss.GetComponentInChildren<Animator>(true);
            if (animator.GetComponent<AnimStateEventListener>() == null) animator.gameObject.AddComponent<AnimStateEventListener>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            MeasureStrike(animator.runtimeAnimatorController, director);
            animator.runtimeAnimatorController = CreateRiseOverride(animator.runtimeAnimatorController);

            HumanoidGuardCounter guardCounter = boss.GetComponentInChildren<HumanoidGuardCounter>(true);
            if (guardCounter != null)
            {
                var serializedDirector = new SerializedObject(director);
                serializedDirector.CopyFromSerializedProperty(new SerializedObject(guardCounter).FindProperty("releaseBurst"));
                serializedDirector.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[Title] 보스 프리팹에서 HumanoidGuardCounter를 찾지 못해 반격 입자 설정을 옮기지 못했습니다.");

            // 흙먼지(BossFootDust)와 검 궤적(HumanoidSwordTrail)은 매니저 없이 돌아가므로 남김, 먼지는 이륙 때 연출이 직접 터뜨림
            StripGameScripts(boss, typeof(AnimStateEventListener), typeof(BossFootDust), typeof(HumanoidSwordTrail));
            RemovePhysics(boss);
            foreach (ParticleSystem system in boss.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
            }

            return animator;
        }

        // 반격 베기(GuardAttack)가 시작부터 베는 순간(OnDeflectRelease)까지 걸리는 시간을 연출에 넣어 물체 도착을 맞춤
        private static void MeasureStrike(RuntimeAnimatorController controller, TitleCinematicDirector director)
        {
            AnimatorState state = controller is AnimatorController source
                ? source.layers.Select(layer => FindState(layer.stateMachine, StrikeState)).FirstOrDefault(found => found != null)
                : null;
            float? eventTime = state?.behaviours.OfType<AnimStateEventSender>()
                .Select(sender => FindEventTime(sender, StrikeEvent)).FirstOrDefault(time => time.HasValue);

            if (state == null || state.motion is not AnimationClip clip || eventTime.HasValue == false)
            {
                Debug.LogWarning($"[Title] {StrikeState}의 {StrikeEvent} 시점을 찾지 못해 기본 타이밍을 씁니다.");
                return;
            }

            var serialized = new SerializedObject(director);
            serialized.FindProperty("strikeNormalizedTime").floatValue = eventTime.Value;
            serialized.FindProperty("strikeLead").floatValue = clip.length * eventTime.Value / Mathf.Max(Mathf.Abs(state.speed), 0.01f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float? FindEventTime(AnimStateEventSender sender, string eventName)
        {
            SerializedProperty events = new SerializedObject(sender).FindProperty("events");
            for (int index = 0; index < events.arraySize; index++)
            {
                SerializedProperty item = events.GetArrayElementAtIndex(index);
                if (item.FindPropertyRelative("name").stringValue == eventName) return item.FindPropertyRelative("time").floatValue;
            }

            return null;
        }

        private static RuntimeAnimatorController CreateRiseOverride(RuntimeAnimatorController controller)
        {
            AnimationClip jump = AssetDatabase.LoadAllAssetsAtPath(JumpStartPath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name.StartsWith("__preview__") == false);
            AnimatorState state = controller is AnimatorController source
                ? source.layers.Select(layer => FindState(layer.stateMachine, RiseState)).FirstOrDefault(found => found != null)
                : null;

            if (jump == null || state == null || state.motion is not AnimationClip original)
            {
                Debug.LogWarning($"[Title] 떠오르는 동작을 만들지 못했습니다. ({RiseState} 상태 또는 점프 클립 없음) 원래 동작으로 떠오릅니다.");
                return controller;
            }

            AssetDatabase.DeleteAsset(OverrideControllerPath);
            var overrideController = new AnimatorOverrideController(controller) { name = Path.GetFileNameWithoutExtension(OverrideControllerPath) };
            overrideController[original] = jump;
            AssetDatabase.CreateAsset(overrideController, OverrideControllerPath);
            return overrideController;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == name) return child.state;

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimatorState found = FindState(child.stateMachine, name);
                if (found != null) return found;
            }

            return null;
        }

        // 에디터의 GetComponent는 없을 때 가짜 null을 돌려주므로 ?? 대신 직접 확인
        private static BossFootDust FindTakeoffDust(Animator animator)
        {
            BossFootDust dust = animator.GetComponentInParent<BossFootDust>(true);
            if (dust == null) dust = animator.GetComponentInChildren<BossFootDust>(true);
            if (dust == null) Debug.LogWarning("[Title] 보스에서 BossFootDust를 찾지 못해 이륙 흙먼지 없이 만듭니다.");
            return dust;
        }

        // 메뉴 배경: 원본 보스 프리팹에 로비 홀로그램 머티리얼을 입혀 연출 보스 자리에 둠, 검 없이 몸체만 T-포즈로 (회전은 맥박 스크립트가 함)
        // 연출 월드와 따로 두어 암전 뒤 연출 월드를 꺼도 남고, 메뉴가 뜰 때 연출이 켬
        private static GameObject CreateMenuHologram(string prefabPath, string name, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var hologram = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            PrefabUtility.UnpackPrefabInstance(hologram, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            hologram.name = name;
            hologram.transform.SetPositionAndRotation(position, rotation);

            // 스캔 중인 대상처럼 T-포즈로 고정: 아바타 설정에 저장된 T-포즈(skeleton)를 뼈마다 입히고, 애니메이터는 꺼서 자세를 유지
            Animator animator = hologram.GetComponentInChildren<Animator>(true);
            ApplyAvatarTPose(animator);
            animator.enabled = false;

            StripGameScripts(hologram);
            RemovePhysics(hologram);

            ApplyHologramMaterial(hologram);

            foreach (ParticleSystem system in hologram.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
            }

            hologram.SetActive(false);
            return hologram;
        }

        // 보스 홀로그램: 자동 생성 메쉬라 면마다 방향이 제각각이어서 홀로그램 가장자리 빛이 면마다 따로 켜져 조각나 보임
        // 홀로그램 전용 복사본 메쉬의 면 방향(노멀)을 부드럽게 다시 계산해 씀, 원본 모델은 그대로라 전투 보스·로비엔 영향 없음
        private static GameObject CreateBossHologram(Vector3 position, Quaternion rotation)
        {
            GameObject hologram = CreateMenuHologram(BossPrefabPath, "Menu Hologram", position, rotation);
            foreach (SkinnedMeshRenderer renderer in hologram.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null) renderer.sharedMesh = GetSmoothedMesh(renderer.sharedMesh);
            return hologram;
        }

        // 같은 위치(작은 오차 안)에 겹친 정점을 한 점으로 보고, 둘러싼 면들의 방향을 면적만큼 더해 평균 낸 노멀을 모두에게 줌
        // UV 경계·각진 모서리에서 갈라져 있던 정점까지 묶이므로 표면 전체가 하나의 곡면처럼 이어짐, 뼈 가중치·바인드포즈는 원본 그대로
        private static Mesh GetSmoothedMesh(Mesh source)
        {
            Directory.CreateDirectory(SpriteFolder);
            string path = $"{SpriteFolder}/{source.name} Hologram Smooth.asset";

            Mesh mesh = Object.Instantiate(source);
            mesh.name = $"{source.name} Hologram Smooth";

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float cell = Mathf.Max(mesh.bounds.size.magnitude * 1e-5f, 1e-6f);

            var groups = new Dictionary<Vector3Int, int>();
            var groupOf = new int[vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 vertex = vertices[index];
                var key = new Vector3Int(Mathf.RoundToInt(vertex.x / cell), Mathf.RoundToInt(vertex.y / cell), Mathf.RoundToInt(vertex.z / cell));
                if (groups.TryGetValue(key, out int group) == false)
                {
                    group = groups.Count;
                    groups.Add(key, group);
                }

                groupOf[index] = group;
            }

            // 자동 생성 메쉬는 삼각형 감김 방향이 섞여 있을 수 있어, 면 방향을 정점의 원래 노멀 쪽으로 뒤집어 더함 (서로 상쇄되지 않게)
            Vector3[] original = mesh.normals;
            bool hasOriginal = original.Length == vertices.Length;
            var sums = new Vector3[groups.Count];
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 face = Vector3.Cross(vertices[triangles[index + 1]] - vertices[triangles[index]],
                    vertices[triangles[index + 2]] - vertices[triangles[index]]); // 길이가 면적의 2배라 큰 면일수록 많이 반영

                for (int corner = 0; corner < 3; corner++)
                {
                    int vertex = triangles[index + corner];
                    sums[groupOf[vertex]] += hasOriginal && Vector3.Dot(face, original[vertex]) < 0f ? -face : face;
                }
            }

            Vector3[] normals = hasOriginal ? (Vector3[])original.Clone() : new Vector3[vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 sum = sums[groupOf[index]];
                if (sum.sqrMagnitude > 0f) normals[index] = sum.normalized;
            }

            mesh.normals = normals;
            mesh.RecalculateTangents(); // 노멀맵이 새 노멀을 기준으로 얹히도록

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // 글리치 때 보스 대신 잠깐 비치는 플레이어(키는 실행 중 보스에 맞춤), 보스 홀로그램과 같은 방식으로 만들고 남은 입력·이동 컴포넌트도 모두 뗌
        private static GameObject CreatePlayerHologram(Vector3 position, Quaternion rotation)
        {
            GameObject hologram = CreateMenuHologram(PlayerPrefabPath, "Menu Player Hologram", position, rotation);
            StripAllBehaviours(hologram);
            return hologram;
        }

        // 다른 컴포넌트가 요구하지 않는 것부터 차례로 지워 모든 MonoBehaviour를 뗌 (PlayerInput 등 게임 어셈블리 밖의 것까지), 소리·카메라도 뗌
        private static void StripAllBehaviours(GameObject root)
        {
            bool removed = true;
            while (removed)
            {
                removed = false;
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || IsRequiredByOther(behaviour)) continue;

                    Object.DestroyImmediate(behaviour);
                    removed = true;
                }
            }

            // 카메라를 요구하던 URP 카메라 데이터 등이 먼저 빠진 뒤에 지움
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
        }

        // 아바타를 만들 때 맞춘 T-포즈는 HumanDescription.skeleton에 뼈 이름별로 저장돼 있음, 모델 루트(위치·크기)는 건드리지 않음
        private static void ApplyAvatarTPose(Animator animator)
        {
            if (animator.avatar == null || animator.avatar.isHuman == false)
            {
                Debug.LogWarning("[Title] 메뉴 홀로그램 보스가 휴머노이드 아바타가 아니라 T-포즈를 입히지 못했습니다.");
                return;
            }

            var bones = new Dictionary<string, Transform>();
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(bone.name, bone);

            foreach (SkeletonBone skeletonBone in animator.avatar.humanDescription.skeleton)
            {
                if (bones.TryGetValue(skeletonBone.name, out Transform bone) == false || bone == animator.transform) continue;

                bone.localPosition = skeletonBone.position;
                bone.localRotation = skeletonBone.rotation;
            }
        }

        // 로비처럼 모든 부위에 로비 홀로그램 머티리얼 하나를 그대로 공유 (복제하거나 노멀맵을 바꾸지 않음, 연출 값은 PropertyBlock으로만 바꿈)
        // 몸체(스킨 메쉬)만 남김: 검은 따로 붙은 일반 메쉬라 끄고, 꺼 둔 렌더러는 회전축·화면 맞춤 계산에서도 빠지므로 몸 가운데를 축으로 돎
        private static void ApplyHologramMaterial(GameObject hologram)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
            Material depth = GetDepthMaterial();
            foreach (Renderer renderer in hologram.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not SkinnedMeshRenderer skinned)
                {
                    renderer.enabled = false;
                    continue;
                }

                // 깊이 머티리얼은 마지막 칸에 덧붙여 같은 메쉬를 한 번 더 그림, 덧붙인 칸은 마지막 서브메쉬만 그리므로 서브메쉬가 하나일 때만
                Material[] materials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                bool singleMesh = skinned.sharedMesh != null && skinned.sharedMesh.subMeshCount == 1;
                renderer.sharedMaterials = depth != null && singleMesh ? materials.Append(depth).ToArray() : materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // 반투명 너머로 안쪽 굴곡이 겹쳐 비치지 않게, 홀로그램 바로 앞 순서에서 깊이만 써 두는 머티리얼
        // 같은 홀로그램 셰이더라 글리치 띠가 정점을 옆으로 밀 때 깊이도 같이 밀림 (따로 만든 셰이더면 밀린 자리에 구멍이 남)
        // 깊이 쓰기를 켜고 투명도 0으로 그려 색은 남기지 않음, 투명도는 연출이 PropertyBlock으로 0을 유지
        private static Material GetDepthMaterial()
        {
            string path = $"{SpriteFolder}/Title Hologram Depth.mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            // 보스·플레이어가 같은 에셋을 가리키므로 지우지 않고 원본 값으로 덮어씀
            if (material == null)
            {
                material = new Material(source) { name = "Title Hologram Depth" };
                Directory.CreateDirectory(SpriteFolder);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Alpha", 0f);
            material.renderQueue = source.renderQueue - 1;
            EditorUtility.SetDirty(material);
            return material;
        }

        // 염동력으로 던질 물체, 잡기·충돌 없이 모양만 남기고 염동력 빛을 붙임
        private static Transform CreateProjectile(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            var projectile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(projectile, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            projectile.name = "Thrown Object";
            projectile.transform.SetParent(parent, true);

            StripGameScripts(projectile);
            RemovePhysics(projectile);

            var glow = new GameObject("Psychokinesis Glow").AddComponent<Light>();
            glow.transform.SetParent(projectile.transform, false);
            glow.type = LightType.Point;
            glow.color = Accent;
            glow.range = 3f;
            glow.intensity = 2.5f;
            glow.shadows = LightShadows.None;

            // 빠르게 날아가는 궤적, 연출이 발사 순간부터 켬
            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
            trail.time = 0.18f;
            trail.minVertexDistance = 0.1f;
            trail.widthMultiplier = 0.7f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            var trailColor = new Gradient();
            trailColor.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Accent, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailColor;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            return projectile.transform;
        }

        private static (Transform rig, Camera camera) CreateCamera(Vector3 eye, Vector3 lookAt)
        {
            var rig = new GameObject("Camera Rig").transform;
            rig.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(rig, false);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            return (rig, camera);
        }

        // 메뉴 화면에 떠다니는 먼지 (Arcanist DustParticles), 연출이 메뉴 카메라 앞으로 옮김
        private static GameObject CreateMenuWorld()
        {
            var root = new GameObject("Menu World");
            var dust = new GameObject("Dust");
            dust.transform.SetParent(root.transform, false);

            ParticleSystem system = dust.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.65f, 0.55f), new Color(1f, 1f, 1f, 0.35f)); // 옅은 금빛·흰빛
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 25f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 8f, 8f);

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;

            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);

            root.SetActive(false);
            return root;
        }

        #endregion

        #region MENU_UI

        private struct MenuUI
        {
            public CanvasGroup Blackout;
            public CanvasGroup MenuGroup;
            public CanvasGroup TitleGroup;
            public CanvasGroup[] Buttons;
            public CanvasGroup Footer;
            public TitleHologramPulse Pulse;
        }

        // 메뉴 배치: 왼쪽 정렬 제목(ODDITY 로고 + 가는 구분선 + 부제)과 글자만 있는 메뉴 목록, 오른쪽엔 회전하는 홀로그램 보스
        // (오른쪽 분석 HUD와 스캔 링은 카메라가 필요해 CreateScanDisplay에서 따로 만듦)
        // 스타일은 Arcanist처럼 클래식하게(흰 글자, 부드러운 어둠, 금빛 먼지), SF 느낌은 홀로그램 보스와 가끔의 글리치 정도로만
        private static MenuUI CreateMenuUI()
        {
            var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            var menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuFontPath);
            Sprite vignetteSprite = CreatePatternSprite("TitleVignette", 256, 256,
                (u, v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.05f, new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f)));
            Sprite shadeSprite = CreatePatternSprite("TitleLeftShade", 256, 4, (u, v) => 1f - Mathf.SmoothStep(0f, 1f, u / 0.6f));

            var canvasObject = new GameObject("Title UI", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            StartMenuUI startMenu = canvasObject.AddComponent<StartMenuUI>();
            Wire(startMenu, ("firstSceneName", FirstSceneName));

            Transform root = canvasObject.transform;
            Stretch(CreateImage("Vignette", root, vignetteSprite, new Color(0f, 0f, 0f, 0.85f), Vector2.zero, Vector2.zero).rectTransform);
            Stretch(CreateImage("Left Shade", root, shadeSprite, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.zero).rectTransform);

            RectTransform titleRect = CreateLeftRect("Title", root, new Vector2(160f, 220f), new Vector2(1000f, 320f));
            CanvasGroup titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
            Image logo = CreateLogo(titleRect, out Image flipLetter);
            UIGlitch titleGlitch = CreateGlitch(logo, flipLetter);
            Image divider = CreateImage("Divider", titleRect, null, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, new Vector2(380f, 2f));
            PlaceLeft(divider.rectTransform, new Vector2(8f, -130f));
            TMP_Text subtitle = CreateLeftText("Subtitle", titleRect, "ANOMALY SUPPRESSION PROTOCOL", titleFont, 22f,
                new Color(1f, 1f, 1f, 0.55f), new Vector2(8f, -162f), new Vector2(900f, 40f));
            subtitle.characterSpacing = 12f;

            RectTransform menuRect = CreateLeftRect("Menu Group", root, new Vector2(160f, -170f), new Vector2(600f, 220f));
            CanvasGroup menuGroup = menuRect.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup[] buttons = CreateMenuButtons(menuRect, menuFont, startMenu);

            // 버전 표기: 업데이트 때 이 문구(VersionLabel)나 씬의 Title UI/Version 글자를 고침
            TMP_Text footerText = CreateLeftText("Version", root, VersionLabel, titleFont, 18f,
                new Color(1f, 1f, 1f, 0.35f), new Vector2(160f, 60f), new Vector2(600f, 30f));
            footerText.characterSpacing = 6f;
            SetAnchor(footerText.rectTransform, Vector2.zero);
            CanvasGroup footer = footerText.gameObject.AddComponent<CanvasGroup>();

            Image blackoutImage = CreateImage("Blackout", root, null, Color.black, Vector2.zero, Vector2.zero);
            Stretch(blackoutImage.rectTransform);
            CanvasGroup blackout = blackoutImage.gameObject.AddComponent<CanvasGroup>();
            blackout.blocksRaycasts = false;

            TitleHologramPulse pulse = canvasObject.AddComponent<TitleHologramPulse>();
            Wire(pulse, ("title", titleRect), ("titleGlitch", titleGlitch), ("flipLetter", flipLetter.rectTransform));

            return new MenuUI
            {
                Blackout = blackout,
                MenuGroup = menuGroup,
                TitleGroup = titleGroup,
                Buttons = buttons,
                Footer = footer,
                Pulse = pulse,
            };
        }

        // 오른쪽 홀로그램을 표적처럼 최소한으로: 발밑에 도는 링, 몸을 훑는 스캔 링(3D), 그 둘레의 모서리 괄호와 이름 한 줄(UI)
        // UI는 암전(Blackout) 아래에 둠
        private static TitleScanHud CreateScanDisplay(TitleHologramPulse pulse, Camera camera)
        {
            Transform canvas = pulse.transform;

            // 둘 다 얇은 선 하나, 발밑 링은 네 군데가 끊겨 있어 도는 게 보임
            Transform baseRing = CreateRingQuad("Base Ring", "TitleBaseRing", (radius, angle) =>
                Mathf.Abs(Mathf.Repeat(angle * 4f, 1f) - 0.5f) < 0.46f ? Band(radius, 0.95f, 0.008f) : 0f);
            Transform scanRing = CreateRingQuad("Scan Ring", "TitleScanRing", (radius, _) => Band(radius, 0.95f, 0.008f));
            Wire(pulse, ("baseRing", baseRing), ("scanRing", scanRing));
            var pulseObject = new SerializedObject(pulse);
            pulseObject.FindProperty("ringColor").colorValue = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            pulseObject.ApplyModifiedPropertiesWithoutUndo();

            var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            RectTransform hud = CreateRect("Scan HUD", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(hud);
            Transform blackout = canvas.Find("Blackout");
            if (blackout != null) hud.SetSiblingIndex(blackout.GetSiblingIndex());
            CanvasGroup group = hud.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // 괄호 크기·위치는 실행 중에 보스 화면 영역으로 맞춤
            RectTransform frame = CreateRect("Frame", hud, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 900f));
            const float Arm = 22f;
            const float Line = 2f;
            for (int corner = 0; corner < 4; corner++)
            {
                var anchor = new Vector2(corner % 2, corner / 2);
                RectTransform bracket = CreateRect($"Bracket {corner}", frame, anchor, Vector2.zero, new Vector2(Arm, Arm));
                bracket.pivot = anchor;
                bracket.anchoredPosition = Vector2.zero;
                foreach (Vector2 size in new[] { new Vector2(Arm, Line), new Vector2(Line, Arm) })
                {
                    Image bar = CreateImage("Bar", bracket, null, Color.white, Vector2.zero, size);
                    SetAnchor(bar.rectTransform, anchor);
                    bar.rectTransform.pivot = anchor;
                    bar.rectTransform.anchoredPosition = Vector2.zero;
                }
            }

            TMP_Text label = CreateFrameText("Label", frame, "SUBJECT 07  //  HUMANOID ANOMALY", titleFont, 13f, new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 10f), TextAlignmentOptions.Left);

            TitleScanHud scanHud = hud.gameObject.AddComponent<TitleScanHud>();
            Wire(scanHud,
                ("pulse", pulse),
                ("viewCamera", camera),
                ("group", group),
                ("frame", frame),
                ("label", label));
            return scanHud;
        }

        private static TMP_Text CreateFrameText(string name, Transform parent, string content, TMP_FontAsset font, float size,
            Vector2 anchor, Vector2 pivot, Vector2 position, TextAlignmentOptions alignment)
        {
            TMP_Text text = CreateText(name, parent, content, font, size, Color.white, Vector2.zero, new Vector2(520f, 24f));
            SetAnchor(text.rectTransform, anchor);
            text.rectTransform.pivot = pivot;
            text.rectTransform.anchoredPosition = position;
            text.alignment = alignment;
            text.characterSpacing = 6f;
            return text;
        }

        // 반지름 center에 폭 width로 그린 가우시안 선
        private static float Band(float radius, float center, float width) => Mathf.Exp(-Mathf.Pow((radius - center) / width, 2f));

        // 바닥에 눕힐 가산 합성 사각형(지름 1), 크기·위치·색은 TitleHologramPulse가 정하고 Bind 전엔 꺼 둠
        private static Transform CreateRingQuad(string name, string textureName, Func<float, float, float> alphaByRadiusAngle)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetRingMaterial(textureName, alphaByRadiusAngle);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            quad.SetActive(false);
            return quad.transform;
        }

        // URP Unlit 가산·양면, 텍스처는 (반지름 0~1, 각도 0~1)로 알파를 정한 흰색, 이미 있으면 그대로 씀
        private static Material GetRingMaterial(string textureName, Func<float, float, float> alphaByRadiusAngle)
        {
            string texturePath = $"{SpriteFolder}/{textureName}.png";
            string materialPath = $"{SpriteFolder}/{textureName}.mat";

            if (File.Exists(texturePath) == false)
            {
                Directory.CreateDirectory(SpriteFolder);

                const int Size = 1024;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var pixels = new Color32[Size * Size];
                float half = Size * 0.5f;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Vector2 offset = new Vector2(x + 0.5f - half, y + 0.5f - half) / half;
                        float radius = offset.magnitude;
                        float angle = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) / (Mathf.PI * 2f), 1f);
                        float value = radius >= 1f ? 0f : Mathf.Clamp01(alphaByRadiusAngle(radius, angle));
                        pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(value * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = textureName };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // 홀로그램 깊이 패스(Geometry+1)보다 먼저 그려 몸 안을 지나는 부분도 가려지지 않게 함 (가산이라 순서와 무관하게 섞임)
            material.renderQueue = (int)RenderQueue.Geometry;
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        // Arcanist 버튼처럼 글자만 있는 버튼(왼쪽 정렬): 올리면 왼쪽을 기준으로 커지고, 누르면 회색, 아주 약하게 지직거림
        private static CanvasGroup CreateButton(string label, Transform parent, TMP_FontAsset font, float y,
            TitleMenuButton.ButtonType type, StartMenuUI menu)
        {
            TMP_Text text = CreateLeftText(label, parent, label, font, 40f, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, y), new Vector2(420f, 64f));
            text.raycastTarget = true;
            text.characterSpacing = 6f;

            CanvasGroup group = text.gameObject.AddComponent<CanvasGroup>();
            TitleMenuButton button = text.gameObject.AddComponent<TitleMenuButton>();
            Wire(button, ("buttonType", type), ("menu", menu), ("glitch", CreateGlitch(text)));

            var serialized = new SerializedObject(button);
            serialized.FindProperty("hoverScaleMultiplier").floatValue = 1.1f;
            serialized.FindProperty("labelShift").floatValue = 0f;
            serialized.FindProperty("glitchStrength").floatValue = 0.25f;
            serialized.FindProperty("pressedColor").colorValue = new Color(0.7f, 0.7f, 0.7f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return group;
        }

        // 부모의 왼쪽 가운데를 기준으로 두는 영역/글자 (왼쪽 정렬 배치용)
        private static RectTransform CreateLeftRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0f, 0.5f), position, size);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text CreateLeftText(string name, Transform parent, string content, TMP_FontAsset font, float size,
            Color color, Vector2 position, Vector2 sizeDelta)
        {
            TMP_Text text = CreateText(name, parent, content, font, size, color, position, sizeDelta);
            PlaceLeft(text.rectTransform, position);
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static void PlaceLeft(RectTransform rect, Vector2 position)
        {
            SetAnchor(rect, new Vector2(0f, 0.5f));
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        // 글자(또는 로고) 자신이 튀고 깜빡이며 붉은색·청록색으로 갈라짐, 자식 그림(extraSplits)도 함께 갈라짐
        private static UIGlitch CreateGlitch(Graphic graphic, params Graphic[] extraSplits)
        {
            UIGlitch glitch = graphic.gameObject.AddComponent<UIGlitch>();
            if (graphic.TryGetComponent(out CanvasGroup group) == false) group = graphic.gameObject.AddComponent<CanvasGroup>();
            Object[] splits = new Object[] { graphic }.Concat(extraSplits).ToArray();
            Wire(glitch, ("targets", new Object[] { graphic.rectTransform }), ("group", group), ("splitGraphics", splits));
            return glitch;
        }

        // 제목 로고: 본체를 가로 LogoWidth에 맞춰 왼쪽 정렬로 두고, 그 위에 같은 크기로 세 번째 D를 겹친다
        // D는 본체의 자식이라 글리치로 튀고 깜빡일 때 같이 움직이고, 피벗을 D 가운데에 둬 제자리에서 뒤집힘
        private static Image CreateLogo(Transform parent, out Image letter)
        {
            Sprite sprite = ImportLogoSprite(LogoPath);
            float height = LogoWidth * sprite.rect.height / sprite.rect.width;
            Image logo = CreateImage("Title Logo", parent, sprite, Color.white, Vector2.zero, new Vector2(LogoWidth, height));
            logo.preserveAspect = true;
            PlaceLeft(logo.rectTransform, new Vector2(0f, 0f));

            letter = CreateImage("Flip Letter", logo.transform, ImportLogoSprite(LogoLetterPath), Color.white, Vector2.zero, Vector2.zero);
            letter.preserveAspect = true;
            Stretch(letter.rectTransform);
            letter.rectTransform.pivot = new Vector2(LogoLetterPivotX, 0.5f);
            letter.rectTransform.anchoredPosition = Vector2.zero;
            return logo;
        }

        private static Sprite ImportLogoSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static TMP_Text CreateText(string name, Transform parent, string content, TMP_FontAsset font, float size,
            Color color, Vector2 position, Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, sizeDelta);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchor(rect, anchor);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void SetAnchor(RectTransform rect, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
        }

        // (u, v) 0~1 좌표로 알파를 정하는 흰색 스프라이트, 이미 있으면 그대로 씀
        private static Sprite CreatePatternSprite(string name, int width, int height, Func<float, float, float> alphaByUV)
        {
            string path = $"{SpriteFolder}/{name}.png";

            if (File.Exists(path) == false)
            {
                Directory.CreateDirectory(SpriteFolder);

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float alpha = Mathf.Clamp01(alphaByUV((x + 0.5f) / width, (y + 0.5f) / height));
                        pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        #endregion

        #region UTILITIES

        private static GameObject FindRoot(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        private static GameObject Copy(GameObject original, Transform parent)
        {
            GameObject copy = parent != null ? Object.Instantiate(original, parent, true) : Object.Instantiate(original);
            copy.name = original.name;
            return copy;
        }

        // 복사한 맵은 배경으로만 쓰므로 게임 스크립트와 카메라·리스너를 뗌
        private static void CleanUpWorld(GameObject world)
        {
            StripGameScripts(world);

            foreach (Camera camera in world.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioListener listener in world.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
        }

        // 이 프로젝트(Assembly-CSharp) 스크립트를 뗌. 다른 컴포넌트가 RequireComponent로 붙든 것은 그쪽부터 지워질 때까지 미룸
        private static void StripGameScripts(GameObject root, params Type[] keep)
        {
            var gameAssembly = typeof(TitleCinematicDirector).Assembly;
            bool removed = true;

            while (removed)
            {
                removed = false;

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;

                    Type type = behaviour.GetType();
                    if (type.Assembly != gameAssembly || keep.Contains(type) || IsRequiredByOther(behaviour)) continue;

                    Object.DestroyImmediate(behaviour);
                    removed = true;
                }
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }

        private static bool IsRequiredByOther(Component component)
        {
            foreach (Component other in component.GetComponents<Component>())
            {
                if (other == null || other == component) continue;

                foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (IsType(require.m_Type0, component) || IsType(require.m_Type1, component) || IsType(require.m_Type2, component))
                        return true;
                }
            }

            return false;
        }

        private static bool IsType(Type type, Component component) => type != null && type.IsInstanceOfType(component);

        private static void RemovePhysics(GameObject root)
        {
            foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(agent);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        }

        private static void Wire(Object target, params (string name, object value)[] values)
        {
            var serialized = new SerializedObject(target);

            foreach ((string name, object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property == null)
                {
                    Debug.LogError($"[Title] {target.GetType().Name}에 {name} 필드가 없습니다.");
                    continue;
                }

                switch (value)
                {
                    case Object reference:
                        property.objectReferenceValue = reference;
                        break;

                    case Object[] references:
                        property.arraySize = references.Length;
                        for (int index = 0; index < references.Length; index++)
                            property.GetArrayElementAtIndex(index).objectReferenceValue = references[index];
                        break;

                    case Enum enumValue:
                        property.intValue = Convert.ToInt32(enumValue);
                        break;

                    case string text:
                        property.stringValue = text;
                        break;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 원본 씬의 안개·환경광·스카이박스를 새 씬에 그대로 옮기기 위한 값
        private readonly struct LightingSnapshot
        {
            private readonly bool fog;
            private readonly FogMode fogMode;
            private readonly Color fogColor;
            private readonly float fogDensity;
            private readonly float fogStart;
            private readonly float fogEnd;
            private readonly AmbientMode ambientMode;
            private readonly Color ambientSky;
            private readonly Color ambientEquator;
            private readonly Color ambientGround;
            private readonly float ambientIntensity;
            private readonly Material skybox;
            private readonly float reflectionIntensity;
            // 원본은 환경광 강도(8)를 구운 조명 데이터로 써서 값만 옮기면 화면이 하얗게 탐
            // 다른 씬의 조명 데이터는 저장할 때 떨어져 나가므로 라이팅 설정만 같이 쓰고, 데이터는 저장 뒤 이 씬으로 다시 구움
            private readonly LightingSettings settings;

            private LightingSnapshot(bool _)
            {
                settings = Lightmapping.TryGetLightingSettings(out LightingSettings sourceSettings) ? sourceSettings : null;
                fog = RenderSettings.fog;
                fogMode = RenderSettings.fogMode;
                fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity;
                fogStart = RenderSettings.fogStartDistance;
                fogEnd = RenderSettings.fogEndDistance;
                ambientMode = RenderSettings.ambientMode;
                ambientSky = RenderSettings.ambientSkyColor;
                ambientEquator = RenderSettings.ambientEquatorColor;
                ambientGround = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                skybox = RenderSettings.skybox;
                reflectionIntensity = RenderSettings.reflectionIntensity;
            }

            public static LightingSnapshot Capture() => new(true);

            public void Apply()
            {
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.skybox = skybox;
                RenderSettings.reflectionIntensity = reflectionIntensity;

                if (settings != null) Lightmapping.lightingSettings = settings;
            }
        }

        #endregion
    }
}
