using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 팝업 창들의 제목줄 굵기를 증강 칩 장착 창에 맞춘다(3840x2160 기준 단위).
    // 제목줄: 창 위에서 48 띄운 높이 96(글자 58), 내용은 창 위에서 192부터.
    // 제목 글자는 띠 안에서 세로 가운데라, 우상단 X 버튼 가운데(1080p 기준 48 = 이 단위 96)와 같은 높이에 온다.
    // 제목 글자 왼쪽 끝은 창 왼쪽에서, X 선 오른쪽 끝이 창 오른쪽에서 떨어진 거리만큼 띄운다.
    // 제목줄 아래 구분선은 끈다.
    // - 증강 칩 창: 제목 글자를 세로 가운데로, 우상단 칩 수 글자도 같은 높이 가운데로
    // - 임무 목록·임무 이동 창: TitleBar 위치·높이, TitleText 글자 크기·정렬, MissionPage 윗변, TitleDivider 끔
    // - 이동 불가·진입 실패 안내 창: 제목 글자 위치·높이·크기·정렬, 설명 글 윗변
    // 다시 실행해도 같은 결과가 된다.
    // 타이틀 씬 팝업(1920x1080 기준)은 빌더(Apply Title Menu)가 같은 비율(24/48/29, 왼쪽 MarkEdgeInset)로 만든다.
    public static class WindowTitleBarUnifier
    {
        private const string UICanvasPath = "Assets/Prefabs/10. UI/UI Canvas.prefab";
        private const string ChipPanelPath = "Assets/Prefabs/4. Superpower/Augmentation Chip Panel.prefab";

        private const float TitleTop = 48f;
        private const float TitleHeight = 96f;
        private const float TitleFontSize = 58f;
        private const float ContentTop = 192f;
        private const float TitleCenter = TitleTop + TitleHeight * 0.5f;
        // X 버튼 값은 1080p 기준이라 이 단위로 2배
        private static readonly float TitleLeft = WindowCloseButtonInstaller.MarkEdgeInset * 2f;

        private static readonly string[] BarWindows = { "JournalWindow", "StageMoveWindow" };
        private static readonly string[] DialogWindows = { "MoveFail", "ClickFail" };

        [MenuItem("Tools/Team Odd/Unify Window Title Bars")]
        private static void Apply()
        {
            ApplyChipPanel();
            ApplyUICanvas();
            Debug.Log("[TitleBar] 팝업 창 제목줄을 증강 칩 창에 맞췄습니다.");
        }

        private static void ApplyChipPanel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ChipPanelPath);
            try
            {
                if (root.transform.Find("Title") is RectTransform title)
                {
                    PlaceTitle(title);
                    SetLeft(title, TitleLeft);
                    if (title.TryGetComponent(out Text text)) text.alignment = TextAnchor.MiddleLeft;
                }

                // 칩 수 글자: 높이는 그대로, 제목과 같은 세로 가운데에 오른쪽 정렬
                if (root.transform.Find("Counts") is RectTransform counts)
                {
                    counts.pivot = new Vector2(counts.pivot.x, 0.5f);
                    counts.anchoredPosition = new Vector2(counts.anchoredPosition.x, -TitleCenter);
                    if (counts.TryGetComponent(out Text text)) text.alignment = TextAnchor.MiddleRight;
                }

                PrefabUtility.SaveAsPrefabAsset(root, ChipPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyUICanvas()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UICanvasPath);
            try
            {
                foreach (string name in BarWindows)
                {
                    Transform window = FindDeep(root.transform, name);
                    if (window == null)
                    {
                        Debug.LogWarning($"[TitleBar] UI Canvas 프리팹에서 '{name}'을(를) 찾지 못했습니다.");
                        continue;
                    }
                    var bar = window.Find("TitleBar") as RectTransform;
                    PlaceTitle(bar);
                    if (bar != null) SetLeft(bar.Find("TitleText") as RectTransform, TitleLeft - bar.offsetMin.x);
                    SetFontSize(window.Find("TitleBar/TitleText"));
                    SetContentTop(window.Find("MissionPage") as RectTransform);
                    Transform divider = window.Find("TitleBar/TitleDivider");
                    if (divider != null) divider.gameObject.SetActive(false);
                }

                foreach (string name in DialogWindows)
                {
                    Transform window = FindDeep(root.transform, $"{name} UI");
                    if (window == null)
                    {
                        Debug.LogWarning($"[TitleBar] UI Canvas 프리팹에서 '{name} UI'을(를) 찾지 못했습니다.");
                        continue;
                    }
                    Transform title = window.Find($"{name} Title Text");
                    PlaceTitle(title as RectTransform);
                    SetLeft(title as RectTransform, TitleLeft);
                    SetFontSize(title);
                    SetContentTop(window.Find($"{name} Description Text") as RectTransform);
                }

                PrefabUtility.SaveAsPrefabAsset(root, UICanvasPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 창 윗변에 붙은 가로 띠: 위에서 TitleTop 띄우고 높이 TitleHeight
        private static void PlaceTitle(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(rect.anchorMin.x, 1f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
            rect.pivot = new Vector2(rect.pivot.x, 1f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -TitleTop);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, TitleHeight);
        }

        // 부모 왼쪽 끝에서 left만큼 띄운다. 오른쪽 끝은 그대로 둔다(늘어나는 칸) / 폭은 그대로 둔다(고정 칸)
        private static void SetLeft(RectTransform rect, float left)
        {
            if (rect == null || rect.anchorMin.x > 0f) return;
            if (rect.anchorMax.x > rect.anchorMin.x)
                rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            else
                rect.anchoredPosition = new Vector2(left + rect.pivot.x * rect.sizeDelta.x, rect.anchoredPosition.y);
        }

        private static void SetFontSize(Transform target)
        {
            if (target == null || target.TryGetComponent(out TMP_Text text) == false) return;
            text.enableAutoSizing = false;
            text.fontSize = TitleFontSize;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
        }

        // 세로로 늘어나는 내용 영역의 윗변만 창 위에서 ContentTop으로 옮긴다
        private static void SetContentTop(RectTransform rect)
        {
            if (rect == null || rect.anchorMax.y < 1f) return;
            rect.offsetMax = new Vector2(rect.offsetMax.x, -ContentTop);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
