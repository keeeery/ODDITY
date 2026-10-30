using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TeamOdd.EditorTools
{
    // 외부 에셋에 들어 있는 Built-in(Standard, Legacy, Particles) 머티리얼을 URP 머티리얼로 바꾸는 메뉴
    // Unity 기본 메뉴(Convert All Built-In Materials)는 프로젝트 전체를 바꾸므로, 지정한 폴더만 바꾸도록 범위를 좁힌다.
    internal static class ImportedMaterialUrpConverter
    {
        private static readonly string[] TargetFolders = { "Assets/Sci-Fi Base", "Assets/Knife", "Assets/Heavy Station Kit" };

        // Heavy Station Kit 프리팹에는 설치되지 않은 Post Processing v2 컴포넌트(PostProcessLayer, PostProcessVolume)가 붙어 있어 Missing Script로 남는다.
        private static readonly string[] MissingScriptFolders = { "Assets/Heavy Station Kit" };

        // DotAnimatedTexture가 편집 모드에서 Material.Instantiate로 만든 복제본이 데모 씬 안에 저장되어 있어, 에셋 머티리얼과 따로 바꿔야 한다.
        [MenuItem("Tools/Team Odd/Convert Heavy Station Demo Scene Materials to URP")]
        private static void ConvertDemoSceneMaterials()
        {
            List<MaterialUpgrader> upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(GraphicsSettings.currentRenderPipelineAssetType);

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", MissingScriptFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.isLoaded;
                if (wasLoaded == false) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                HashSet<Material> visited = new();
                int converted = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || EditorUtility.IsPersistent(material) || visited.Add(material) == false) continue;

                    Shader before = material.shader;
                    MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.CleanupNonUpgradedProperties);
                    if (material.shader != before) converted++;
                }

                if (converted > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if (wasLoaded == false) EditorSceneManager.CloseScene(scene, true);
                Debug.Log($"씬 내장 머티리얼 {converted}개를 URP로 변환: {path}");
            }
        }

        [MenuItem("Tools/Team Odd/Remove Missing Scripts in Imported Asset Prefabs")]
        private static void RemoveMissingScripts()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", MissingScriptFolders);
            int removed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int count = 0;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

                if (count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    removed += count;
                    Debug.Log($"Missing Script {count}개 제거: {path}");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log($"프리팹 {guids.Length}개에서 Missing Script {removed}개를 제거했습니다.");
        }

        [MenuItem("Tools/Team Odd/Convert Imported Asset Materials to URP")]
        private static void Convert()
        {
            if (GraphicsSettings.isScriptableRenderPipelineEnabled == false)
            {
                Debug.LogError("URP가 활성화되어 있지 않아 머티리얼을 변환하지 않았습니다.");
                return;
            }

            List<MaterialUpgrader> upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(GraphicsSettings.currentRenderPipelineAssetType);
            string[] guids = AssetDatabase.FindAssets("t:Material", TargetFolders);
            int converted = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int index = 0; index < guids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                    EditorUtility.DisplayProgressBar("Convert Materials to URP", path, (float)index / guids.Length);

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null) continue;

                    // 업그레이더가 없는 셰이더(이미 URP인 머티리얼 등)는 그대로 두므로 셰이더가 바뀌었는지로 변환 여부를 판단
                    Shader before = material.shader;
                    MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.CleanupNonUpgradedProperties);
                    if (material.shader == before) continue;

                    EditorUtility.SetDirty(material);
                    converted++;
                    Debug.Log($"URP 변환: {path} ({before.name} → {material.shader.name})", material);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"머티리얼 {guids.Length}개 중 {converted}개를 URP로 변환했습니다.");
        }
    }
}
