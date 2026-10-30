using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TeamOdd.EditorTools
{
    // 임시 작업용: Temp/EditorCommand/command.txt에 적힌 명령을 에디터에서 실행하고 결과를 result.txt에 남긴다.
    // 정해 둔 명령만 받으며, 환경 배치 반복 작업이 끝나면 이 파일은 지운다.
    [InitializeOnLoad]
    internal static class EditorCommandBridge
    {
        private const string Folder = "Temp/EditorCommand";
        private static readonly string CommandPath = Path.Combine(Folder, "command.txt");
        private static readonly string ResultPath = Path.Combine(Folder, "result.txt");
        private static double nextPoll;

        static EditorCommandBridge()
        {
            Directory.CreateDirectory(Folder);
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (File.Exists(CommandPath) == false || EditorApplication.isCompiling || EditorApplication.isPlaying) return;

            string[] lines = File.ReadAllLines(CommandPath);
            File.Delete(CommandPath);
            StringBuilder result = new();

            foreach (string line in lines)
            {
                string[] parts = line.Trim().Split(' ', 2);
                switch (parts[0])
                {
                    case "refresh":
                        AssetDatabase.Refresh();
                        result.AppendLine("refresh: ok");
                        break;
                    case "build-arena":
                        result.AppendLine($"build-arena: {EditorApplication.ExecuteMenuItem("Tools/Team Odd/Build Environment Demo Arena")}");
                        break;
                    case "capture":
                        result.AppendLine($"capture: {EditorApplication.ExecuteMenuItem("Tools/Team Odd/Capture Scene Preview")}");
                        break;
                    case "style":
                        // 저장하지 않는 새 씬에 스타일을 배치, 열린 씬에 저장 안 된 변경이 있으면 건드리지 않음
                        UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        if (active.isDirty && string.IsNullOrEmpty(active.path) == false)
                        {
                            result.AppendLine("style: active scene has unsaved changes");
                            break;
                        }
                        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);
                        string menu = parts[1] switch
                        {
                            "A" => "A. Observation Lab",
                            "B" => "B. Heavy Containment Bunker",
                            _ => "C. Gravity Reactor",
                        };
                        result.AppendLine($"style {parts[1]}: {EditorApplication.ExecuteMenuItem("Tools/Team Odd/Containment Arena/" + menu)}");
                        break;
                    case "menu":
                        // 지금 열린 씬에서 팀 메뉴만 실행
                        result.AppendLine(parts[1].StartsWith("Tools/Team Odd/")
                            ? $"menu: {EditorApplication.ExecuteMenuItem(parts[1])}"
                            : "menu: only Tools/Team Odd/ is allowed");
                        break;
                    case "deactivate":
                        // 루트 이름/자식 경로로 찾은 오브젝트를 끔 (Undo 가능)
                        string[] path = parts[1].Split('/', 2);
                        Transform found = null;
                        foreach (GameObject rootObject in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                            if (rootObject.name == path[0]) found = path.Length == 1 ? rootObject.transform : rootObject.transform.Find(path[1]);
                        if (found != null)
                        {
                            Undo.RecordObject(found.gameObject, "Deactivate");
                            found.gameObject.SetActive(false);
                        }
                        result.AppendLine($"deactivate {parts[1]}: {found != null}");
                        break;
                    case "scene":
                        // 열린 씬마다 경로와 저장 안 된 변경 여부를 알려 줌
                        for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                        {
                            UnityEngine.SceneManagement.Scene loaded = UnityEngine.SceneManagement.SceneManager.GetSceneAt(index);
                            result.AppendLine($"scene: {loaded.path} dirty={loaded.isDirty}");
                        }
                        break;
                    case "save":
                        result.AppendLine($"save: {UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene())}");
                        break;
                    case "open":
                        // style로 만든 임시 씬은 저장하지 않고 버린다.
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parts[1]);
                        result.AppendLine($"open: {parts[1]}");
                        break;
                    case "sheet":
                        string[] sheetArgs = parts[1].Split(' ', 2);
                        result.AppendLine($"sheet {sheetArgs[0]}: {RenderSheet(sheetArgs[0], sheetArgs[1].Split(','))}");
                        break;
                    case "view":
                        // view <이름> <x,y,z> <x,y,z>: 한 시점을 Temp/ScenePreview/view_이름.png로 찍음
                        string[] viewArgs = parts[1].Split(' ');
                        result.AppendLine($"view {viewArgs[0]}: {RenderView(viewArgs[0], ParseVector(viewArgs[1]), ParseVector(viewArgs[2]))}");
                        break;
                    case "viewpp":
                        // view와 같지만 씬의 Volume 포스트 프로세싱을 적용해서 찍음
                        string[] viewPpArgs = parts[1].Split(' ');
                        result.AppendLine($"viewpp {viewPpArgs[0]}: {RenderView(viewPpArgs[0], ParseVector(viewPpArgs[1]), ParseVector(viewPpArgs[2]), true)}");
                        break;
                    case "shards":
                        // 열린 씬의 WallFragmentSpawner마다 금속 조각 생성 메뉴를 실행
                        int spawnerCount = 0;
                        foreach (TeamOdd.Arena.WallFragmentSpawner spawner in Object.FindObjectsByType<TeamOdd.Arena.WallFragmentSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            typeof(TeamOdd.Arena.WallFragmentSpawner).GetMethod("GenerateMetalShardPrefabs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(spawner, null);
                            spawnerCount++;
                        }
                        result.AppendLine($"shards: {spawnerCount}");
                        break;
                    case "debris":
                        // debris <소품 이름들>: 소품 옆에 그 파편 머티리얼을 입힌 금속 조각을 늘어놓고 한 장씩 찍음
                        foreach (string prop in parts[1].Split(','))
                            result.AppendLine($"debris {prop}: {RenderDebris(prop.Trim())}");
                        break;
                    case "measure":
                        foreach (string name in parts[1].Split(','))
                            result.AppendLine($"measure {name.Trim()}: {Measure(name.Trim())}");
                        break;
                    default:
                        result.AppendLine($"unknown: {line}");
                        break;
                }
            }

            File.WriteAllText(ResultPath, result.ToString());
        }

        // 프리팹들을 씬 멀리 하나씩 놓고 찍어 4열 격자 한 장(Temp/ScenePreview/sheet_이름.png)으로 저장
        // 이름에 '/'가 있으면 Assets 기준 경로, 없으면 Sci-Fi Base 프리팹 이름으로 찾는다.
        private static string RenderSheet(string sheetName, string[] names)
        {
            const int Tile = 400;
            const int Columns = 4;
            int rows = Mathf.CeilToInt(names.Length / (float)Columns);
            Texture2D sheet = new(Tile * Columns, Tile * rows, TextureFormat.RGB24, false);
            RenderTexture target = new(Tile, Tile, 24, RenderTextureFormat.ARGB32);
            Camera camera = new GameObject("Sheet Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.fieldOfView = 40f;
            camera.farClipPlane = 2000f;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            Vector3 spot = new(3000f, 0f, 3000f);
            StringBuilder missing = new();

            try
            {
                for (int index = 0; index < names.Length; index++)
                {
                    string name = names[index].Trim();
                    string path = name.Contains("/") ? name : $"Assets/Sci-Fi Base/Prefabs/{name}.prefab";
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) { missing.Append(name).Append(' '); continue; }

                    GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    probe.transform.position = spot;
                    Bounds bounds = new(spot, Vector3.one);
                    bool found = false;
                    foreach (Renderer renderer in probe.GetComponentsInChildren<Renderer>())
                    {
                        if (found) bounds.Encapsulate(renderer.bounds);
                        else bounds = renderer.bounds;
                        found = true;
                    }

                    // 앞쪽 비스듬히 위에서 바운드 전체가 들어오도록 거리를 잡음
                    float radius = bounds.extents.magnitude;
                    float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                    Vector3 direction = Quaternion.Euler(20f, 215f, 0f) * Vector3.forward;
                    camera.transform.SetPositionAndRotation(bounds.center - direction * distance, Quaternion.LookRotation(direction));
                    camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
                    camera.Render();

                    RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0, 0, Tile, Tile), (index % Columns) * Tile, (rows - 1 - index / Columns) * Tile);
                    Object.DestroyImmediate(probe);
                }

                sheet.Apply();
                Directory.CreateDirectory("Temp/ScenePreview");
                File.WriteAllBytes($"Temp/ScenePreview/sheet_{sheetName}.png", sheet.EncodeToPNG());
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderTexture.active = null;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sheet);
            }

            return missing.Length == 0 ? "ok" : $"missing {missing}";
        }

        private static string RenderDebris(string prop)
        {
            const string Folder = "Assets/Prefabs/8. Environmental Mechanics/";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}PsychokinesisProps/{prop}.prefab");
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}PsychokinesisProps/{prop}_Debris.mat");
            if (prefab == null || material == null) return "missing";

            Vector3 spot = new(3000f, 0f, 3000f);
            GameObject root = new("Debris Preview");
            GameObject light = new("Preview Light");
            try
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.transform.position = spot;
                Bounds bounds = instance.GetComponent<Renderer>().bounds;
                for (int index = 0; index < 6; index++)
                {
                    GameObject shard = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}GeneratedFragments/MetalShard/MetalShardObject0{index + 1}.prefab"), root.transform);
                    shard.transform.SetPositionAndRotation(spot + new Vector3(bounds.extents.x + 0.6f + (index % 3) * 0.55f, 0.3f + (index / 3) * 0.6f, -bounds.extents.z), Quaternion.Euler(-60f + index * 25f, 200f + index * 30f, index * 40f));
                    shard.GetComponent<Renderer>().sharedMaterial = material;
                }
                Light lamp = light.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 20f;
                lamp.intensity = 20f;
                light.transform.position = spot + new Vector3(1f, 4f, -4f);
                Vector3 center = spot + new Vector3(bounds.extents.x * 0.5f + 0.5f, bounds.extents.y, 0f);
                return RenderView("debris_" + prop.Replace(' ', '_'), center + new Vector3(0.5f, 1.5f, -4.5f) * Mathf.Max(1f, bounds.extents.magnitude), center);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(light);
            }
        }

        private static Vector3 ParseVector(string text)
        {
            string[] values = text.Split(',');
            return new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
        }

        private static string RenderView(string name, Vector3 position, Vector3 lookAt, bool postProcessing = false)
        {
            RenderTexture target = new(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new(1280, 720, TextureFormat.RGB24, false);
            Camera camera = new GameObject("View Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            try
            {
                UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera).renderPostProcessing = postProcessing;
                camera.fieldOfView = 55f;
                camera.farClipPlane = 1000f;
                camera.targetTexture = target;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes($"Temp/ScenePreview/view_{name}.png", image.EncodeToPNG());
                return "ok";
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        // 회전 없이 놓았을 때의 렌더러 바운드 크기와, 피벗 기준 바운드 중심
        private static string Measure(string prefabName)
        {
            string path = prefabName.Contains("/") ? prefabName : $"Assets/Sci-Fi Base/Prefabs/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return "missing";

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in probe.GetComponentsInChildren<Renderer>())
            {
                if (renderer is not MeshRenderer and not SkinnedMeshRenderer) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }
            bool hasCollider = probe.GetComponentInChildren<Collider>() != null;
            Object.DestroyImmediate(probe);
            return found ? $"size={bounds.size:F2} center={bounds.center:F2} collider={hasCollider}" : "no renderer";
        }
    }
}
