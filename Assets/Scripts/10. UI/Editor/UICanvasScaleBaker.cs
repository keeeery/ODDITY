using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // UI Canvas 프리팹의 임의 배율을 정리한다.
    // - 임무 목록·수락 목록·스테이지 이동 창(배율 0.65)의 배율을 1로 바꾸고, 그만큼 안쪽 위치·크기·글자·간격에 곱해 겉모습은 그대로 둔다.
    //   배율이 걸린 창은 같은 글자 크기를 써도 다른 창보다 작고, VectorPanel 테두리가 화면 1px보다 얇게 그려져 흐렸다.
    // - HUD Canvas 프리팹 이전에 쓰던 옛 HUD 복사본(HUD > Player UI / Battle UI)을 지운다. 로비 씬은 이미 이 부분을 지운 채 쓴다.
    //   프리팹 안에서 이 복사본을 가리키는 참조가 있으면 지우지 않고 알린다.
    // 배율이 이미 1이면 건너뛰므로 다시 실행해도 된다.
    public static class UICanvasScaleBaker
    {
        private const string UICanvasPath = "Assets/Prefabs/10. UI/UI Canvas.prefab";
        private static readonly string[] ScaledWindows = { "Mission UI", "Quest List UI", "Stage Move UI" };
        private const string LegacyHUDName = "HUD";

        [MenuItem("Tools/Team Odd/Bake UI Canvas Window Scales")]
        private static void Bake()
        {
            if (EditorUtility.DisplayDialog("UI Canvas 배율 정리",
                    "UI Canvas 프리팹에서 임무 창들의 배율(0.65)을 크기 값에 반영하고, 쓰지 않는 옛 HUD 복사본을 지웁니다.\n되돌리기(Ctrl+Z)가 되지 않으니 필요하면 먼저 커밋해 두세요.",
                    "정리", "취소") == false)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(UICanvasPath);
            try
            {
                foreach (string name in ScaledWindows)
                {
                    if (root.transform.Find(name) is not RectTransform window)
                    {
                        Debug.LogWarning($"[UIScale] UI Canvas 아래 '{name}'을(를) 찾지 못했습니다.");
                        continue;
                    }

                    float scale = window.localScale.x;
                    if (Mathf.Abs(scale - 1f) < 0.0001f) continue;
                    if (Mathf.Abs(window.localScale.y - scale) > 0.0001f)
                    {
                        Debug.LogWarning($"[UIScale] '{name}'의 가로·세로 배율이 달라 건너뜁니다.");
                        continue;
                    }

                    BakeWindow(window, scale);
                    Debug.Log($"[UIScale] '{name}'의 배율 {scale}을(를) 크기 값에 반영했습니다.");
                }

                RemoveLegacyHUD(root);
                PrefabUtility.SaveAsPrefabAsset(root, UICanvasPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 창 자신은 기준점을 중심으로 줄어든 모양 그대로 앵커·여백을 바꾸고, 안쪽은 모든 길이 값에 scale을 곱한다.
        private static void BakeWindow(RectTransform window, float scale)
        {
            Vector2 pivot = window.pivot;
            Vector2 pivotAnchor = LerpPerAxis(window.anchorMin, window.anchorMax, pivot);
            Vector2 pivotOffset = LerpPerAxis(window.offsetMin, window.offsetMax, pivot);
            Vector2 anchorMin = pivotAnchor + (window.anchorMin - pivotAnchor) * scale;
            Vector2 anchorMax = pivotAnchor + (window.anchorMax - pivotAnchor) * scale;
            Vector2 offsetMin = pivotOffset + (window.offsetMin - pivotOffset) * scale;
            Vector2 offsetMax = pivotOffset + (window.offsetMax - pivotOffset) * scale;
            window.anchorMin = anchorMin;
            window.anchorMax = anchorMax;
            window.offsetMin = offsetMin;
            window.offsetMax = offsetMax;
            window.localScale = Vector3.one;

            ScaleComponents(window.gameObject, scale);
            foreach (RectTransform child in window.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == window) continue;
                // X 버튼은 실행 중 스스로 화면 기준 크기를 맞추므로 1920x1080 기준 값 그대로 둔다
                if (child.GetComponentInParent<UICloseButton>(true) != null) continue;
                child.anchoredPosition *= scale;
                child.sizeDelta *= scale;
                ScaleComponents(child.gameObject, scale);
            }
        }

        private static Vector2 LerpPerAxis(Vector2 from, Vector2 to, Vector2 t) =>
            new(Mathf.Lerp(from.x, to.x, t.x), Mathf.Lerp(from.y, to.y, t.y));

        private static void ScaleComponents(GameObject target, float scale)
        {
            foreach (TMP_Text text in target.GetComponents<TMP_Text>())
            {
                text.fontSize *= scale;
                text.fontSizeMin *= scale;
                text.fontSizeMax *= scale;
                text.margin *= scale;
            }

            foreach (Text text in target.GetComponents<Text>())
            {
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(text.fontSize * scale));
                text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(text.resizeTextMinSize * scale));
                text.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(text.resizeTextMaxSize * scale));
            }

            foreach (LayoutGroup group in target.GetComponents<LayoutGroup>())
            {
                group.padding = ScaleOffset(group.padding, scale);
                if (group is HorizontalOrVerticalLayoutGroup line) line.spacing *= scale;
                if (group is GridLayoutGroup grid)
                {
                    grid.cellSize *= scale;
                    grid.spacing *= scale;
                }
            }

            foreach (LayoutElement element in target.GetComponents<LayoutElement>())
            {
                if (element.minWidth >= 0f) element.minWidth *= scale;
                if (element.minHeight >= 0f) element.minHeight *= scale;
                if (element.preferredWidth >= 0f) element.preferredWidth *= scale;
                if (element.preferredHeight >= 0f) element.preferredHeight *= scale;
            }

            foreach (Shadow shadow in target.GetComponents<Shadow>())
                shadow.effectDistance *= scale;

            // 9분할·타일 이미지는 모서리 두께가 그대로 보이도록 배율을 반대로 준다
            foreach (Image image in target.GetComponents<Image>())
            {
                if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
                    image.pixelsPerUnitMultiplier /= scale;
            }

            foreach (RectMask2D mask in target.GetComponents<RectMask2D>())
            {
                mask.padding *= scale;
                mask.softness = Vector2Int.RoundToInt((Vector2)mask.softness * scale);
            }

            foreach (ScrollRect scroll in target.GetComponents<ScrollRect>())
                scroll.scrollSensitivity *= scale;

            foreach (UIPopIn popIn in target.GetComponents<UIPopIn>())
            {
                var serialized = new SerializedObject(popIn);
                SerializedProperty rise = serialized.FindProperty("rise");
                rise.floatValue *= scale;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static RectOffset ScaleOffset(RectOffset offset, float scale) => new(
            Mathf.RoundToInt(offset.left * scale), Mathf.RoundToInt(offset.right * scale),
            Mathf.RoundToInt(offset.top * scale), Mathf.RoundToInt(offset.bottom * scale));

        private static void RemoveLegacyHUD(GameObject root)
        {
            Transform hud = root.transform.Find(LegacyHUDName);
            if (hud == null) return;

            List<string> users = FindReferencesInto(root, hud);
            if (users.Count > 0)
            {
                Debug.LogWarning($"[UIScale] 옛 HUD 복사본을 가리키는 참조가 있어 지우지 않았습니다:\n{string.Join("\n", users)}");
                return;
            }

            Object.DestroyImmediate(hud.gameObject);
            Debug.Log("[UIScale] 쓰지 않는 옛 HUD 복사본(HUD > Player UI / Battle UI)을 지웠습니다.");
        }

        // hud 밖의 컴포넌트가 hud 안의 오브젝트를 가리키는 직렬화 참조를 모은다
        private static List<string> FindReferencesInto(GameObject root, Transform hud)
        {
            var users = new List<string>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component.transform.IsChildOf(hud)) continue;

                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    Transform target = value switch
                    {
                        GameObject go => go.transform,
                        Component c => c.transform,
                        _ => null,
                    };
                    if (target != null && target.IsChildOf(hud))
                        users.Add($"{component.name}.{component.GetType().Name}.{property.propertyPath} → {target.name}");
                }
            }
            return users;
        }
    }
}
