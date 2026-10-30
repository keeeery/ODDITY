using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TeamOdd.EditorTools
{
    // 씬 텍스트에 SF 느낌의 영문 폰트를 적용한다.
    // 영문 폰트에는 한글이 없으므로 한글 폰트를 Fallback 으로 연결해 한글은 그 폰트로 표시한다.
    // TMP 폰트 에셋이 없으면 원본 폰트 파일로 만든다.
    internal static class StartSceneFontApplier
    {
        private const string StartScenePath = "Assets/Scenes/1. Player/0. Start.unity";
        private const string GetMissionScenePath = "Assets/Scenes/1. Player/1. Get mission 1.unity";
        // Get mission 씬에서 실행 중에 복제해 쓰는 텍스트 프리팹
        private static readonly string[] GetMissionPrefabPaths = { "Assets/Prefabs/9. Mission/Quest 1.prefab" };

        private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;/|[]()-_+!?#%&*<>=\"'";
        // 시작 씬에서 쓰는 한글. 나머지 한글은 실행 중에 아틀라스에 추가된다.
        private const string KoreanCharacters = "작전개시스템종료";

        private const string NotoSansKRAssetPath = "Assets/Font/Noto_Sans_KR/static/NotoSansKR-Regular SDF.asset";
        private const string OrbitPath = "Assets/Font/Orbit/Orbit-Regular.ttf";
        private const string OrbitAssetPath = "Assets/Font/Orbit/Orbit SDF.asset";
        private const string OrbitronPath = "Assets/Font/Orbitron/Orbitron.ttf";
        private const string OrbitronAssetPath = "Assets/Font/Orbitron/Orbitron SDF.asset";
        private const string AquirePath = "Assets/Font/aquire-font/Aquire-BW0ox.otf";
        private const string AquireAssetPath = "Assets/Font/aquire-font/Aquire SDF.asset";

        private static readonly string[] EnglishTextNames = { "TitleText", "SubtitleText", "FooterText" };

        [MenuItem("Tools/Team Odd/Apply Orbitron To Start Scene")]
        private static void ApplyOrbitron()
        {
            // 영문 텍스트에만 적용 (버튼의 한글 텍스트는 NotoSansKR 그대로)
            TMP_FontAsset orbitron = LoadOrCreateFontAsset(OrbitronPath, OrbitronAssetPath, Characters);
            SetFallback(orbitron, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoSansKRAssetPath));
            ApplyToScene(StartScenePath, orbitron, null, EnglishTextNames);
        }

        [MenuItem("Tools/Team Odd/Apply Aquire To Start Scene")]
        private static void ApplyAquireToStartScene()
        {
            ApplyToScene(StartScenePath, LoadAquire(), null, null);
        }

        [MenuItem("Tools/Team Odd/Apply Aquire To Get Mission Scene")]
        private static void ApplyAquireToGetMissionScene()
        {
            TMP_FontAsset aquire = LoadAquire();
            Font legacyAquire = LoadLegacyAquire();

            if (ApplyToScene(GetMissionScenePath, aquire, legacyAquire, null) == false)
                return;

            foreach (string prefabPath in GetMissionPrefabPaths)
                ApplyToPrefab(prefabPath, aquire, legacyAquire);
        }

        // 한글과 Aquire 에 없는 기호(: / 등)는 Aquire 와 어울리는 Orbit 으로 표시한다.
        private static TMP_FontAsset LoadAquire()
        {
            TMP_FontAsset aquire = LoadOrCreateFontAsset(AquirePath, AquireAssetPath, Characters);
            SetFallback(aquire, LoadOrCreateFontAsset(OrbitPath, OrbitAssetPath, Characters + KoreanCharacters));
            return aquire;
        }

        // 레거시 Text 용 Aquire. 한글은 폰트 임포터의 Font Names 에 넣은 Orbit 으로 표시된다.
        private static Font LoadLegacyAquire()
        {
            TrueTypeFontImporter importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(AquirePath);
            if (System.Array.IndexOf(importer.fontNames, "Orbit") < 0)
            {
                importer.fontNames = new[] { "Aquire", "Orbit" };
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Font>(AquirePath);
        }

        // textNames 가 null 이면 씬의 모든 텍스트에 적용한다. legacyFont 가 null 이면 레거시 Text 는 건드리지 않는다.
        // 씬이 열려 있고 저장 안 된 변경이 있으면 덮어쓰지 않도록 중단한다.
        private static bool ApplyToScene(string scenePath, TMP_FontAsset fontAsset, Font legacyFont, string[] textNames)
        {
            if (fontAsset == null)
                return false;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.isLoaded;
            if (wasLoaded && scene.isDirty)
            {
                Debug.LogWarning($"[StartSceneFontApplier] '{scene.name}' 에 저장 안 된 변경이 있어 중단합니다. 저장한 뒤 다시 실행하세요.");
                return false;
            }

            // 열려 있는 다른 씬은 건드리지 않도록 추가로 열어서 고치고 저장한다.
            if (wasLoaded == false)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                count += ApplyToHierarchy(root, fontAsset, legacyFont, textNames);

            EditorSceneManager.SaveScene(scene);
            if (wasLoaded == false)
                EditorSceneManager.CloseScene(scene, true);

            Debug.Log($"[StartSceneFontApplier] '{scene.name}' 에 {fontAsset.name} 적용: {count}개");
            return true;
        }

        private static void ApplyToPrefab(string prefabPath, TMP_FontAsset fontAsset, Font legacyFont)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            int count = ApplyToHierarchy(prefab, fontAsset, legacyFont, null);
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);

            Debug.Log($"[StartSceneFontApplier] '{prefabPath}' 에 {fontAsset.name} 적용: {count}개");
        }

        private static int ApplyToHierarchy(GameObject root, TMP_FontAsset fontAsset, Font legacyFont, string[] textNames)
        {
            int count = 0;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (textNames != null && System.Array.IndexOf(textNames, text.name) < 0)
                    continue;

                Undo.RecordObject(text, "Apply Font");
                text.font = fontAsset;
                EditorUtility.SetDirty(text);
                count++;
            }

            if (legacyFont == null)
                return count;

            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(text, "Apply Font");
                text.font = legacyFont;
                EditorUtility.SetDirty(text);
                count++;
            }

            // 실행 중에 텍스트를 만드는 컴포넌트(SkillSlotHUD.font, AugmentationChipPanel.labelFont 등)의 폰트 필드
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // TMP_SubMeshUI 는 TMP 가 Fallback 글자용으로 관리하므로 건드리지 않는다.
                if (behaviour == null || behaviour is TMP_Text || behaviour is Text || behaviour is TMP_SubMeshUI)
                    continue;

                count += ReplaceFontFields(behaviour, fontAsset, legacyFont);
            }

            return count;
        }

        private static int ReplaceFontFields(MonoBehaviour behaviour, TMP_FontAsset fontAsset, Font legacyFont)
        {
            int count = 0;
            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                Object value = property.objectReferenceValue;
                if (value is TMP_FontAsset && value != fontAsset)
                {
                    property.objectReferenceValue = fontAsset;
                    count++;
                }
                else if (value is Font && value != legacyFont)
                {
                    property.objectReferenceValue = legacyFont;
                    count++;
                }
            }

            serialized.ApplyModifiedProperties();
            return count;
        }

        private static TMP_FontAsset LoadOrCreateFontAsset(string fontPath, string assetPath, string characters)
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset == null)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null)
                {
                    Debug.LogWarning($"[StartSceneFontApplier] '{fontPath}' 을(를) 찾을 수 없습니다.");
                    return null;
                }

                fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                fontAsset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                AssetDatabase.CreateAsset(fontAsset, assetPath);

                // 아틀라스 텍스처와 머티리얼은 폰트 에셋 안에 함께 저장해야 다시 열었을 때 유지된다.
                fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

                // 쓸 글자를 미리 아틀라스에 넣어 둔다.
                fontAsset.TryAddCharacters(characters);
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
            }

            return fontAsset;
        }

        // Fallback 을 이 폰트 하나로 교체한다.
        private static void SetFallback(TMP_FontAsset fontAsset, TMP_FontAsset fallback)
        {
            if (fontAsset == null || fallback == null)
            {
                Debug.LogWarning("[StartSceneFontApplier] 폰트 에셋이 없어 Fallback 을 연결하지 못했습니다.");
                return;
            }

            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
        }
    }
}
