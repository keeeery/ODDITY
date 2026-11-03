using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // [F] 또는 [우클릭] 상호작용처럼 키캡과 설명을 한 줄로 늘어놓는 조작 안내를 실행 중에 만든다.
    // NPC 상호작용 안내(InteractionPrompt)와 증강칩 장착 창의 조작 안내가 같은 생김새를 쓰도록 모았다.
    // 크기는 기준 해상도 1920x1080 캔버스 기준이며, 기준 해상도가 다른 캔버스는 scale로 맞춘다 (3840x2160이면 2).
    public static class KeyHintBuilder
    {
        public const float DefaultFontSize = 19f;
        // HUD 기본 회백색보다 밝은 백색, 밝은 바닥·벽 위에서도 외곽선·그림자와 함께 눈에 띄도록
        public static readonly Color TextColor = new(1f, 1f, 1f, 1f);
        public const float SubTextAlpha = 0.6f;

        // 자식을 가로로 늘어놓고 내용 크기에 맞춰 줄어드는 줄
        public static RectTransform NewRow(string objectName, Transform parent, float scale)
        {
            RectTransform row = NewUI(objectName, parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f * scale;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return row;
        }

        // HUD 슬롯과 같은 각진 패널 위에 키 이름을 적은 키캡
        public static GameObject AddKey(Transform row, string key, TMP_FontAsset font, float scale)
        {
            RectTransform cap = NewUI($"Key {key}", row);
            VectorPanel panel = cap.gameObject.AddComponent<VectorPanel>();
            panel.raycastTarget = false;
            HorizontalLayoutGroup layout = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
            int horizontal = Mathf.RoundToInt(8f * scale);
            int vertical = Mathf.RoundToInt(3f * scale);
            layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            // 한 글자 키도 정사각형에 가깝게 보이도록
            LayoutElement element = cap.gameObject.AddComponent<LayoutElement>();
            element.minWidth = (DefaultFontSize + 10f) * scale;

            TMP_Text label = AddText(cap, "Label", key, font, DefaultFontSize * 0.9f * scale, TextColor);
            label.fontStyle = FontStyles.Bold;
            return cap.gameObject;
        }

        // 키캡 사이의 "또는"처럼 흐리게 보이는 글자
        public static TMP_Text AddSubText(Transform row, string objectName, string text, TMP_FontAsset font, float scale)
        {
            Color color = TextColor;
            color.a *= SubTextAlpha;
            return AddText(row, objectName, text, font, DefaultFontSize * 0.85f * scale, color);
        }

        public static TMP_Text AddText(Transform row, string objectName, string text, TMP_FontAsset font, float size, Color color)
        {
            RectTransform rect = NewUI(objectName, row);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset textFont = font != null ? font : ReadableTextStyle.Font;
            if (textFont != null) label.font = textFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            ReadableTextStyle.Apply(label);
            return label;
        }

        private static RectTransform NewUI(string objectName, Transform parent)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
