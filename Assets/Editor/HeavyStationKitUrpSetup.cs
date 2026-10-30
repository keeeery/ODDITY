using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TeamOdd.EditorTools
{
    // Heavy Station Kit 데모의 Post Processing v2 프로필(Hangars Profile)을 URP Volume Profile로 옮기고 Hangars 씬에 연결하는 메뉴
    // AO는 PC_Renderer의 SSAO 기능이 맡고, SSR은 URP에 없으며, DoF(초점 0.5m)는 3인칭 전투 화면을 흐리게 하므로 옮기지 않는다.
    internal static class HeavyStationKitUrpSetup
    {
        private const string ProfilePath = "Assets/Heavy Station Kit/HANGARS/Post Processing/Hangars URP Profile.asset";
        private const string ScenePath = "Assets/Heavy Station Kit/HANGARS/Scenes/Hangars.unity";
        private const string PlayerPrefabPath = "Assets/Heavy Station Kit/_common/Prefabs/FPC_Player.prefab";
        private const string VolumeName = "Global Volume (URP)";

        [MenuItem("Tools/Team Odd/Heavy Station Kit/Setup URP Post Processing")]
        private static void Setup()
        {
            VolumeProfile profile = CreateProfile();
            EnableCameraPostProcessing();
            AddVolumeToScene(profile);
        }

        private static VolumeProfile CreateProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null)
            {
                Debug.Log($"이미 있는 URP 프로필을 사용합니다: {ProfilePath}");
                return profile;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // PPv2 Bloom intensity 3은 셰이더에서 2^(3/10)-1 ≈ 0.23배로 쓰이므로 URP에는 그 값을 그대로 넣는다.
            Bloom bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.23f);
            bloom.scatter.Override(0.7f);

            // ColorGrading: HDR 모드 + ACES 톤매퍼, Post Exposure 1.44, Lift/Gamma 트랙볼 값
            Tonemapping tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments colorAdjustments = AddComponent<ColorAdjustments>(profile);
            colorAdjustments.postExposure.Override(1.44f);

            LiftGammaGain liftGammaGain = AddComponent<LiftGammaGain>(profile);
            liftGammaGain.lift.Override(new Vector4(0.8928171f, 0.9196129f, 1f, 0.019860972f));
            liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, 0.03423784f));

            ChromaticAberration chromaticAberration = AddComponent<ChromaticAberration>(profile);
            chromaticAberration.intensity.Override(0.25f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"URP 프로필 생성: {ProfilePath}", profile);
            return profile;
        }

        private static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // URP 카메라는 Post Processing이 꺼진 채로 추가되므로 데모 플레이어 카메라에서 켠다.
        private static void EnableCameraPostProcessing()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void AddVolumeToScene(VolumeProfile profile)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.isLoaded;
            if (wasLoaded == false) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Volume volume = null;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
                if (rootObject.name == VolumeName) volume = rootObject.GetComponent<Volume>();

            if (volume == null)
            {
                GameObject volumeObject = new(VolumeName);
                SceneManager.MoveGameObjectToScene(volumeObject, scene);
                volume = volumeObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (wasLoaded == false) EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"Hangars 씬에 {VolumeName} 연결 완료");
        }
    }
}
