using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd.UI
{
    // SkillSlotHUD가 있는 모든 씬과 프리팹을 열어 아직 슬롯이 없는 HUD에 "1. Build Skill Slot UI" 를 실행하고 저장한다.
    public static class SkillSlotHUDBuilder
    {
        [MenuItem("Tools/UI/Build Skill Slot UI In All Scenes")]
        private static void BuildAll()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;

            string scriptGuid = null;
            foreach (string guid in AssetDatabase.FindAssets($"{nameof(SkillSlotHUD)} t:MonoScript"))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(SkillSlotHUD)) scriptGuid = guid;
            }
            if (scriptGuid == null) return;

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            int built = 0;

            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (UsesScript(path, scriptGuid) == false) continue;

                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    int count = BuildIn(root.GetComponentsInChildren<SkillSlotHUD>(true), path);
                    if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                    built += count;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (UsesScript(path, scriptGuid) == false) continue;

                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int count = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        count += BuildIn(root.GetComponentsInChildren<SkillSlotHUD>(true), path);
                    if (count == 0) continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    built += count;
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            Debug.Log($"[{nameof(SkillSlotHUDBuilder)}] 스킬 슬롯 UI {built}곳 생성 완료.");
        }

        private static int BuildIn(SkillSlotHUD[] huds, string path)
        {
            int count = 0;
            foreach (SkillSlotHUD hud in huds)
            {
                if (hud.HasBuiltSlots) continue;
                hud.BuildUI();
                count++;
                Debug.Log($"[{nameof(SkillSlotHUDBuilder)}] {path} / {hud.name}");
            }
            return count;
        }

        // 파일에 스크립트 GUID가 들어 있는 것만 연다.
        private static bool UsesScript(string path, string scriptGuid) =>
            File.ReadAllText(path).Contains(scriptGuid);
    }
}
