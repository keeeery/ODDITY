using TeamOdd.Reward;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 임무 실패(DeathScreen 프리팹)·임무 성공(보스 씬 3곳의 BossRewardPanel) 화면에 ResultBanner 강조를 붙인다.
    // - 제목을 Pretendard Bold로 키우고, 위에 Orbit 윗줄(MISSION FAILED / MISSION COMPLETE)을 얹는다. 제목 양옆 선은 두지 않는다
    // - 실패: 붉은 강조 + 카운트다운 막대, 성공: 금색 강조 + 받은 칩 수 문구 + 칩 대표 아이콘을 겹친 보상 칸
    // 만든 것은 "Accent" 오브젝트 하나에 모으므로 다시 실행하면 지우고 새로 만든다. 씬은 저장까지 하고 원래 열려 있던 씬으로 돌아간다.
    public static class ResultScreenEnhancer
    {
        private const string DeathPrefabPath = "Assets/Prefabs/10. UI/HUD/DeathScreen.prefab";
        private static readonly string[] BossScenePaths =
        {
            "Assets/Scenes/10. Build/2. Final/03. Easy Boss.unity",
            "Assets/Scenes/10. Build/2. Final/04. Normal Boss.unity",
            "Assets/Scenes/10. Build/2. Final/05. Hard Boss.unity",
        };
        private const string BoldFontPath = "Assets/Font/Pretendard/Pretendard-Bold SDF.asset";
        private const string MediumFontPath = "Assets/Font/Pretendard/Pretendard-Medium SDF.asset";
        private const string OrbitFontPath = "Assets/Font/Orbit/Orbit SDF.asset";
        private const string AccentName = "Accent";
        private const string ChipIconName = "Chip Icon";
        private const string ChipBorderName = "Chip Border";
        private const string ChipBackName = "Chip Back";
        private const string DeathMessage = "임무 실패";

        private static readonly Color TextColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        private static readonly Color Danger = new(0.78f, 0.2f, 0.21f, 1f);
        private static readonly Color Gold = new(0.7686275f, 0.5764706f, 0.2901961f, 1f);

        [MenuItem("Tools/Team Odd/Enhance Result Screens")]
        private static void Enhance()
        {
            TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            TMP_FontAsset medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumFontPath);
            TMP_FontAsset orbit = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OrbitFontPath);
            if (bold == null || medium == null || orbit == null)
            {
                Debug.LogError($"[ResultScreen] 폰트를 찾지 못했습니다: {BoldFontPath}, {MediumFontPath}, {OrbitFontPath}");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;

            EnhanceDeathScreen(bold, orbit);

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in BossScenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    if (EnhanceRewardPanel(bold, medium, orbit) == false)
                    {
                        Debug.LogWarning($"[ResultScreen] {path}에서 BossRewardPanel/Mission Complete Canvas/Reveal/Dim/Title 을 찾지 못했습니다.");
                        continue;
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            Debug.Log("[ResultScreen] 임무 성공·실패 화면 강조 완료.");
        }

        // 검은 화면 가운데: 윗줄 / 제목 / 카운트다운 / 줄어드는 막대
        private static void EnhanceDeathScreen(TMP_FontAsset bold, TMP_FontAsset orbit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DeathPrefabPath);
            try
            {
                DeathScreen screen = root.GetComponent<DeathScreen>();
                Transform reveal = root.transform.Find("Panel/BandReveal");
                Transform band = reveal != null ? reveal.Find("Band") : null;
                TMP_Text title = band != null ? band.Find("MessageText")?.GetComponent<TMP_Text>() : null;
                Transform countdown = band != null ? band.Find("CountdownText") : null;
                if (screen == null || title == null || countdown == null)
                {
                    Debug.LogError("[ResultScreen] DeathScreen 프리팹 구조가 달라 강조를 붙이지 못했습니다. '1. Build Death Screen UI' 로 다시 만든 뒤 실행하세요.");
                    return;
                }

                ((RectTransform)reveal).sizeDelta = new Vector2(0f, 320f);
                title.text = DeathMessage;
                var serializedScreen = new SerializedObject(screen);
                serializedScreen.FindProperty("message").stringValue = DeathMessage;
                serializedScreen.ApplyModifiedPropertiesWithoutUndo();
                StyleTitle(title, bold, 84f, new Vector2(0f, 30f));
                ((RectTransform)countdown).anchoredPosition = new Vector2(0f, -54f);

                // 띠의 UIStaggerReveal이 자식을 차례로 켜므로 강조는 맨 앞에 한 묶음으로 둔다
                RectTransform accent = RebuildAccent(band);
                TMP_Text kicker = NewText("Kicker", accent, orbit, "MISSION FAILED", 18f, Danger, new Vector2(0f, 104f), new Vector2(600f, 28f));
                kicker.characterSpacing = 30f;

                Color trackColor = TextColor;
                trackColor.a = 0.12f;
                NewLine("Progress Track", accent, trackColor, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(360f, 2f));
                RectTransform progress = NewLine("Progress", accent, Danger, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(360f, 2f));

                ResultBanner banner = accent.gameObject.AddComponent<ResultBanner>();
                Wire(banner, ("kicker", kicker), ("title", title), ("progress", progress));
                Wire(screen, ("banner", banner));

                PrefabUtility.SaveAsPrefabAsset(root, DeathPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 어두운 전체 화면: 윗줄 / 제목 / 받은 칩 수 / 보상 칸(칸 그림 + 대표 아이콘) / 확인
        private static bool EnhanceRewardPanel(TMP_FontAsset bold, TMP_FontAsset medium, TMP_FontAsset orbit)
        {
            BossRewardPanel panel = Object.FindFirstObjectByType<BossRewardPanel>(FindObjectsInactive.Include);
            Transform dim = panel != null ? panel.transform.Find("Mission Complete Canvas/Reveal/Dim") : null;
            TMP_Text title = dim != null ? dim.Find("Title")?.GetComponent<TMP_Text>() : null;
            if (title == null) return false;

            StyleTitle(title, bold, 84f, new Vector2(0f, 180f));

            RectTransform accent = RebuildAccent(dim);
            TMP_Text kicker = NewText("Kicker", accent, orbit, "MISSION COMPLETE", 18f, Gold, new Vector2(0f, 254f), new Vector2(600f, 28f));
            kicker.characterSpacing = 30f;
            Color countColor = TextColor;
            countColor.a = 0.8f;
            TMP_Text rewardCount = NewText("Reward Count", accent, medium, "증강 칩 0개를 획득했습니다.", 22f, countColor, new Vector2(0f, 76f), new Vector2(800f, 32f));

            ResultBanner banner = accent.gameObject.AddComponent<ResultBanner>();
            Wire(banner, ("kicker", kicker), ("title", title));
            Wire(panel, ("banner", banner), ("rewardCountText", rewardCount));

            EnhanceRewardSlot(dim);
            return true;
        }

        // 보상 칸: 칩 계열색 외곽선 + 바탕(스킬 칩은 칸 색, 스탯 칩은 검정) + 칩 대표 아이콘. 칸 그림(Icon)은 투명하게 두고 툴팁 영역으로만 쓴다.
        // 런타임에 이 칸을 복제해 늘리므로 하나만 고치면 된다.
        private static void EnhanceRewardSlot(Transform dim)
        {
            if (dim.Find("HorizontalGrid") is not RectTransform grid) return;
            if (grid.TryGetComponent(out GridLayoutGroup layout))
            {
                layout.cellSize = new Vector2(96f, 96f);
                layout.spacing = new Vector2(14f, 14f);
            }
            grid.anchoredPosition = new Vector2(0f, -32f);

            RewardIconView slot = grid.GetComponentInChildren<RewardIconView>(true);
            if (slot == null || slot.transform.Find("Icon") is not RectTransform icon || icon.TryGetComponent(out Image iconImage) == false) return;

            icon.sizeDelta = new Vector2(-12f, -12f);
            iconImage.sprite = null;
            iconImage.color = new Color(1f, 1f, 1f, 0f);
            foreach (string name in new[] { ChipBorderName, ChipBackName, ChipIconName })
            {
                Transform old = icon.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            RectTransform border = NewUI(ChipBorderName, icon);
            Stretch(border, 0f);
            RawImage borderImage = border.gameObject.AddComponent<RawImage>();
            borderImage.raycastTarget = false;

            RectTransform back = NewUI(ChipBackName, icon);
            Stretch(back, 2f);
            Image backImage = back.gameObject.AddComponent<Image>();
            backImage.color = Color.black;
            backImage.raycastTarget = false;

            RectTransform chipIcon = NewUI(ChipIconName, icon);
            chipIcon.anchorMin = new Vector2(0.2f, 0.2f);
            chipIcon.anchorMax = new Vector2(0.8f, 0.8f);
            chipIcon.sizeDelta = Vector2.zero;
            Image image = chipIcon.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            Wire(slot, ("chipIcon", image), ("colorBorder", borderImage), ("colorBack", backImage));
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(-inset * 2f, -inset * 2f);
        }

        private static void StyleTitle(TMP_Text title, TMP_FontAsset bold, float size, Vector2 position)
        {
            title.font = bold;
            title.fontSize = size;
            title.fontStyle = FontStyles.Normal;
            title.characterSpacing = 4f;
            RectTransform rect = title.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1000f, size + 24f);
            rect.localScale = Vector3.one;
        }

        private static RectTransform RebuildAccent(Transform parent)
        {
            Transform old = parent.Find(AccentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            RectTransform accent = NewUI(AccentName, parent);
            accent.anchorMin = Vector2.zero;
            accent.anchorMax = Vector2.one;
            accent.sizeDelta = Vector2.zero;
            accent.SetAsFirstSibling();
            return accent;
        }

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
            Vector2 position, Vector2 sizeDelta)
        {
            RectTransform rect = NewUI(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform NewLine(string name, Transform parent, Color color, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = NewUI(name, parent);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void Wire(Object target, params (string name, Object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach ((string name, Object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property == null)
                {
                    Debug.LogError($"[ResultScreen] {target.GetType().Name}에 {name} 필드가 없습니다.");
                    continue;
                }
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
