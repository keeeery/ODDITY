using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamOdd.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd.UI
{
    // 임무 목록·임무 이동 창의 보스 그림을 보스 씬에서 직접 찍어 만든다.
    // missions.tsv에서 다음 씬(NextStageName)이 있는 임무마다 그 씬을 열고, 보스(BossMonster)를 대기 자세로 둔 뒤
    // 보스 정면에서 조금 옆·위로 비켜 내려다보는 카메라로 찍어 Resources/{BossSpriteKey}.png(Sprite)로 저장한다.
    // 씬은 저장하지 않고, 끝나면 실행 전에 열려 있던 씬으로 돌아간다. 다시 실행하면 그림을 새로 찍는다.
    public static class BossMissionShotCapture
    {
        private const string TablePath = "Assets/StreamingAssets/missions.tsv";
        private const string SceneFolder = "Assets/Scenes/10. Build/2. Final";
        private const string ResourcesFolder = "Assets/Resources";

        // 임무 창 그림 칸 비율(약 2.94:1)에 맞춘 크기
        private const int Width = 2400;
        private const int Height = 816;
        private const float FieldOfView = 40f;
        private const float MaxFieldOfView = 75f;
        // 뼈로 잰 크기에 더하는 여유(몸 두께)
        private const float BonePadding = 0.2f;
        // 보스 키에 대한 화면 세로 여유, 대기 자세를 뽑을 시각
        private const float VerticalMargin = 1.35f;
        // 정면에서 옆으로 돌린 각도, 위에서 내려다보는 각도
        private const float SideAngle = 30f;
        private const float ElevationAngle = 15f;
        private const float IdleSampleTime = 0.5f;

        [MenuItem("Tools/Team Odd/Capture Boss Mission Shots")]
        private static void Capture()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            var saved = new List<string>();
            try
            {
                foreach ((string scene, string key) in ReadTargets())
                {
                    string scenePath = $"{SceneFolder}/{scene}.unity";
                    if (File.Exists(scenePath) == false)
                    {
                        Debug.LogWarning($"[BossShot] '{scenePath}' 씬이 없어 '{key}' 그림을 건너뜁니다.");
                        continue;
                    }

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    string assetPath = $"{ResourcesFolder}/{key}.png";
                    if (CaptureOpenScene(assetPath)) saved.Add(assetPath);
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            foreach (string path in saved) ImportAsSprite(path);
            Debug.Log($"[BossShot] 보스 그림 {saved.Count}장을 찍었습니다.\n{string.Join("\n", saved)}");
        }

        // (다음 씬 이름, 그림 키) — 씬이 정해진 임무만
        private static IEnumerable<(string scene, string key)> ReadTargets()
        {
            string[] lines = File.ReadAllLines(TablePath);
            string[] header = lines[0].Split('\t');
            int keyColumn = System.Array.IndexOf(header, "BossSpriteKey");
            int sceneColumn = System.Array.IndexOf(header, "NextStageName");
            foreach (string line in lines.Skip(1))
            {
                string[] cells = line.Split('\t');
                if (cells.Length <= Mathf.Max(keyColumn, sceneColumn)) continue;
                string scene = cells[sceneColumn].Trim();
                string key = cells[keyColumn].Trim();
                if (scene.Length > 0 && key.Length > 0) yield return (scene, key);
            }
        }

        private static bool CaptureOpenScene(string assetPath)
        {
            // 켜져 있는 본체만 고른다. 꺼진 분신(… Clone, 홀로그램 재질)은 찍지 않는다
            BossMonster boss = Object.FindObjectsByType<BossMonster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name.Contains("Clone") == false);
            if (boss == null)
            {
                Debug.LogWarning($"[BossShot] '{SceneManager.GetActiveScene().name}' 씬에서 켜져 있는 보스를 찾지 못했습니다.");
                return false;
            }

            Animator animator = boss.GetComponentInChildren<Animator>(true);
            AnimationClip idle = FindIdleClip(animator);
            AnimationMode.StartAnimationMode();
            try
            {
                if (idle != null)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, idle, Mathf.Min(IdleSampleTime, idle.length));
                    AnimationMode.EndSampling();
                }

                Bounds bounds = GetBounds(boss.gameObject);
                Texture2D shot = Render(boss.transform, bounds, FrontDirection(boss.transform, animator));
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                File.WriteAllBytes(assetPath, shot.EncodeToPNG());
                Object.DestroyImmediate(shot);
                return true;
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }

        // 컨트롤러 기본 상태의 모션, 없으면 이름에 idle이 든 클립
        private static AnimationClip FindIdleClip(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return null;
            if (animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller &&
                controller.layers.Length > 0 && controller.layers[0].stateMachine.defaultState?.motion is AnimationClip clip)
                return clip;
            return animator.runtimeAnimatorController.animationClips
                .FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle"));
        }

        // 대기 자세의 뼈 위치로 크기를 잰다. 에디터에서 스킨 메시 경계는 실제 몸보다 훨씬 크게 잡혀 카메라가 멀어진다.
        // 뼈는 몸 표면 안쪽이므로 조금 넓힌다. 뼈가 없으면 메시 경계를 쓴다.
        private static Bounds GetBounds(GameObject boss)
        {
            Transform[] bones = boss.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.enabled).SelectMany(r => r.bones).Where(b => b != null).Distinct().ToArray();
            if (bones.Length > 0)
            {
                var fromBones = new Bounds(bones[0].position, Vector3.zero);
                foreach (Transform bone in bones) fromBones.Encapsulate(bone.position);
                fromBones.Expand(fromBones.size * BonePadding);
                fromBones.min = new Vector3(fromBones.min.x, Mathf.Max(fromBones.min.y, boss.transform.position.y), fromBones.min.z);
                return fromBones;
            }

            Renderer[] renderers = boss.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray<Renderer>();
            if (renderers.Length == 0)
                renderers = boss.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray<Renderer>();
            if (renderers.Length == 0) return new Bounds(boss.transform.position + Vector3.up, Vector3.one * 2f);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        // 몸이 바라보는 방향(수평). 사람형 뼈대는 양 어깨를 잇는 선의 수직 방향(찍어 보고 앞쪽이 나오는 부호로 맞춤), 아니면 오브젝트 정면.
        // 사람형 보스는 모델이 오브젝트 정면과 다른 쪽을 보고 서 있어 오브젝트 정면으로 찍으면 옆모습이 된다.
        private static Vector3 FrontDirection(Transform boss, Animator animator)
        {
            Vector3 front = boss.forward;
            if (animator != null && animator.isHuman)
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (left != null && right != null) front = Vector3.Cross(Vector3.up, right.position - left.position);
            }
            front.y = 0f;
            return front.sqrMagnitude > 0.001f ? front.normalized : Vector3.forward;
        }

        // 보스 정면에서 옆으로 SideAngle, 위로 ElevationAngle 돌아간 곳에서 몸 가운데를 내려다보며 전신을 담는다
        private static Texture2D Render(Transform boss, Bounds bounds, Vector3 front)
        {
            GameObject player = GameObject.FindWithTag("Player");
            Vector3 side = Quaternion.AngleAxis(SideAngle, Vector3.up) * front;
            float elevation = ElevationAngle * Mathf.Deg2Rad;
            Vector3 toCamera = side * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);

            float height = bounds.size.y;
            float halfHeight = height * 0.5f * VerticalMargin;
            float depth = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float distance = halfHeight / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) + depth;
            float fieldOfView = FieldOfView;
            Vector3 target = bounds.center;
            Vector3 eye = target;

            // 보스와 카메라 사이에 벽이 있으면 벽 앞으로 당기고, 그만큼 화각을 넓혀 전신을 담는다
            float clear = ClearDistance(eye, toCamera, distance, boss, player);
            if (clear < distance)
            {
                distance = Mathf.Max(clear - 0.5f, depth + 0.5f);
                fieldOfView = Mathf.Clamp(2f * Mathf.Atan(halfHeight / Mathf.Max(distance - depth, 0.1f)) * Mathf.Rad2Deg, FieldOfView, MaxFieldOfView);
            }
            Vector3 position = eye + toCamera * distance;
            Debug.Log($"[BossShot] {SceneManager.GetActiveScene().name}: '{boss.name}' 크기 {bounds.size}, 거리 {distance:0.0}(막힘 {clear:0.0}), 화각 {fieldOfView:0}, 옆 {SideAngle:0}° 위 {ElevationAngle:0}°");

            Camera main = Camera.main;
            var go = new GameObject("Boss Shot Camera") { hideFlags = HideFlags.HideAndDontSave };
            // 다중 샘플링은 파이프라인 설정(URP 에셋)이 카메라 안에서 하므로 결과 텍스처는 1샘플로 둔다.
            // 결과 텍스처에 샘플 수를 따로 주면 URP 설정과 어긋나 마지막 복사가 실패하고 검은 그림이 된다.
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            try
            {
                Camera camera = go.AddComponent<Camera>();
                if (main != null) camera.CopyFrom(main);
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                camera.fieldOfView = fieldOfView;
                camera.aspect = (float)Width / Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 500f);
                camera.targetTexture = rt;

                // 플레이어 캐릭터가 그림에 걸리지 않게 잠깐 숨긴다
                Renderer[] hidden = player != null ? player.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray() : new Renderer[0];
                foreach (Renderer r in hidden) r.enabled = false;
                camera.Render();
                foreach (Renderer r in hidden) r.enabled = true;

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;
                return shot;
            }
            finally
            {
                Object.DestroyImmediate(go);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        // 눈높이에서 카메라 쪽으로 처음 부딪히는 벽까지 거리(보스·플레이어·트리거는 무시)
        private static float ClearDistance(Vector3 origin, Vector3 direction, float distance, Transform boss, GameObject player)
        {
            Physics.SyncTransforms();
            float nearest = distance;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.collider.transform;
                if (t.IsChildOf(boss) || (player != null && t.IsChildOf(player.transform))) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return nearest;
        }

        private static void ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
    }
}
