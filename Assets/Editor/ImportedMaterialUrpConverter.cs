using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamOdd.EditorTools
{
    // 외부 에셋에 들어 있는 Built-in(Standard, Legacy, Particles) 머티리얼을 URP 머티리얼로 바꾸는 메뉴
    // Unity 기본 메뉴(Convert All Built-In Materials)는 프로젝트 전체를 바꾸므로, 지정한 폴더만 바꾸도록 범위를 좁힌다.
    internal static class ImportedMaterialUrpConverter
    {
        private static readonly string[] TargetFolders = { "Assets/Sci-Fi Base", "Assets/Knife" };

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
