using TeamOdd.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    // 증강칩 장착 창 제목 오른쪽에 붙이는 조작 안내. [드래그] 장착/해제 [우클릭] 빠른 장착/해제 [R] 회전
    // NPC 상호작용 안내와 같은 KeyHintBuilder 생김새를 쓰고, 패널을 처음 열 때 한 번 만든다.
    public static class ChipControlHint
    {
        private const string TitleName = "Title";
        private const string HintName = "Control Hint";
        // 칩 캔버스 기준 해상도(3840x2160)가 HUD(1920x1080)의 두 배라 같은 크기로 보이도록 키운다
        private const float CanvasScale = 2f;
        // 제목 글자 끝에서 안내까지의 여백(칩 캔버스 px)
        private const float TitleGap = 40f;

        public static void Attach(Transform panel)
        {
            Text title = FindTitle(panel);
            if (title == null || title.transform.parent.Find(HintName) != null) return;

            RectTransform titleRect = title.rectTransform;
            RectTransform row = KeyHintBuilder.NewRow(HintName, titleRect.parent, CanvasScale);
            row.anchorMin = titleRect.anchorMin;
            row.anchorMax = titleRect.anchorMax;
            // 제목 칸의 세로 가운데에 맞추고, 가로는 실제 글자 너비 끝에서 TitleGap만큼 띄운다
            row.pivot = new Vector2(0f, 0.5f);
            Vector2 titleLeftCenter = titleRect.anchoredPosition
                                      + new Vector2(-titleRect.pivot.x * titleRect.rect.width, (0.5f - titleRect.pivot.y) * titleRect.rect.height);
            row.anchoredPosition = titleLeftCenter + new Vector2(title.preferredWidth + TitleGap, 0f);

            AddPair(row, "드래그", "장착/해제");
            AddSpacer(row);
            AddPair(row, "우클릭", "빠른 장착/해제");
            AddSpacer(row);
            AddPair(row, "R", "회전");
        }

        private static void AddPair(RectTransform row, string key, string action)
        {
            KeyHintBuilder.AddKey(row, key, null, CanvasScale);
            KeyHintBuilder.AddText(row, $"Action {key}", action, null, KeyHintBuilder.DefaultFontSize * CanvasScale, KeyHintBuilder.TextColor);
        }

        // 묶음 사이를 키캡과 설명 사이보다 넓게 띄운다
        private static void AddSpacer(RectTransform row)
        {
            LayoutElement spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement)).GetComponent<LayoutElement>();
            spacer.transform.SetParent(row, false);
            spacer.minWidth = 10f * CanvasScale;
        }

        private static Text FindTitle(Transform panel)
        {
            foreach (Text text in panel.GetComponentsInChildren<Text>(true))
            {
                if (text.name == TitleName)
                    return text;
            }

            return null;
        }
    }
}
