using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 씬마다 복사돼 있던 HUD 캔버스를 프리팹으로 만들고, 열린 씬의 HUD를 프리팹 인스턴스로 교체한다.
    // 프리팹 캔버스는 1920x1080 기준 Scale With Screen Size(Expand)라서 해상도가 바뀌어도 화면 대비 크기는 유지되고
    // 각 그룹은 자기 앵커(모서리)를 따라 위치만 이동한다.
    public static class HUDPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/10. UI/HUD";
        private const string HUDPrefabPath = PrefabFolder + "/HUD Canvas.prefab";
        private const string DeathScreenPrefabPath = PrefabFolder + "/DeathScreen.prefab";
        private const string BossIntroTitlePrefabPath = PrefabFolder + "/BossIntroTitle.prefab";

        private const string SystemMessageName = "SystemMessage";
        private const string SystemMessageFontPath = "Assets/Font/Pretendard/Pretendard-Medium SDF.asset";
        private const string SystemMessageMaterialPath = "Assets/Font/Pretendard/Pretendard-Medium SDF - System Message.mat";

        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        // HUD Canvas 프리팹에 들어가는 그룹. 나머지 자식은 씬에만 남는다.
        private static readonly string[] CoreGroups =
        {
            "DamageVignette", "Crosshair", "PlayerInfo", "EnemyInfo", "Minimap", "SkillSlots"
        };

        [MenuItem("Tools/UI/HUD/1. Create HUD Prefabs From Open Scene")]
        private static void CreatePrefabs()
        {
            GameObject source = FindSceneHUD(SceneManager.GetActiveScene());
            if (source == null) return;

            if (Directory.Exists(PrefabFolder) == false)
            {
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
            }

            // 비활성 부모 밑에서 복제해 OnEnable 등이 돌지 않게 한다.
            GameObject holder = new("HUD Prefab Holder");
            holder.SetActive(false);
            try
            {
                GameObject hud = Object.Instantiate(source, holder.transform);
                hud.name = source.name;
                foreach (Transform child in ChildrenOf(hud.transform))
                    if (System.Array.IndexOf(CoreGroups, child.name) < 0) Object.DestroyImmediate(child.gameObject);
                ApplyScaler(hud);
                SavePrefab(hud, HUDPrefabPath);

                foreach (DeathScreen death in source.GetComponentsInChildren<DeathScreen>(true))
                    SavePrefab(Object.Instantiate(death.gameObject, holder.transform), DeathScreenPrefabPath, death.name);
                foreach (BossIntroTitle title in source.GetComponentsInChildren<BossIntroTitle>(true))
                    SavePrefab(Object.Instantiate(title.gameObject, holder.transform), BossIntroTitlePrefabPath, title.name);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }

            Debug.Log($"[{nameof(HUDPrefabBuilder)}] {PrefabFolder} 에 HUD 프리팹 저장 완료.");
        }

        // 기상기 슬롯(화면 아래에서 약 184px) 바로 위에 시스템 안내 한 줄을 HUD 프리팹에 넣는다. 이미 있으면 위치·글꼴만 다시 맞춘다.
        [MenuItem("Tools/UI/HUD/3. Add System Message To HUD Prefab")]
        private static void AddSystemMessage()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HUDPrefabPath);
            try
            {
                Transform existing = root.transform.Find(SystemMessageName);
                GameObject message = existing != null ? existing.gameObject : new GameObject(SystemMessageName, typeof(RectTransform));
                message.transform.SetParent(root.transform, false);
                message.transform.SetAsLastSibling();

                var rect = (RectTransform)message.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 204f);
                rect.sizeDelta = new Vector2(900f, 32f);

                if (message.TryGetComponent(out CanvasGroup group) == false) group = message.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                Transform label = message.transform.Find("Text");
                if (label == null)
                {
                    label = new GameObject("Text", typeof(RectTransform)).transform;
                    label.SetParent(message.transform, false);
                }
                var labelRect = (RectTransform)label;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

                if (label.TryGetComponent(out TextMeshProUGUI text) == false) text = label.gameObject.AddComponent<TextMeshProUGUI>();
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SystemMessageFontPath);
                text.fontSize = 21f;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
                text.color = new Color(0.84705883f, 0.8235294f, 0.76862746f, 1f);
                text.text = string.Empty;
                text.fontSharedMaterial = SystemMessageMaterial(text.font);

                if (message.TryGetComponent(out SystemMessage component) == false) component = message.AddComponent<SystemMessage>();
                var serialized = new SerializedObject(component);
                serialized.FindProperty("text").objectReferenceValue = text;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, HUDPrefabPath);
                Debug.Log($"[{nameof(HUDPrefabBuilder)}] {HUDPrefabPath} 에 {SystemMessageName} 추가 완료.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 밝은 바닥·이펙트 위에서도 읽히도록 바깥 외곽선과 오른쪽 아래로 살짝 번지는 그림자를 켠 글꼴 머티리얼.
        // 외곽선은 글자를 두껍게(FaceDilate) 한 만큼만 그려서 글자 안쪽을 깎지 않고 바깥으로만 생긴다.
        private static Material SystemMessageMaterial(TMP_FontAsset font)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SystemMessageMaterialPath);
            if (material == null)
            {
                material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(SystemMessageMaterialPath) };
                AssetDatabase.CreateAsset(material, SystemMessageMaterialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(font.material);
            }

            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.03f, 0.025f, 0.02f, 0.9f));
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);

            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.5f);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        [MenuItem("Tools/UI/HUD/2. Replace HUD In Open Scene With Prefab")]
        private static void ReplaceInOpenScene()
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HUDPrefabPath);
            if (hudPrefab == null)
            {
                Debug.LogError($"[{nameof(HUDPrefabBuilder)}] {HUDPrefabPath} 가 없습니다. 1번 메뉴를 먼저 실행하세요.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject hud = FindSceneHUD(scene);
            if (hud == null) return;
            if (PrefabUtility.IsPartOfPrefabInstance(hud))
            {
                Debug.Log($"[{nameof(HUDPrefabBuilder)}] {scene.name} 의 HUD는 이미 프리팹 인스턴스입니다.");
                return;
            }

            Undo.SetCurrentGroupName("Replace HUD With Prefab");
            int undoGroup = Undo.GetCurrentGroup();

            string hudName = hud.name;
            bool active = hud.activeSelf;
            List<SceneReference> references = CollectSceneReferences(hud.transform);

            // 사망 화면·보스 타이틀은 각자 프리팹으로 바꾼다. 그 밖의 자식(팀원 UI 등)은 씬에 추가된 오브젝트로 그대로 남는다.
            List<(GameObject child, GameObject prefab)> extras = new();
            foreach (Transform child in ChildrenOf(hud.transform))
            {
                if (hudPrefab.transform.Find(child.name) != null) continue;
                string path = child.GetComponent<DeathScreen>() != null ? DeathScreenPrefabPath
                    : child.GetComponent<BossIntroTitle>() != null ? BossIntroTitlePrefabPath
                    : null;
                GameObject prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                if (prefab != null) extras.Add((child.gameObject, prefab));
            }

            // 기존 오브젝트를 그대로 둔 채 프리팹 인스턴스로 바꾸므로 씬의 다른 오브젝트가 걸어둔 참조가 유지된다.
            // 값은 프리팹 것을 따르고(폰트·색 등), 씬 오브젝트 참조만 아래에서 다시 넣는다.
            ConvertToPrefabInstanceSettings settings = new()
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true,
                recordPropertyOverridesOfMatches = false,
                changeRootNameToAssetName = false
            };
            PrefabUtility.ConvertToPrefabInstance(hud, hudPrefab, settings, InteractionMode.UserAction);
            foreach ((GameObject child, GameObject prefab) in extras)
                PrefabUtility.ConvertToPrefabInstance(child, prefab, settings, InteractionMode.UserAction);

            Undo.RecordObject(hud, "Restore HUD Name");
            hud.name = hudName;
            hud.SetActive(active);
            int restored = RestoreSceneReferences(hud.transform, references);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud;

            Debug.Log($"[{nameof(HUDPrefabBuilder)}] {scene.name} 의 HUD를 프리팹으로 교체했습니다. " +
                      $"(하위 프리팹 {extras.Count}개, 씬 참조 {restored}개 복원) 확인 후 씬을 저장하세요.");
        }

        private readonly struct SceneReference
        {
            public readonly string OwnerPath;
            public readonly System.Type OwnerType;
            public readonly int OwnerIndex;
            public readonly string PropertyPath;
            public readonly Object Value;

            public SceneReference(string ownerPath, System.Type ownerType, int ownerIndex, string propertyPath, Object value)
            {
                OwnerPath = ownerPath;
                OwnerType = ownerType;
                OwnerIndex = ownerIndex;
                PropertyPath = propertyPath;
                Value = value;
            }
        }

        // 스크립트가 들고 있는 씬 오브젝트 참조(플레이어, 보스, HUD 내부 오브젝트 등)를 경로 기준으로 기록한다.
        private static List<SceneReference> CollectSceneReferences(Transform root)
        {
            List<SceneReference> references = new();
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                string ownerPath = RelativePath(behaviour.transform, root);
                int ownerIndex = IndexOfType(behaviour);

                SerializedProperty property = new SerializedObject(behaviour).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value)) continue;
                    references.Add(new SceneReference(ownerPath, behaviour.GetType(), ownerIndex, property.propertyPath, value));
                }
            }
            return references;
        }

        // 프리팹 값과 다른 참조만 다시 넣어 오버라이드로 남긴다.
        private static int RestoreSceneReferences(Transform root, List<SceneReference> references)
        {
            int restored = 0;
            foreach (SceneReference reference in references)
            {
                Transform owner = reference.OwnerPath.Length == 0 ? root : root.Find(reference.OwnerPath);
                Component component = owner != null ? NthOfType(owner.GetComponents(reference.OwnerType), reference.OwnerIndex) : null;
                if (component == null || reference.Value == null) continue;

                SerializedObject serialized = new(component);
                SerializedProperty property = serialized.FindProperty(reference.PropertyPath);
                if (property == null || property.objectReferenceValue == reference.Value) continue;
                property.objectReferenceValue = reference.Value;
                serialized.ApplyModifiedProperties();
                restored++;
            }
            return restored;
        }

        // PlayerHUD가 들어 있는 최상위 캔버스. 다른 UI 캔버스 안에 섞여 있으면 자동 교체하지 않는다.
        private static GameObject FindSceneHUD(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PlayerHUD playerHUD in root.GetComponentsInChildren<PlayerHUD>(true))
                {
                    Canvas canvas = playerHUD.GetComponentInParent<Canvas>(true);
                    if (canvas == null) continue;
                    canvas = canvas.rootCanvas;

                    if (playerHUD.transform.parent != canvas.transform)
                    {
                        Debug.LogError($"[{nameof(HUDPrefabBuilder)}] {scene.name}: HUD가 '{canvas.name}' 캔버스 안의 다른 오브젝트 밑에 있어 " +
                                       "자동으로 처리하지 않습니다. 캔버스 담당자와 협의 후 직접 교체하세요.", canvas);
                        return null;
                    }
                    return canvas.gameObject;
                }

            Debug.LogError($"[{nameof(HUDPrefabBuilder)}] {scene.name} 에서 PlayerHUD가 있는 캔버스를 찾지 못했습니다.");
            return null;
        }

        private static void ApplyScaler(GameObject hud)
        {
            CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        // 씬 오브젝트 참조는 프리팹에 담을 수 없으므로 비운다. 런타임에 각 스크립트가 직접 찾는다.
        private static void SavePrefab(GameObject root, string path, string name = null)
        {
            if (name != null) root.name = name;
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                SerializedObject serialized = new(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value) || IsInside(value, root.transform)) continue;
                    property.objectReferenceValue = null;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static int IndexOfType(Component component)
        {
            Component[] same = component.GetComponents(component.GetType());
            return System.Array.IndexOf(same, component);
        }

        private static Component NthOfType(Component[] components, int index) =>
            index >= 0 && index < components.Length ? components[index] : null;

        private static string RelativePath(Transform transform, Transform root)
        {
            List<string> names = new();
            for (Transform current = transform; current != root; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static bool IsInside(Object value, Transform root)
        {
            Transform transform = value switch
            {
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => null
            };
            return transform != null && transform.IsChildOf(root);
        }

        private static List<Transform> ChildrenOf(Transform parent)
        {
            List<Transform> children = new();
            foreach (Transform child in parent) children.Add(child);
            return children;
        }
    }
}
