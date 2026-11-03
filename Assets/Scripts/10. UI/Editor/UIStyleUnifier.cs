using System;
using System.Collections.Generic;
using TeamOdd.Reward;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TeamOdd.UI
{
    // Final 씬의 UI를 HUD 프리팹과 같은 생김새·해상도 대응으로 맞춘다.
    // - 해상도: 최상위 캔버스를 Scale With Screen Size(Expand)로 바꿔 어떤 비율에서도 기준 화면 전체가 보이게 하고,
    //   기준 해상도 크기로 고정된 전체 화면 오브젝트는 화면에 꽉 차도록 늘린다. 로비 UI Canvas 안에 복사돼 있던 HUD는 HUD 프리팹으로 바꾼다.
    // - 글꼴: Pretendard(본문 Medium, 제목 SemiBold). 게임 로고 글자만 기존 글꼴을 둔다.
    // - 색: HUD 글자색(#D8D2C4)과 체력·기력·그로기 바 색을 강조색으로 쓴다.
    // - 패널·버튼: 단색 Image를 VectorPanel(HUD 스킬 슬롯과 같은 그림자·1px 테두리·반투명 트랙)로 바꾼다.
    //   코드가 Image 타입으로 들고 있는 것은 바꾸지 않고 색만 맞춘다.
    // 기준 해상도 값은 그대로 두므로(1920x1080 또는 3840x2160) 테두리 두께 같은 값은 캔버스 단위(unit)로 환산해 넣는다.
    // 다듬기: 창은 반투명하게 겹치고, 안쪽 패널·버튼은 그림자 없이 옅은 바탕과 머리카락 굵기 테두리로 층을 나눈다.
    //   제목 띠는 바탕 없이 글자와 1px 구분선만 남기고, 4K 기준 팀 UI의 커다란 글자·버튼·목록 행·스크롤바는 HUD 비율로 줄인다.
    //   켜졌다 꺼지는 창·팝업에는 UIPopIn을 붙여 부드럽게 나타나게 한다.
    // HUD·사망 화면·보상창·보스 타이틀은 기준 스타일이므로 크기·배치 다듬기에서 건드리지 않는다.
    // 미니멀 리디자인: HUD 프리팹(체력바·스킬 슬롯)과 사망 화면·보스 타이틀을 뺀 창은 HUD 생김새를 따르지 않고 아래 규칙으로 칠한다.
    //   창 = 거의 불투명한 짙은 바탕 + 옅은 1px 외곽선 + 넓고 흐린 그림자
    //   안쪽 패널 = 테두리 없이 아주 옅은 바탕, 버튼 = 바탕 없는 외곽선 버튼(올리면 채워지고 선이 밝아짐)
    //   주 버튼 = 밝은 색으로 채우고 글자를 어둡게 뒤집어 화면에서 할 일이 하나로 보이게 한다.
    public static class UIStyleUnifier
    {
        private const string FinalSceneFolder = "Assets/Scenes/10. Build/2. Final";
        private const string HUDPrefabPath = "Assets/Prefabs/10. UI/HUD/HUD Canvas.prefab";
        private const string FontFolder = "Assets/Font/Pretendard/";

        // 프리팹 경로와 그 프리팹이 놓이는 캔버스의 기준 높이(캔버스 없이 다른 캔버스 안에 들어가는 프리팹용)
        // popIn: 프리팹 루트가 코드로 켜고 꺼지는 창이면 UIPopIn을 붙인다.
        private static readonly (string path, float referenceHeight, bool popIn)[] Prefabs =
        {
            ("Assets/Prefabs/10. UI/UI Canvas.prefab", 2160f, false),
            ("Assets/Prefabs/10. UI/Quest 1.prefab", 2160f, false),
            ("Assets/Prefabs/4. Superpower/Augmentation Chip Panel.prefab", 2160f, true),
            ("Assets/Prefabs/4. Superpower/Augmentation Chip Canvas.prefab", 2160f, false),
            ("Assets/Prefabs/10. UI/HUD/DeathScreen.prefab", 1080f, false),
            ("Assets/Prefabs/10. UI/HUD/BossIntroTitle.prefab", 1080f, false),
        };

        // 이 이름이 들어간 버튼은 주 행동(확인·수락·이동)이라 테두리를 더 밝게 둔다.
        private static readonly string[] PrimaryButtonNames = { "Accept", "Move", "Confirm", "Start", "OK" };

        // 1080p 기준 글자 크기 단계. 4K 기준 팀 UI의 큰 글자를 (이상, 미만) 구간별로 이 크기로 맞춘다.
        // 처음 적용했던 단계(30/22/18/16)는 너무 작아 아래 단계로 옮긴다. 두 단계 값이 겹치지 않아 여러 번 실행해도 같다.
        private static readonly (float min, float max, float size)[] TypeScale =
        {
            (44f, float.MaxValue, 40f), // 창 제목
            (34f, 44f, 29f),            // 소제목·버튼
            (26f, 34f, 24f),            // 본문
            (22f, 26f, 21f),            // 보조 글자
        };
        private static readonly (float from, float to)[] PreviousTypeScale = { (30f, 40f), (22f, 29f), (18f, 24f), (16f, 21f) };

        private const float ButtonHeight = 52f;       // 1080p 기준 버튼 높이
        private const float PreviousButtonHeight = 44f;
        private const float SquareButtonWidth = 120f; // 정사각형이던 버튼(닫기 등)의 너비
        private const float RowHeight = 64f;          // 1080p 기준 목록 행 높이
        private const float PreviousRowHeight = 56f;
        private const float ScrollbarWidth = 6f;      // 1080p 기준 스크롤바 너비

        // 로고는 브랜드 글꼴을 유지한다. 글자 내용으로 찾는다.
        private static readonly HashSet<string> LogoTexts = new() { "PROJECT : ODD", "ODD", "PROJECT" };

        // 1080p 기준 이 크기 이상이면 제목으로 보고 SemiBold를 쓴다.
        private const float TitleFontSize = 28f;

        private static class Palette
        {
            public static readonly Color Text = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
            public static readonly Color Positive = new(0.3607843f, 0.5411765f, 0.3137255f, 1f);
            public static readonly Color Danger = new(0.6588235f, 0.1568627f, 0.1686275f, 1f);
            public static readonly Color Gold = new(0.7686275f, 0.5764706f, 0.2901961f, 1f);
            public static readonly Color Dim = new(0.039f, 0.031f, 0.027f, 1f);
            public static readonly Color Track = new(0.07058824f, 0.07843137f, 0.09019608f, 0.85f);
            public static readonly Color RaisedTrack = new(0.1215686f, 0.1254902f, 0.1372549f, 0.9f);
            public static readonly Color Border = new(0.1176471f, 0.1058824f, 0.09019608f, 1f);
            public static readonly Color Shadow = new(0f, 0f, 0f, 0.35f);
            public static readonly Color Line = new(0.84705883f, 0.8235294f, 0.76862746f, 0.12f);
            public static readonly Color WindowTrack = new(0.043f, 0.047f, 0.055f, 0.94f);
            public static readonly Color WindowShadow = new(0f, 0f, 0f, 0.45f);
            public static readonly Color InverseText = new(0.07f, 0.07f, 0.08f, 1f);
            public static readonly Color ScrollTrack = new(0.84705883f, 0.8235294f, 0.76862746f, 0.04f);
            public static readonly Color ScrollHandle = new(0.84705883f, 0.8235294f, 0.76862746f, 0.25f);
            public static readonly Color Clear = new(0f, 0f, 0f, 0f);

            // 창 바탕 투명도, 전체 화면 막 투명도
            public const float WindowAlpha = 0.82f;
            public const float BackdropAlpha = 0.85f;

            public static Color TextAlpha(float alpha) => new(Text.r, Text.g, Text.b, alpha);
        }

        // VectorPanel 역할별 모양
        private enum PanelRole { Window, Header, Inset, Button, PrimaryButton }

        // 코드가 런타임에 칠하는 색은 직렬화된 필드를 팔레트 값으로 바꾼다. (타입 이름, 필드, 값)
        private static readonly (string type, string field, Color value)[] CodeColors =
        {
            ("StageMoveUI", "rowNormalColor", new Color(0.7f, 0.7f, 0.7f, 1f)),
            ("StageMoveUI", "rowSelectedColor", Color.white),
            ("ChipGridView", "cellColor", new Color(0.1215686f, 0.1254902f, 0.1372549f, 1f)),
            ("ChipGridView", "validColor", Palette.Positive),
            ("ChipGridView", "invalidColor", Palette.Danger),
        };

        // VectorPanel로 바꿔도 되는 참조 타입. Image·RawImage처럼 구체 타입으로 들고 있으면 바꾸지 않는다.
        private static readonly HashSet<string> GraphicCompatibleTypes = new()
        {
            "Graphic", "MaskableGraphic", "UIBehaviour", "MonoBehaviour", "Behaviour", "Component", "Object"
        };

        private sealed class Context
        {
            public float Unit = 1f;
            public bool UseUndo;
            // 프리팹 에셋을 고칠 때는 중첩 프리팹 안쪽을, 씬을 고칠 때는 프리팹 인스턴스 전체를 건너뛴다.
            public bool PrefabPass;
            public readonly Dictionary<Object, List<(Component owner, string path, string type)>> References = new();
            public int Changed;
        }

        private static TMP_FontAsset mediumFont;
        private static TMP_FontAsset semiBoldFont;
        private static Font mediumLegacyFont;
        private static Font semiBoldLegacyFont;

        [MenuItem("Tools/UI/Style/1. Apply To UI Prefabs")]
        private static void ApplyToPrefabsMenu()
        {
            if (LoadFonts() == false) return;
            int changed = ApplyToPrefabs();
            Debug.Log($"[{nameof(UIStyleUnifier)}] UI 프리팹 {changed}곳 변경 완료.");
        }

        [MenuItem("Tools/UI/Style/2. Apply To Open Scene")]
        private static void ApplyToOpenSceneMenu()
        {
            if (LoadFonts() == false) return;
            Scene scene = SceneManager.GetActiveScene();
            int changed = ApplyToScene(scene, true);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[{nameof(UIStyleUnifier)}] {scene.name}: {changed}곳 변경. 확인 후 씬을 저장하세요.");
        }

        [MenuItem("Tools/UI/Style/3. Apply To UI Prefabs And All Final Scenes")]
        private static void ApplyToAllMenu()
        {
            if (LoadFonts() == false) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;

            int changed = ApplyToPrefabs();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { FinalSceneFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int count = ApplyToScene(scene, false);
                    changed += count;
                    if (count == 0) continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[{nameof(UIStyleUnifier)}] {scene.name}: {count}곳 변경 후 저장.");
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            Debug.Log($"[{nameof(UIStyleUnifier)}] 전체 {changed}곳 변경 완료.");
        }

        private static bool LoadFonts()
        {
            mediumFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + "Pretendard-Medium SDF.asset");
            semiBoldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + "Pretendard-SemiBold SDF.asset");
            mediumLegacyFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "Pretendard-Medium.ttf");
            semiBoldLegacyFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "Pretendard-SemiBold.ttf");
            if (mediumFont != null && semiBoldFont != null && mediumLegacyFont != null && semiBoldLegacyFont != null) return true;

            Debug.LogError($"[{nameof(UIStyleUnifier)}] {FontFolder} 에서 Pretendard 글꼴을 찾지 못했습니다.");
            return false;
        }

        private static int ApplyToPrefabs()
        {
            int changed = 0;
            foreach ((string path, float referenceHeight, bool popIn) in Prefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[{nameof(UIStyleUnifier)}] {path} 가 없어 건너뜁니다.");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Context context = new() { Unit = referenceHeight / 1080f, PrefabPass = true };
                    // 프리팹 편집 환경에는 화면이 없어 루트 캔버스 크기가 0이다. 꽉 채운 자식의 크기를 잴 수 있게 기준 해상도로 맞춘다.
                    if (root.TryGetComponent(out CanvasScaler rootScaler) && root.transform is RectTransform rootRect)
                    {
                        rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;
                        rootRect.sizeDelta = rootScaler.referenceResolution;
                    }
                    CollectReferences(context, root.GetComponentsInChildren<Component>(true));
                    ApplyToHierarchy(root.transform, context);
                    if (popIn) context.Changed += AddPopIn(root, context);
                    if (context.Changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed += context.Changed;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return changed;
        }

        private static int ApplyToScene(Scene scene, bool useUndo)
        {
            int changed = ReplaceNestedHUD(scene, useUndo);

            List<Component> sceneComponents = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                sceneComponents.AddRange(root.GetComponentsInChildren<Component>(true));

            Context context = new() { UseUndo = useUndo };
            CollectReferences(context, sceneComponents);

            foreach (Component component in sceneComponents)
            {
                if (component == null || component is not Canvas canvas || RootCanvas(canvas.transform) != canvas) continue;
                // 프리팹 인스턴스는 1번 메뉴에서 에셋을 고치고, 씬에서는 글꼴·색 오버라이드만 걷어 내 에셋 값을 따르게 한다.
                if (PrefabUtility.IsPartOfPrefabInstance(canvas))
                {
                    context.Changed += RevertStyleOverrides(PrefabUtility.GetOutermostPrefabInstanceRoot(canvas));
                    continue;
                }

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                context.Unit = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                    ? scaler.referenceResolution.y / 1080f
                    : 1f;
                ApplyToHierarchy(canvas.transform, context);
            }

            // 캔버스 밖의 3D 글자(로비 NPC 머리 위 이름 등)
            foreach (Component component in sceneComponents)
                if (component != null && component is TMP_Text worldText && RootCanvas(worldText.transform) == null &&
                    PrefabUtility.IsPartOfPrefabInstance(worldText) == false)
                {
                    context.Unit = 1f;
                    ApplyText(worldText, context);
                }

            // 캔버스 밖 컴포넌트(보상창 매니저 등)가 들고 있는 글꼴 필드
            foreach (Component component in sceneComponents)
                if (component != null && component is MonoBehaviour behaviour && PrefabUtility.IsPartOfPrefabInstance(behaviour) == false)
                    ApplyFontFields(behaviour, context);

            return changed + context.Changed;
        }

        private static void ApplyToHierarchy(Transform root, Context context)
        {
            foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                ApplyScaler(scaler, context);

            StretchFullScreenRects(root, context);
            // 창 크기를 먼저 정해야 아래 Image 교체에서 전체 화면 막이 아니라 창으로 판단된다.
            if (context.Unit > 1f) PrepareFramedWindows(root, context);

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (ShouldSkip(component, root, context)) continue;

                switch (component)
                {
                    case TMP_Text text: ApplyText(text, context); break;
                    case Text text: ApplyLegacyText(text, context); break;
                    case MonoBehaviour behaviour:
                        ApplyFontFields(behaviour, context);
                        ApplyCodeColors(behaviour, context);
                        break;
                }
            }

            // Image 교체는 부모부터 처리해야 중첩 여부(RaisedTrack)를 알 수 있다. GetComponentsInChildren은 부모 먼저다.
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image.GetType() != typeof(Image)) continue;
                if (ShouldSkip(image, root, context)) continue;
                ApplyImage(image, root, context);
            }

            foreach (VectorPanel panel in root.GetComponentsInChildren<VectorPanel>(true))
            {
                if (panel == null || ShouldSkip(panel, root, context) || KeepsHUDLook(panel)) continue;
                StylePanel(panel, root, context);
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable == null || ShouldSkip(selectable, root, context) || IsReferenceStyle(selectable)) continue;
                ResizeButton(selectable, context);
            }

            foreach (LayoutElement element in root.GetComponentsInChildren<LayoutElement>(true))
            {
                if (element == null || ShouldSkip(element, root, context) || IsReferenceStyle(element)) continue;
                ResizeRow(element, context);
            }

            if (context.Unit > 1f) ApplyLayouts(root, context);
        }

        // HUD 프리팹을 따라 이미 다듬어진 UI. 글꼴·색 통일만 하고 모양·크기는 그대로 둔다.
        private static bool IsReferenceStyle(Component component) =>
            component.GetComponentInParent<DeathScreen>(true) != null ||
            component.GetComponentInParent<BossRewardPanel>(true) != null ||
            component.GetComponentInParent<TooltipView>(true) != null ||
            component.GetComponentInParent<BossIntroTitle>(true) != null ||
            component.GetComponentInParent<PlayerHUD>(true) != null ||
            component.GetComponentInParent<SkillSlotHUD>(true) != null;

        // 미니멀 리디자인에서도 HUD 생김새를 그대로 두는 UI. 보상창·툴팁은 창이므로 새 스타일을 따른다.
        private static bool KeepsHUDLook(Component component) =>
            component.GetComponentInParent<DeathScreen>(true) != null ||
            component.GetComponentInParent<BossIntroTitle>(true) != null ||
            component.GetComponentInParent<PlayerHUD>(true) != null ||
            component.GetComponentInParent<SkillSlotHUD>(true) != null;

        // 중첩 프리팹 안쪽은 그 프리팹 에셋에서 따로 처리한다.
        private static bool ShouldSkip(Component component, Transform root, Context context)
        {
            if (context.PrefabPass == false) return PrefabUtility.IsPartOfPrefabInstance(component);
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(component);
            return instanceRoot != null && instanceRoot.transform != root;
        }

        #region Resolution

        private static void ApplyScaler(CanvasScaler scaler, Context context)
        {
            // 하위 캔버스의 스케일러는 쓰이지 않는다.
            Transform parent = scaler.transform.parent;
            if (parent != null && parent.GetComponentInParent<Canvas>(true) != null) return;
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand) return;

            Record(scaler, context);
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Changed(scaler, context);
        }

        // 캔버스 바로 아래에서 기준 해상도 크기로 고정된 오브젝트(페이드 화면 등)는 화면 비율이 달라지면 가장자리가 비므로 꽉 채운다.
        private static void StretchFullScreenRects(Transform root, Context context)
        {
            CanvasScaler scaler = root.GetComponentInParent<CanvasScaler>(true);
            if (scaler == null) return;
            Vector2 reference = scaler.referenceResolution;

            foreach (Transform child in root)
            {
                if (child is not RectTransform rect) continue;
                if (rect.anchorMin != rect.anchorMax) continue;
                if (Mathf.Abs(rect.sizeDelta.x - reference.x) > 1f || Mathf.Abs(rect.sizeDelta.y - reference.y) > 1f) continue;

                Record(rect, context);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                Changed(rect, context);
            }
        }

        // 다른 캔버스 안에 복사돼 있는 HUD를 HUD 프리팹 인스턴스로 바꾼다. 하위 캔버스는 부모 캔버스 배율을 따르므로
        // 1920x1080 기준인 HUD 프리팹은 별도 최상위 캔버스로 둬야 한다.
        private static int ReplaceNestedHUD(Scene scene, bool useUndo)
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HUDPrefabPath);
            if (hudPrefab == null) return 0;

            int replaced = 0;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (PlayerHUD playerHUD in sceneRoot.GetComponentsInChildren<PlayerHUD>(true))
            {
                if (playerHUD == null) continue;
                Canvas rootCanvas = RootCanvas(playerHUD.transform);
                if (rootCanvas == null || playerHUD.transform.parent == rootCanvas.transform) continue;
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(rootCanvas.gameObject) == hudPrefab) continue;

                Transform container = playerHUD.transform;
                while (container.parent != rootCanvas.transform) container = container.parent;

                GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
                if (useUndo) Undo.RegisterCreatedObjectUndo(hud, "Replace Nested HUD");

                int restored = CopyReferences(container, hud.transform);
                restored += RemapExternalReferences(scene, container, hud.transform);

                // 로비 창들은 HUD 위에 그려져야 한다. 원래는 같은 캔버스 안에서 HUD가 먼저(아래) 그려졌다.
                Canvas hudCanvas = hud.GetComponent<Canvas>();
                if (rootCanvas.sortingOrder <= hudCanvas.sortingOrder)
                {
                    if (useUndo) Undo.RecordObject(rootCanvas, "Raise UI Canvas");
                    rootCanvas.sortingOrder = hudCanvas.sortingOrder + 10;
                    if (PrefabUtility.IsPartOfPrefabInstance(rootCanvas))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(rootCanvas);
                }

                string containerPath = $"{rootCanvas.name}/{container.name}";
                GameObject old = container.gameObject;
                try
                {
                    // 프리팹 인스턴스 안쪽이면 '제거된 오브젝트' 오버라이드로 남는다.
                    Undo.DestroyObjectImmediate(old);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[{nameof(UIStyleUnifier)}] {containerPath} 를 지우지 못해 꺼 둡니다. ({exception.Message})");
                }
                if (old != null)
                {
                    Undo.RecordObject(old, "Disable Nested HUD");
                    old.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(old);
                }

                Debug.Log($"[{nameof(UIStyleUnifier)}] {scene.name}: {containerPath} 를 HUD 프리팹으로 교체 (참조 {restored}개 이전).", hud);
                replaced++;
                break;
            }
            return replaced;
        }

        // 옛 HUD 컴포넌트가 들고 있던 씬 오브젝트 참조(플레이어 등)를 같은 이름·타입의 새 컴포넌트에 옮긴다.
        private static int CopyReferences(Transform from, Transform to)
        {
            int copied = 0;
            foreach (MonoBehaviour behaviour in from.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                Component target = FindMatch(behaviour, to);
                if (target == null) continue;

                SerializedObject source = new(behaviour);
                SerializedObject destination = new(target);
                SerializedProperty property = source.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value) || IsInside(value, from)) continue;

                    SerializedProperty targetProperty = destination.FindProperty(property.propertyPath);
                    if (targetProperty == null || targetProperty.propertyType != SerializedPropertyType.ObjectReference) continue;
                    targetProperty.objectReferenceValue = value;
                    copied++;
                }
                destination.ApplyModifiedProperties();
            }
            return copied;
        }

        // 씬의 다른 오브젝트가 옛 HUD 안쪽을 가리키면 새 HUD의 같은 오브젝트로 바꾼다.
        private static int RemapExternalReferences(Scene scene, Transform from, Transform to)
        {
            int remapped = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.transform.IsChildOf(from) || behaviour.transform.IsChildOf(to)) continue;

                SerializedObject serialized = new(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool dirty = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    if (value == null || IsInside(value, from) == false) continue;

                    Object match = value switch
                    {
                        GameObject gameObject => FindMatch(gameObject.transform, to)?.gameObject,
                        Component component => FindMatch(component, to),
                        _ => null
                    };
                    property.objectReferenceValue = match;
                    dirty = true;
                    remapped++;
                }
                if (dirty) serialized.ApplyModifiedProperties();
            }
            return remapped;
        }

        // 같은 타입·같은 이름의 컴포넌트, 여러 개면 부모 이름까지 같은 것
        private static Component FindMatch(Component source, Transform root)
        {
            Component fallback = null;
            foreach (Component candidate in root.GetComponentsInChildren(source.GetType(), true))
            {
                if (candidate.name != source.name) continue;
                Transform parent = candidate.transform.parent;
                Transform sourceParent = source.transform.parent;
                if (parent != null && sourceParent != null && parent.name == sourceParent.name) return candidate;
                fallback ??= candidate;
            }
            return fallback;
        }

        #endregion

        #region Text

        private static void ApplyText(TMP_Text text, Context context)
        {
            TMP_FontAsset font = text.font;
            bool logo = LogoTexts.Contains(text.text?.Trim() ?? string.Empty);
            bool isPretendard = font != null && font.name.StartsWith("Pretendard", StringComparison.Ordinal);
            Color color = MapTextColor(text.color);

            float size = logo ? text.fontSize : ScaledFontSize(text.fontSize, text, context);
            bool title = (text.fontStyle & FontStyles.Bold) != 0 || size / context.Unit >= TitleFontSize;
            // 줄인 글자는 크기에 맞는 굵기로 다시 고른다.
            bool resized = Mathf.Approximately(size, text.fontSize) == false;
            TMP_FontAsset weight = resized ? (size / context.Unit >= 22f ? semiBoldFont : mediumFont) : title ? semiBoldFont : mediumFont;

            bool changeFont = logo == false && (isPretendard == false || (resized && font != weight));
            bool changeColor = color != text.color;
            if (changeFont == false && changeColor == false && resized == false) return;

            Record(text, context);
            if (changeFont)
            {
                text.font = weight;
                text.fontSharedMaterial = weight.material;
                text.fontStyle &= ~FontStyles.Bold;
            }
            if (resized)
            {
                text.fontSize = size;
                // 제목급 글자는 자간을 조금 벌려 단단해 보이지 않게 한다.
                text.characterSpacing = size / context.Unit >= 22f ? 2f : 0f;
            }
            text.color = color;
            Changed(text, context);
        }

        // 4K 기준 팀 UI의 큰 글자를 HUD 비율의 크기 단계로 줄인다. 이미 단계 크기면 그대로 두어 여러 번 실행해도 같다.
        private static float ScaledFontSize(float size, Component text, Context context)
        {
            if (context.Unit <= 1f || IsReferenceStyle(text)) return size;
            float pixels = size / context.Unit;
            foreach ((float _, float _, float step) in TypeScale)
                if (Mathf.Approximately(pixels, step)) return size;
            foreach ((float from, float to) in PreviousTypeScale)
                if (Mathf.Approximately(pixels, from)) return to * context.Unit;
            foreach ((float min, float max, float step) in TypeScale)
                if (pixels >= min && pixels < max) return step * context.Unit;
            return size;
        }

        private static void ApplyLegacyText(Text text, Context context)
        {
            int size = Mathf.RoundToInt(ScaledFontSize(text.fontSize, text, context));
            bool title = text.fontStyle == FontStyle.Bold || size / context.Unit >= 22f;
            Font font = title ? semiBoldLegacyFont : mediumLegacyFont;
            Color color = MapTextColor(text.color);
            if (text.font == font && text.color == color && text.fontSize == size) return;

            Record(text, context);
            text.font = font;
            text.fontSize = size;
            if (text.fontStyle == FontStyle.Bold) text.fontStyle = FontStyle.Normal;
            text.color = color;
            Changed(text, context);
        }

        // 무채색은 HUD 글자색(어두운 회색은 흐린 보조 글자), 유채색은 가까운 HUD 강조색으로. 알파는 원래 값을 따른다.
        private static Color MapTextColor(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            // 검은 글자는 밝은 바탕(칩 등) 위에 쓴 것이라 그대로 둔다.
            if (value < 0.35f) return color;

            Color mapped;
            if (saturation < 0.3f)
            {
                mapped = Palette.Text;
                mapped.a = value < 0.7f ? color.a * 0.55f : color.a;
                return mapped;
            }

            mapped = AccentFor(hue);
            mapped.a = color.a;
            return mapped;
        }

        private static Color AccentFor(float hue)
        {
            if (hue < 0.04f || hue > 0.92f) return Palette.Danger;
            if (hue < 0.2f) return Palette.Gold;
            if (hue < 0.45f) return Palette.Positive;
            return Palette.Text;
        }

        // 컴포넌트가 들고 있는 글꼴 필드(런타임에 글자를 만드는 증강칩 패널 등)
        private static void ApplyFontFields(MonoBehaviour behaviour, Context context)
        {
            if (behaviour == null || behaviour is Graphic) return;
            // 외부 에셋 컴포넌트는 건드리지 않는다.
            if (behaviour.GetType().Namespace?.StartsWith("TeamOdd", StringComparison.Ordinal) != true) return;

            SerializedObject serialized = new(behaviour);
            SerializedProperty property = serialized.GetIterator();
            bool dirty = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                string type = ReferenceType(property);
                if (type == nameof(Font))
                {
                    if (property.objectReferenceValue is Font font && font.name.StartsWith("Pretendard", StringComparison.Ordinal)) continue;
                    property.objectReferenceValue = mediumLegacyFont;
                    dirty = true;
                }
                else if (type == nameof(TMP_FontAsset))
                {
                    if (property.objectReferenceValue is TMP_FontAsset font && font.name.StartsWith("Pretendard", StringComparison.Ordinal)) continue;
                    // 비워 둔 TMP 글꼴은 각 스크립트가 기본값을 쓰도록 둔다.
                    if (property.objectReferenceValue == null) continue;
                    property.objectReferenceValue = mediumFont;
                    dirty = true;
                }
            }
            if (dirty == false) return;
            ApplySerialized(serialized, context);
        }

        private static void ApplyCodeColors(MonoBehaviour behaviour, Context context)
        {
            string typeName = behaviour.GetType().Name;
            SerializedObject serialized = null;
            foreach ((string type, string field, Color value) in CodeColors)
            {
                if (type != typeName) continue;
                serialized ??= new SerializedObject(behaviour);
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null || property.propertyType != SerializedPropertyType.Color || property.colorValue == value) continue;
                property.colorValue = value;
            }
            if (serialized != null && serialized.hasModifiedProperties) ApplySerialized(serialized, context);
        }

        #endregion

        #region Panel

        private static void ApplyImage(Image image, Transform root, Context context)
        {
            Color color = image.color;
            if (color.a <= 0.01f) return;
            // 그림이 들어간 이미지(아이콘, 비네트, 체크 표시 등)는 그대로 둔다.
            if (image.sprite != null && IsBuiltinSprite(image.sprite) == false) return;
            // 토글의 체크 표시는 켜고 끌 때 투명도로 보이고 숨으므로 패널로 바꾸지 않는다.
            Toggle toggle = image.GetComponentInParent<Toggle>(true);
            if (toggle != null && toggle.graphic == image) return;

            Scrollbar scrollbar = image.GetComponentInParent<Scrollbar>(true);
            if (scrollbar != null)
            {
                ApplyScrollbar(image, scrollbar, context);
                return;
            }

            Color.RGBToHSV(color, out _, out float saturation, out float value);
            // 검은 막(페이드·암전·뷰포트 마스크)은 그대로 둔다. 이 툴이 칠한 창 뒤 막(Dim)은 다시 다듬는다.
            bool ownDim = Mathf.Abs(color.r - Palette.Dim.r) < 0.01f && Mathf.Abs(color.g - Palette.Dim.g) < 0.01f &&
                          Mathf.Abs(color.b - Palette.Dim.b) < 0.01f;
            if (value < 0.06f && ownDim == false) return;
            // 흰색은 런타임에 아이콘을 넣는 자리이거나 코드가 색을 정하는 것이라 그대로 둔다.
            if (saturation < 0.1f && value > 0.9f) return;

            RectTransform rect = image.rectTransform;
            Vector2 size = EffectiveSize(rect, context);
            // 크기가 0인 것은 코드가 늘려 가며 보여 주는 연출 선(UILineReveal 등)이다.
            if (size.x <= 0f || size.y <= 0f) return;
            float thin = 8f * context.Unit;

            if (Mathf.Min(size.x, size.y) > 0f && Mathf.Min(size.x, size.y) <= thin && Mathf.Max(size.x, size.y) >= thin * 4f)
            {
                SetImageColor(image, Palette.Line, context);
                ThinLine(rect, size, context);
                return;
            }

            if (IsFullScreen(rect, root, context))
            {
                Color dim = Palette.Dim;
                // 창 뒤 막은 게임 화면이 비치도록 조금 더 투명하게, 완전히 불투명한 배경(타이틀 화면 등)은 그대로 둔다.
                dim.a = color.a >= 0.995f || IsReferenceStyle(image) ? color.a : Mathf.Min(color.a, Palette.BackdropAlpha);
                SetImageColor(image, dim, context);
                if (IsToggled(image.transform, root) && IsReferenceStyle(image) == false) context.Changed += AddPopIn(image.gameObject, context);
                return;
            }

            bool selectable = image.GetComponent<Selectable>() != null;
            // 색으로 상태를 나타내던 작은 표시(완료 체크 등)는 HUD 강조색으로
            if (selectable == false && saturation > 0.35f && Mathf.Max(size.x, size.y) <= 160f * context.Unit)
            {
                Color.RGBToHSV(color, out float hue, out _, out _);
                SetImageColor(image, AccentFor(hue), context);
                return;
            }

            bool nested = image.transform.parent != null && image.transform.parent.GetComponentInParent<VectorPanel>(true) != null;
            Color track = nested ? Palette.RaisedTrack : Palette.Track;

            if (CanSwap(image, context))
                SwapToVectorPanel(image, track, nested, selectable, context);
            else
                ApplyImageFallback(image, track, context);
        }

        // 레이아웃 그룹이 크기를 정하는 행(목록 항목 등)은 저장된 크기가 0이므로 LayoutElement 값을 쓰고, 그것도 없으면 큰 값으로 본다.
        private static Vector2 EffectiveSize(RectTransform rect, Context context)
        {
            Vector2 size = rect.rect.size;
            bool driven = rect.GetComponent<LayoutElement>() != null ||
                          (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null);
            if (driven == false) return size;

            LayoutElement element = rect.GetComponent<LayoutElement>();
            float unknown = 1000f * context.Unit;
            if (size.x <= 0f) size.x = element != null ? Mathf.Max(element.preferredWidth, element.minWidth) : 0f;
            if (size.y <= 0f) size.y = element != null ? Mathf.Max(element.preferredHeight, element.minHeight) : 0f;
            if (size.x <= 0f) size.x = unknown;
            if (size.y <= 0f) size.y = unknown;
            return size;
        }

        private static bool IsFullScreen(RectTransform rect, Transform root, Context context)
        {
            if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one) return false;
            if (rect.parent == root && root.GetComponent<Canvas>() != null) return true;

            CanvasScaler scaler = root.GetComponentInParent<CanvasScaler>(true);
            Vector2 reference = scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f) * context.Unit;
            Vector2 size = rect.rect.size;
            return size.x >= reference.x * 0.9f && size.y >= reference.y * 0.9f;
        }

        private static bool IsBuiltinSprite(Sprite sprite) =>
            AssetDatabase.GetAssetPath(sprite) == "Resources/unity_builtin_extra";

        private static void SetImageColor(Image image, Color color, Context context)
        {
            if (image.color == color) return;
            Record(image, context);
            image.color = color;
            Changed(image, context);
        }

        // 다른 컴포넌트가 Graphic 계열 타입으로만 참조하고, Image를 요구하는 컴포넌트가 없을 때만 바꾼다.
        private static bool CanSwap(Image image, Context context)
        {
            if (image.type != Image.Type.Simple && image.type != Image.Type.Sliced) return false;
            if (image.GetComponent<Mask>() != null) return false;

            foreach (Component component in image.GetComponents<Component>())
            {
                if (component == null || component == image) continue;
                foreach (RequireComponent require in component.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    if (IsGraphicType(require.m_Type0) || IsGraphicType(require.m_Type1) || IsGraphicType(require.m_Type2))
                        return false;
            }

            if (context.References.TryGetValue(image, out List<(Component owner, string path, string type)> references) == false) return true;
            foreach ((Component owner, string path, string type) in references)
            {
                if (owner == null) continue;
                if (path.Contains("m_PersistentCalls")) return false;
                if (GraphicCompatibleTypes.Contains(type) == false) return false;
            }
            return true;
        }

        private static bool IsGraphicType(Type type) => type != null && typeof(Graphic).IsAssignableFrom(type);

        private static void SwapToVectorPanel(Image image, Color track, bool nested, bool selectable, Context context)
        {
            GameObject gameObject = image.gameObject;
            bool raycastTarget = image.raycastTarget;
            bool maskable = image.maskable;
            context.References.TryGetValue(image, out List<(Component owner, string path, string type)> references);

            // 테두리는 VectorPanel이 그리므로 기존 Outline 효과는 뺀다.
            foreach (Outline outline in gameObject.GetComponents<Outline>()) Destroy(outline, context);
            Destroy(image, context);
            if (image != null) return;

            VectorPanel panel = context.UseUndo ? Undo.AddComponent<VectorPanel>(gameObject) : gameObject.AddComponent<VectorPanel>();
            panel.raycastTarget = raycastTarget;
            panel.maskable = maskable;
            panel.color = Color.white;

            // 역할별 모양은 StylePanel이 정한다. 여기서는 기본 HUD 값만 넣어 둔다.
            SerializedObject serialized = new(panel);
            serialized.FindProperty("trackColor").colorValue = track;
            serialized.FindProperty("borderColor").colorValue = Palette.Border;
            serialized.FindProperty("shadowColor").colorValue = Palette.Shadow;
            serialized.FindProperty("borderWidth").floatValue = 1f * context.Unit;
            serialized.FindProperty("shadowSize").floatValue = (nested ? 0f : 3f) * context.Unit;
            serialized.FindProperty("highlightOnHover").boolValue = selectable;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (references != null)
            {
                foreach ((Component owner, string path, string _) in references)
                {
                    if (owner == null) continue;
                    SerializedObject ownerSerialized = new(owner);
                    SerializedProperty property = ownerSerialized.FindProperty(path);
                    if (property == null) continue;
                    property.objectReferenceValue = panel;
                    ApplySerialized(ownerSerialized, context);
                }
            }

            // 패널이 마우스 반응으로 테두리가 밝아지므로 버튼 색 전환은 눌림·비활성만 남긴다.
            foreach (Selectable target in gameObject.GetComponents<Selectable>())
                ApplySelectableColors(target, Color.white, context);

            context.References.Remove(image);
            Changed(panel, context);
        }

        private static void ApplyImageFallback(Image image, Color track, Context context)
        {
            SetImageColor(image, track, context);
            foreach (Outline outline in image.GetComponents<Outline>())
            {
                Vector2 distance = new(context.Unit, -context.Unit);
                if (outline.effectColor == Palette.Border && outline.effectDistance == distance) continue;
                Record(outline, context);
                outline.effectColor = Palette.Border;
                outline.effectDistance = distance;
                Changed(outline, context);
            }
            // Image는 테두리 반응이 없으므로 평소에 조금 어둡게 두고 올렸을 때 밝힌다.
            foreach (Selectable target in image.GetComponents<Selectable>())
                ApplySelectableColors(target, new Color(0.8f, 0.8f, 0.8f, 1f), context);
        }

        private static void ApplySelectableColors(Selectable selectable, Color normal, Context context)
        {
            Record(selectable, context);
            selectable.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = selectable.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.white;
            colors.selectedColor = normal;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            selectable.colors = colors;
            Changed(selectable, context);
        }

        #endregion

        #region Polish

        private static PanelRole RoleOf(VectorPanel panel, Transform root)
        {
            Transform transform = panel.transform;
            Selectable selectable = panel.GetComponent<Selectable>();
            // 토글 체크 상자처럼 부모가 버튼인 바탕도 버튼으로 본다.
            if (selectable == null && transform.parent != null) selectable = transform.parent.GetComponent<Selectable>();
            if (selectable != null)
            {
                foreach (string name in PrimaryButtonNames)
                    if (selectable.name.Contains(name, StringComparison.Ordinal)) return PanelRole.PrimaryButton;
                return PanelRole.Button;
            }

            // 시작 화면 버튼 뒤의 큰 판은 버튼만 남기려고 바탕 없이 둔다.
            if (panel.name.Contains("TitleBar", StringComparison.Ordinal) || panel.name.Contains("Header", StringComparison.Ordinal) ||
                panel.name == "MenuPanel")
                return PanelRole.Header;

            bool nested = transform != root && transform.parent != null &&
                          (transform.parent.GetComponentInParent<VectorPanel>(true) != null ||
                           panel.GetComponent<LayoutElement>() != null ||
                           transform.parent.GetComponent<LayoutGroup>() != null);
            return nested ? PanelRole.Inset : PanelRole.Window;
        }

        private static void StylePanel(VectorPanel panel, Transform root, Context context)
        {
            PanelRole role = RoleOf(panel, root);
            SerializedObject serialized = new(panel);
            // 보상 아이콘처럼 버튼이 아니어도 올리면 반응하던 칸은 버튼 모양으로 둔다.
            SerializedProperty highlight = serialized.FindProperty("highlightOnHover");
            if (role == PanelRole.Inset && highlight != null && highlight.boolValue) role = PanelRole.Button;

            Color track, border, hoverTrack = Palette.Clear, hoverBorder = Palette.TextAlpha(0.85f), shadowColor = Palette.Shadow;
            float shadow = 0f;
            bool hover = false;
            switch (role)
            {
                case PanelRole.Window:
                    track = Palette.WindowTrack;
                    border = Palette.TextAlpha(0.16f);
                    shadowColor = Palette.WindowShadow;
                    shadow = 18f;
                    break;
                case PanelRole.Header:
                    track = Palette.Clear;
                    border = Palette.Clear;
                    break;
                case PanelRole.Inset:
                    track = Palette.TextAlpha(0.025f);
                    border = Palette.Clear;
                    break;
                case PanelRole.PrimaryButton:
                    track = Palette.TextAlpha(0.92f);
                    border = Palette.TextAlpha(0.92f);
                    hoverTrack = new Color(1f, 1f, 1f, 0.12f);
                    hoverBorder = Color.white;
                    hover = true;
                    break;
                default:
                    track = Palette.Clear;
                    border = Palette.TextAlpha(0.22f);
                    hoverTrack = Palette.TextAlpha(0.07f);
                    hover = true;
                    break;
            }

            SetColor(serialized, "trackColor", track);
            SetColor(serialized, "borderColor", border);
            SetColor(serialized, "shadowColor", shadowColor);
            SetColor(serialized, "highlightTrackColor", hoverTrack);
            SetColor(serialized, "highlightBorderColor", hoverBorder);
            SetFloat(serialized, "borderWidth", 1f * context.Unit);
            SetFloat(serialized, "shadowSize", shadow * context.Unit);
            if (highlight != null) highlight.boolValue = hover;
            if (serialized.hasModifiedProperties) ApplySerialized(serialized, context);

            // 밝게 채운 주 버튼 위 글자는 어둡게 뒤집는다. 어두운 글자는 글자색 정리에서 그대로 두므로 다시 실행해도 같다.
            if (role == PanelRole.PrimaryButton)
                foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.color == Palette.InverseText) continue;
                    Record(label, context);
                    label.color = Palette.InverseText;
                    Changed(label, context);
                }

            // 켜졌다 꺼지는 팝업 창. 툴팁·보상창은 자체 연출이 있어 붙이지 않는다.
            if (role == PanelRole.Window && IsToggled(panel.transform, root) && panel.transform != root && IsReferenceStyle(panel) == false)
                context.Changed += AddPopIn(panel.gameObject, context);
        }

        private static void SetColor(SerializedObject serialized, string name, Color value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null && property.colorValue != value) property.colorValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string name, float value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null && Mathf.Approximately(property.floatValue, value) == false) property.floatValue = value;
        }

        // 자신이나 루트까지의 부모 중 꺼진 것이 있으면 코드가 켜는 창으로 본다.
        private static bool IsToggled(Transform transform, Transform root)
        {
            for (Transform current = transform; current != null && current != root; current = current.parent)
                if (current.gameObject.activeSelf == false) return true;
            return false;
        }

        // 열림 연출은 창 하나에 한 번만. 바깥 창에 이미 있으면 안쪽 패널에는 붙이지 않고, 예전에 붙은 것도 뗀다.
        private static int AddPopIn(GameObject target, Context context)
        {
            Transform parent = target.transform.parent;
            if (parent != null && parent.GetComponentInParent<UIPopIn>(true) != null)
            {
                UIPopIn nested = target.GetComponent<UIPopIn>();
                if (nested == null) return 0;
                Destroy(nested, context);
                return 1;
            }
            int removed = 0;
            foreach (UIPopIn inner in target.GetComponentsInChildren<UIPopIn>(true))
            {
                if (inner.gameObject == target) continue;
                Destroy(inner, context);
                removed++;
            }
            if (target.GetComponent<UIPopIn>() != null || target.GetComponent<UIStaggerReveal>() != null ||
                target.GetComponentInParent<UILineReveal>(true) != null) return removed;
            if (context.UseUndo) Undo.AddComponent<UIPopIn>(target);
            else target.AddComponent<UIPopIn>();
            return removed + 1;
        }

        // 1080p 기준 60px이 넘는 큰 버튼은 HUD 보상창 확인 버튼 비율로 줄인다. 위치는 기준점(pivot)을 중심으로 유지된다.
        private static void ResizeButton(Selectable selectable, Context context)
        {
            if (selectable is Scrollbar || selectable is Slider) return;
            if (selectable.transform is not RectTransform rect || rect.anchorMin.y != rect.anchorMax.y) return;
            Vector2 size = rect.sizeDelta;
            float height = size.y / context.Unit;
            bool previous = context.Unit > 1f && Mathf.Approximately(height, PreviousButtonHeight);
            if (height <= 60f && previous == false) return;

            Record(rect, context);
            bool square = Mathf.Abs(size.x - size.y) < 1f;
            float width = square ? SquareButtonWidth * context.Unit : Mathf.Min(size.x, Mathf.Max(size.x * 0.6f, 240f * context.Unit));
            if (rect.anchorMin.x != rect.anchorMax.x || previous) width = size.x;
            rect.sizeDelta = new Vector2(width, ButtonHeight * context.Unit);
            Changed(rect, context);
        }

        // 1080p 기준 80px이 넘는 목록 행은 줄인다.
        private static void ResizeRow(LayoutElement element, Context context)
        {
            float rowHeight = RowHeight * context.Unit;
            bool tall = element.preferredHeight / context.Unit > 80f || element.minHeight / context.Unit > 80f;
            bool previous = context.Unit > 1f && (Mathf.Approximately(element.preferredHeight / context.Unit, PreviousRowHeight) ||
                                                  Mathf.Approximately(element.minHeight / context.Unit, PreviousRowHeight));
            if (tall == false && previous == false) return;

            Record(element, context);
            if (element.preferredHeight > rowHeight || previous) element.preferredHeight = rowHeight;
            if (element.minHeight > rowHeight || (previous && element.minHeight > 0f)) element.minHeight = rowHeight;
            Changed(element, context);
        }

        // 구분선은 1px 굵기로
        private static void ThinLine(RectTransform rect, Vector2 size, Context context)
        {
            if (IsReferenceStyle(rect)) return;
            float thickness = context.Unit;
            bool horizontal = size.x >= size.y;
            Vector2 delta = rect.sizeDelta;
            if (horizontal && rect.anchorMin.y == rect.anchorMax.y && Mathf.Approximately(delta.y, thickness) == false) delta.y = thickness;
            else if (horizontal == false && rect.anchorMin.x == rect.anchorMax.x && Mathf.Approximately(delta.x, thickness) == false) delta.x = thickness;
            else return;

            Record(rect, context);
            rect.sizeDelta = delta;
            Changed(rect, context);
        }

        // 스크롤바는 옅은 홈과 가는 손잡이로, 세로 스크롤바는 너비도 줄인다.
        private static void ApplyScrollbar(Image image, Scrollbar scrollbar, Context context)
        {
            bool handle = scrollbar.handleRect != null && image.transform == scrollbar.handleRect;
            SetImageColor(image, handle ? Palette.ScrollHandle : Palette.ScrollTrack, context);
            if (handle || image.transform != scrollbar.transform || IsReferenceStyle(scrollbar)) return;

            if (scrollbar.transform is not RectTransform rect) return;
            bool vertical = scrollbar.direction is Scrollbar.Direction.BottomToTop or Scrollbar.Direction.TopToBottom;
            float width = ScrollbarWidth * context.Unit;
            Vector2 delta = rect.sizeDelta;
            if (vertical && rect.anchorMin.x == rect.anchorMax.x && Mathf.Approximately(delta.x, width) == false) delta.x = width;
            else if (vertical == false && rect.anchorMin.y == rect.anchorMax.y && Mathf.Approximately(delta.y, width) == false) delta.y = width;
            else return;

            Record(rect, context);
            rect.sizeDelta = delta;
            Changed(rect, context);

            // 스크롤바 자리로 비워 두던 뷰포트 여백을 새 너비에 맞춰 줄인다.
            ScrollRect scrollRect = scrollbar.GetComponentInParent<ScrollRect>(true);
            if (vertical && scrollRect != null && scrollRect.verticalScrollbar == scrollbar && scrollRect.viewport != null)
            {
                RectTransform viewport = scrollRect.viewport;
                float margin = -(width + 8f * context.Unit);
                if (viewport.anchorMin.x != viewport.anchorMax.x && viewport.offsetMax.x < margin)
                {
                    Record(viewport, context);
                    viewport.offsetMax = new Vector2(margin, viewport.offsetMax.y);
                    Changed(viewport, context);
                }
            }

            // 손잡이 영역의 안쪽 여백을 없애 손잡이가 너비를 다 쓰게 한다.
            if (scrollbar.handleRect != null && scrollbar.handleRect.parent is RectTransform area && area != rect)
            {
                Record(area, context);
                area.sizeDelta = vertical ? new Vector2(0f, area.sizeDelta.y) : new Vector2(area.sizeDelta.x, 0f);
                Changed(area, context);
            }
        }

        #endregion

        #region Layout

        // 4K 기준 팀 UI 창들의 배치. 값은 1080p 기준 px이고 캔버스 단위로 환산해 넣는다.
        // - 전체 화면 창: 화면 가장자리에서 여백을 두고, 제목은 왼쪽 정렬 + 아래 1px 선, 닫기 버튼은 제목 줄 오른쪽 끝에 맞춘다.
        //   본문은 왼쪽 목록(고정 너비)과 오른쪽 상세로 나누고, 상세는 제목 → 그림 → 설명 → 오른쪽 아래 주 버튼 순서로 쌓는다.
        // - 팝업: 화면 가운데 고정 크기, 제목·설명은 왼쪽 위 정렬, 버튼은 오른쪽 아래에 한 줄로(주 버튼이 가장 오른쪽).
        // - 진행 미션 위젯: 화면 오른쪽 위 모서리에서 여백을 둔다.
        private const float WindowMarginX = 120f;
        private const float WindowMarginTop = 56f;
        private const float WindowMarginBottom = 72f;
        private const float WindowTitleHeight = 88f;
        private const float WindowTitleGap = 40f;
        private const float ListWidth = 560f;
        private const float ColumnGap = 40f;
        private const float PanelPadding = 40f;
        private const float DetailTitleHeight = 64f;
        private const float IllustrationHeight = 360f;
        private const float BlockGap = 28f;
        private const float DialogWidth = 680f;
        private const float DialogHeight = 340f;
        private const float DialogPadding = 44f;
        private const float DialogTitleHeight = 48f;
        private const float DialogButtonWidth = 168f;
        private const float ButtonGap = 12f;
        private const float WidgetMargin = 40f;
        private const float WidgetWidth = 420f;

        private static readonly string[] WindowNames = { "JournalWindow", "StageMoveWindow" };

        // 증강칩 패널처럼 화면 가운데 한 장의 창(반투명 바탕 + 밝은 외곽선)으로 띄우는 창. 전체 화면 막 대신 창 자체가 바탕이 된다.
        // 크기는 증강칩 패널(1578x909)과 비슷하게, 안쪽 여백·제목 줄도 증강칩 패널 값(1080p 기준)을 따른다.
        private static readonly string[] FramedWindowNames = { "JournalWindow" };
        private static readonly Vector2 FramedWindowSize = new(1520f, 880f);
        private const float FramedPadding = 36f;
        private const float FramedTitleTop = 24f;
        private const float FramedTitleHeight = 56f;
        private const float FramedTitleGap = 20f;
        private const float FramedListWidth = 480f;
        private const float FramedColumnGap = 28f;
        private static readonly string[] DialogNames = { "ClickSuccess UI", "ClickFail UI", "MoveFail UI" };

        private static void ApplyLayouts(Transform root, Context context)
        {
            foreach (string name in WindowNames)
                if (FindDeep(root, name) is RectTransform window) LayoutWindow(window, context);
            foreach (string name in DialogNames)
                if (FindDeep(root, name) is RectTransform dialog) LayoutDialog(dialog, context);
            // 로비 씬에서는 인스턴스 이름이 "Mission List UI"로 바뀌어 있다
            if ((FindDeep(root, "Quest List UI") ?? FindDeep(root, "Mission List UI")) is RectTransform widget)
                SetRect(widget, Vector2.one, Vector2.one, Vector2.one, new Vector2(-WidgetMargin - WidgetWidth, -WidgetMargin - widget.sizeDelta.y / context.Unit),
                    new Vector2(-WidgetMargin, -WidgetMargin), context);
            if (FindDeep(root, "Start UI") != null || root.name == "Start UI") LayoutStartMenu(root, context);
        }

        private static void PrepareFramedWindows(Transform root, Context context)
        {
            foreach (string name in FramedWindowNames)
                if (FindDeep(root, name) is RectTransform window)
                    SetRect(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        -FramedWindowSize * 0.5f, FramedWindowSize * 0.5f, context);
        }

        private static void LayoutWindow(RectTransform window, Context context)
        {
            bool framed = Array.IndexOf(FramedWindowNames, window.name) >= 0;
            float marginX = framed ? FramedPadding : WindowMarginX;
            float marginTop = framed ? FramedTitleTop : WindowMarginTop;
            float marginBottom = framed ? FramedPadding : WindowMarginBottom;
            float titleGap = framed ? FramedTitleGap : WindowTitleGap;
            float listWidth = framed ? FramedListWidth : ListWidth;
            float columnGap = framed ? FramedColumnGap : ColumnGap;
            float titleBottom = marginTop + (framed ? FramedTitleHeight : WindowTitleHeight);
            if (window.Find("TitleBar") is RectTransform titleBar)
            {
                SetRect(titleBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                    new Vector2(marginX, -titleBottom), new Vector2(-marginX, -marginTop), context);
                if (titleBar.Find("TitleText") is RectTransform titleText)
                {
                    SetRect(titleText, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-SquareButtonWidth - 24f, 0f), context);
                    SetAlignment(titleText, TextAlignmentOptions.MidlineLeft, context);
                }
                if (titleBar.Find("ExitButton") is RectTransform exit)
                    SetRect(exit, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-SquareButtonWidth, -ButtonHeight * 0.5f), new Vector2(0f, ButtonHeight * 0.5f), context);
            }

            if (window.Find("MissionPage") is not RectTransform page) return;
            SetRect(page, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(marginX, marginBottom), new Vector2(-marginX, -(titleBottom + titleGap)), context);

            foreach (RectTransform child in page)
            {
                if (child.name.EndsWith("ListPanel", StringComparison.Ordinal))
                {
                    SetRect(child, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(listWidth, 0f), context);
                    LayoutList(child, context);
                }
                else if (child.name == "DetailPanel")
                {
                    SetRect(child, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(listWidth + columnGap, 0f), Vector2.zero, context);
                    LayoutDetail(child, framed, context);
                }
            }
        }

        // 목록 패널: 머리글이 있으면 위에 두고 그 아래를 스크롤 영역으로, 안쪽 여백을 통일한다.
        private static void LayoutList(RectTransform list, Context context)
        {
            float top = 16f;
            foreach (RectTransform child in list)
            {
                if (child.name.Contains("Header", StringComparison.Ordinal))
                {
                    SetRect(child, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                        new Vector2(PanelPadding * 0.5f, -72f), new Vector2(-PanelPadding * 0.5f, 0f), context);
                    foreach (TMP_Text text in child.GetComponentsInChildren<TMP_Text>(true))
                        SetAlignment(text.rectTransform, TextAlignmentOptions.MidlineLeft, context);
                    top = 80f;
                }
            }
            foreach (RectTransform child in list)
            {
                if (child.GetComponent<ScrollRect>() == null && child.GetComponent<TMP_Text>() == null) continue;
                SetRect(child, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(12f, 12f), new Vector2(-12f, -top), context);
            }
        }

        private static void LayoutDetail(RectTransform detail, bool framed, Context context)
        {
            // 가운데 창 안에서는 세로 공간이 좁아 여백·그림 높이를 줄인다.
            float padding = framed ? 28f : PanelPadding;
            float gap = framed ? 20f : BlockGap;
            float illustrationHeight = framed ? 300f : IllustrationHeight;
            float y = padding;
            if (detail.Find("DetailTitleBar") is RectTransform titleBar)
            {
                SetRect(titleBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                    new Vector2(padding, -(y + DetailTitleHeight)), new Vector2(-padding, -y), context);
                foreach (TMP_Text text in titleBar.GetComponentsInChildren<TMP_Text>(true))
                    SetAlignment(text.rectTransform, TextAlignmentOptions.MidlineLeft, context);
                y += DetailTitleHeight + gap;
            }
            if (detail.Find("IllustrationImage") is RectTransform illustration)
            {
                SetRect(illustration, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                    new Vector2(padding, -(y + illustrationHeight)), new Vector2(-padding, -y), context);
                y += illustrationHeight + gap;
            }

            RectTransform button = null;
            foreach (Button candidate in detail.GetComponentsInChildren<Button>(true))
                if (candidate.transform.parent == detail) button = (RectTransform)candidate.transform;
            float bottom = padding;
            if (button != null)
            {
                SetRect(button, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-padding - 220f, padding), new Vector2(-padding, padding + ButtonHeight), context);
                bottom += ButtonHeight + gap;
            }

            if (detail.Find("DescriptionScrollView") is RectTransform description)
            {
                SetRect(description, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(padding, bottom), new Vector2(-padding, -y), context);
                // 뷰포트 크기가 0이면 설명이 마스크에 가려 보이지 않으므로 스크롤 영역을 꽉 채운다(오른쪽은 스크롤바 자리).
                ScrollRect scroll = description.GetComponent<ScrollRect>();
                if (scroll != null && scroll.viewport != null)
                    SetRect(scroll.viewport, Vector2.zero, Vector2.one, new Vector2(0f, 1f), Vector2.zero, new Vector2(-(ScrollbarWidth + 12f), 0f), context);
            }
        }

        private static void LayoutDialog(RectTransform dialog, Context context)
        {
            SetRect(dialog, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-DialogWidth * 0.5f, -DialogHeight * 0.5f), new Vector2(DialogWidth * 0.5f, DialogHeight * 0.5f), context);

            List<RectTransform> buttons = new();
            RectTransform title = null;
            RectTransform description = null;
            foreach (RectTransform child in dialog)
            {
                if (child.GetComponent<Button>() != null) buttons.Add(child);
                else if (child.GetComponent<TMP_Text>() != null && child.name.Contains("Title", StringComparison.Ordinal)) title = child;
                else if (child.GetComponent<TMP_Text>() != null && child.name.Contains("Description", StringComparison.Ordinal)) description = child;
            }

            float top = DialogPadding;
            if (title != null)
            {
                SetRect(title, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                    new Vector2(DialogPadding, -(top + DialogTitleHeight)), new Vector2(-DialogPadding, -top), context);
                SetAlignment(title, TextAlignmentOptions.MidlineLeft, context);
                top += DialogTitleHeight + 12f;
            }
            if (description != null)
            {
                SetRect(description, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(DialogPadding, DialogPadding + ButtonHeight + BlockGap), new Vector2(-DialogPadding, -top), context);
                SetAlignment(description, TextAlignmentOptions.TopLeft, context);
            }

            // 주 버튼(확인)을 가장 오른쪽에, 나머지는 그 왼쪽으로
            buttons.Sort((a, b) => IsPrimary(b).CompareTo(IsPrimary(a)));
            float right = DialogPadding;
            foreach (RectTransform button in buttons)
            {
                SetRect(button, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-(right + DialogButtonWidth), DialogPadding), new Vector2(-right, DialogPadding + ButtonHeight), context);
                right += DialogButtonWidth + ButtonGap;
            }
        }

        // 시작 화면: 버튼을 같은 너비로 좁은 간격에 쌓는다.
        private static void LayoutStartMenu(Transform root, Context context)
        {
            Transform menu = root.name == "Start UI" ? root : FindDeep(root, "Start UI");
            float y = -170f;
            foreach (string name in new[] { "StartButton", "QuitButton" })
            {
                if (menu.Find(name) is not RectTransform button) continue;
                SetRect(button, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                    new Vector2(-160f, y - ButtonHeight), new Vector2(160f, y), context);
                y -= ButtonHeight + 16f;
            }
        }

        private static bool IsPrimary(RectTransform button)
        {
            foreach (string name in PrimaryButtonNames)
                if (button.name.Contains(name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        // 1080p 기준 px 오프셋으로 앵커·피벗·모서리를 정한다. 값이 같으면 건드리지 않는다.
        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 offsetMin, Vector2 offsetMax, Context context)
        {
            offsetMin *= context.Unit;
            offsetMax *= context.Unit;
            if (rect.anchorMin == anchorMin && rect.anchorMax == anchorMax && rect.pivot == pivot &&
                Approximately(rect.offsetMin, offsetMin) && Approximately(rect.offsetMax, offsetMax)) return;

            Record(rect, context);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Changed(rect, context);
        }

        private static bool Approximately(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 0.01f;

        private static void SetAlignment(RectTransform rect, TextAlignmentOptions alignment, Context context)
        {
            if (rect.TryGetComponent(out TMP_Text text) == false || text.alignment == alignment) return;
            Record(text, context);
            text.alignment = alignment;
            Changed(text, context);
        }

        #endregion

        #region Overrides

        // 프리팹 인스턴스에 남아 있는 글꼴·색 오버라이드를 지워 에셋 스타일을 그대로 따르게 한다. 위치·크기 오버라이드는 둔다.
        private static int RevertStyleOverrides(GameObject instanceRoot)
        {
            if (instanceRoot == null) return 0;
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
            if (modifications == null) return 0;

            List<PropertyModification> kept = new(modifications.Length);
            int removed = 0;
            foreach (PropertyModification modification in modifications)
            {
                if (modification.target is Graphic && IsStyleProperty(modification.propertyPath))
                {
                    removed++;
                    continue;
                }
                kept.Add(modification);
            }
            if (removed > 0) PrefabUtility.SetPropertyModifications(instanceRoot, kept.ToArray());
            return removed;
        }

        private static bool IsStyleProperty(string path) =>
            path.StartsWith("m_fontColor", StringComparison.Ordinal) ||
            path.StartsWith("m_fontAsset", StringComparison.Ordinal) ||
            path.StartsWith("m_sharedMaterial", StringComparison.Ordinal) ||
            path.StartsWith("m_fontStyle", StringComparison.Ordinal) ||
            path.StartsWith("m_Color", StringComparison.Ordinal) ||
            path.StartsWith("m_FontData.m_Font", StringComparison.Ordinal);

        #endregion

        #region Helpers

        // Image를 누가 어떤 타입으로 들고 있는지 한 번에 모아 둔다.
        private static void CollectReferences(Context context, IEnumerable<Component> components)
        {
            foreach (Component component in components)
            {
                if (component == null || component is Transform || component is CanvasRenderer) continue;
                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue is not Image image || image == component) continue;

                    if (context.References.TryGetValue(image, out List<(Component, string, string)> list) == false)
                        context.References[image] = list = new List<(Component, string, string)>();
                    list.Add((component, property.propertyPath, ReferenceType(property)));
                }
            }
        }

        // "PPtr<$Image>" → "Image"
        private static string ReferenceType(SerializedProperty property)
        {
            string type = property.type;
            if (type.StartsWith("PPtr<", StringComparison.Ordinal)) type = type.Substring(5, type.Length - 6);
            return type.TrimStart('$');
        }

        // 꺼져 있는 캔버스도 판단할 수 있도록 Canvas.rootCanvas 대신 부모를 직접 거슬러 올라간다.
        private static Canvas RootCanvas(Transform transform)
        {
            Canvas root = null;
            for (Transform current = transform; current != null; current = current.parent)
                if (current.TryGetComponent(out Canvas canvas)) root = canvas;
            return root;
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

        private static void Record(Object target, Context context)
        {
            if (context.UseUndo) Undo.RecordObject(target, "Unify UI Style");
        }

        private static void Changed(Object target, Context context)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            context.Changed++;
        }

        private static void ApplySerialized(SerializedObject serialized, Context context)
        {
            if (context.UseUndo) serialized.ApplyModifiedProperties();
            else serialized.ApplyModifiedPropertiesWithoutUndo();
            context.Changed++;
        }

        private static void Destroy(Object target, Context context)
        {
            if (context.UseUndo) Undo.DestroyObjectImmediate(target);
            else Object.DestroyImmediate(target, true);
        }

        #endregion
    }
}
