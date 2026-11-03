using TeamOdd.Mission;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 임무 목록 창(UI Canvas 프리팹의 Mission UI / JournalWindow)을 다듬는다.
    // - 제목 "Journal" → "임무 목록", 수락 버튼 "Accept" → "임무 수락"
    // - 수락 버튼 왼쪽에 "수락 및 이동" 버튼을 두고 MissionUI.RegisterAndMove를 연결한다(보조 버튼 모양)
    // - 목록 행의 "!" 표시와 보스 이름이 겹치지 않게, "!"는 왼쪽 고정 폭 칸에 두고 이름은 그 오른쪽부터 왼쪽 정렬한다
    // - 임무 이동 창(Stage Move UI)을 임무 목록 창과 같은 창 모양·크기·여백·패널 배치로 맞추고 글자를 한글로 바꾼다
    // - 두 창의 X 버튼을 다시 만든다(제목줄 Exit 버튼은 끈다)
    // 길이 값은 배율 1 기준이고, Mission UI에 배율이 남아 있으면(Bake UI Canvas Window Scales 전) 그만큼 나눠 넣어 화면에서 같게 보인다.
    // 다시 실행해도 같은 결과가 된다.
    public static class MissionWindowUpdater
    {
        private const string UICanvasPath = "Assets/Prefabs/10. UI/UI Canvas.prefab";
        private const string MoveButtonName = "AcceptMoveButton";
        private const float ButtonGap = 16f;

        // 행 안 배치(배율 1 기준 단위): "!" 칸, 이름 시작 위치, 오른쪽 완료 표시 자리
        private const float MarkLeft = 20f;
        private const float MarkWidth = 42f;
        private const float TitleLeft = MarkLeft + MarkWidth + 10f;
        private const float TitleRight = 104f;

        private static readonly Color SecondaryTrack = new(0.07058824f, 0.07843137f, 0.09019608f, 0.85f);
        private static readonly Color SecondaryBorder = new(0.84705883f, 0.8235294f, 0.76862746f, 0.45f);
        private static readonly Color SecondaryText = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);

        // 임무 이동 창에서 임무 목록 창의 배치를 그대로 따를 부분: (임무 이동 창 경로, 임무 목록 창 경로)
        private static readonly (string stage, string journal)[] SharedRects =
        {
            ("TitleBar", "TitleBar"),
            ("TitleBar/TitleText", "TitleBar/TitleText"),
            ("MissionPage", "MissionPage"),
            ("MissionPage/MissionListPanel", "MissionPage/ChapterListPanel"),
            ("MissionPage/DetailPanel", "MissionPage/DetailPanel"),
            ("MissionPage/DetailPanel/DetailTitleBar", "MissionPage/DetailPanel/DetailTitleBar"),
            ("MissionPage/DetailPanel/IllustrationImage", "MissionPage/DetailPanel/IllustrationImage"),
            ("MissionPage/DetailPanel/DescriptionScrollView", "MissionPage/DetailPanel/DescriptionScrollView"),
            ("MissionPage/DetailPanel/MoveButton", "MissionPage/DetailPanel/AcceptButton"),
        };

        [MenuItem("Tools/Team Odd/Update Mission Window")]
        private static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UICanvasPath);
            try
            {
                Transform window = FindDeep(root.transform, "JournalWindow");
                MissionUI mission = window != null ? window.GetComponentInParent<MissionUI>(true) : null;
                Transform accept = window != null ? FindDeep(window, "AcceptButton") : null;
                if (mission == null || accept == null)
                {
                    Debug.LogError("[MissionWindow] UI Canvas 프리팹에서 MissionUI / JournalWindow / AcceptButton 을 찾지 못했습니다.");
                    return;
                }

                SetLabel(window.Find("TitleBar/TitleText"), "임무 목록");
                SetLabel(accept.Find("Label"), "임무 수락");
                float unit = 1f / mission.transform.localScale.x;
                CreateMoveButton((RectTransform)accept, mission, unit);
                FixRows(window, unit);
                AddCloseButton((RectTransform)window, mission.CloseUI);
                UpdateStageMoveWindow(root.transform, (RectTransform)window);

                PrefabUtility.SaveAsPrefabAsset(root, UICanvasPath);
                Debug.Log("[MissionWindow] 임무 목록 창을 고쳤습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 임무 이동 창을 임무 목록 창과 같은 모양으로 맞춘다. 임무 목록 창의 배율이 이미 정리된(1) 뒤에만 한다.
        private static void UpdateStageMoveWindow(Transform canvas, RectTransform journal)
        {
            StageMoveUI stage = canvas.GetComponentInChildren<StageMoveUI>(true);
            RectTransform window = stage != null ? stage.transform.Find("StageMoveWindow") as RectTransform : null;
            if (window == null)
            {
                Debug.LogWarning("[MissionWindow] UI Canvas 프리팹에서 Stage Move UI / StageMoveWindow 를 찾지 못했습니다.");
                return;
            }
            if (Mathf.Abs(stage.transform.localScale.x - 1f) > 0.0001f || Mathf.Abs(journal.parent.localScale.x - 1f) > 0.0001f)
            {
                Debug.LogWarning("[MissionWindow] 창 배율이 남아 있어 임무 이동 창은 맞추지 않았습니다. Bake UI Canvas Window Scales 를 먼저 실행하세요.");
                return;
            }

            // 바깥은 화면 전체, 창은 임무 목록 창과 같은 크기로 가운데
            var root = (RectTransform)stage.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = journal.sizeDelta;

            // 창 바탕: 테두리 없던 어두운 막을 임무 목록 창과 같은 VectorPanel로
            if (journal.TryGetComponent(out VectorPanel journalPanel))
            {
                if (window.TryGetComponent(out Image image)) Object.DestroyImmediate(image);
                if (window.TryGetComponent(out VectorPanel panel) == false) panel = window.gameObject.AddComponent<VectorPanel>();
                EditorUtility.CopySerialized(journalPanel, panel);
            }

            foreach ((string stagePath, string journalPath) in SharedRects)
            {
                if (window.Find(stagePath) is RectTransform target && journal.Find(journalPath) is RectTransform source)
                    CopyRect(source, target);
                else
                    Debug.LogWarning($"[MissionWindow] '{stagePath}' 또는 '{journalPath}' 를 찾지 못해 배치를 맞추지 못했습니다.");
            }

            SetLabel(window.Find("TitleBar/TitleText"), "임무 이동");
            SetLabel(window.Find("MissionPage/DetailPanel/MoveButton/Label"), "이동");
            Transform fail = stage.transform.Find("MoveFail UI");
            if (fail != null) SetLabel(fail.Find("ConfirmButton/Label"), "확인");

            AddCloseButton(window, stage.CloseUI);
        }

        // 제목줄의 글자 닫기 버튼은 끄고 우상단 X로 닫는다
        private static void AddCloseButton(RectTransform window, UnityAction close)
        {
            Transform exit = window.Find("TitleBar/ExitButton");
            if (exit != null) exit.gameObject.SetActive(false);
            Button button = WindowCloseButtonInstaller.Create(window);
            UnityEventTools.AddPersistentListener(button.onClick, close);
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
        }

        // 수락 버튼을 복제해 바로 왼쪽에 두고, 테두리만 있는 보조 버튼 모양으로 바꾼다.
        private static void CreateMoveButton(RectTransform accept, MissionUI mission, float unit)
        {
            Transform old = accept.parent.Find(MoveButtonName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject copy = Object.Instantiate(accept.gameObject, accept.parent, false);
            copy.name = MoveButtonName;
            var rect = (RectTransform)copy.transform;
            rect.anchoredPosition = accept.anchoredPosition - new Vector2(accept.sizeDelta.x + ButtonGap * unit, 0f);
            rect.SetSiblingIndex(accept.GetSiblingIndex());

            Button button = copy.GetComponent<Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(mission.RegisterAndMove));

            if (copy.TryGetComponent(out VectorPanel panel))
            {
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("trackColor").colorValue = SecondaryTrack;
                serialized.FindProperty("borderColor").colorValue = SecondaryBorder;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform label = copy.transform.Find("Label");
            SetLabel(label, "수락 및 이동");
            if (label != null && label.TryGetComponent(out TMP_Text text)) text.color = SecondaryText;
        }

        // 목록 행: "!"는 왼쪽 고정 칸 가운데, 이름은 그 오른쪽부터 완료 표시 앞까지 왼쪽 정렬
        private static void FixRows(Transform window, float unit)
        {
            foreach (TMP_Text mark in window.GetComponentsInChildren<TMP_Text>(true))
            {
                if (mark.name != "QuestMarkIcon") continue;
                Transform row = mark.transform.parent;

                RectTransform markRect = mark.rectTransform;
                markRect.anchorMin = new Vector2(0f, 0f);
                markRect.anchorMax = new Vector2(0f, 1f);
                markRect.pivot = new Vector2(0f, 0.5f);
                markRect.anchoredPosition = new Vector2(MarkLeft * unit, 0f);
                markRect.sizeDelta = new Vector2(MarkWidth * unit, 0f);
                mark.alignment = TextAlignmentOptions.Center;
                mark.textWrappingMode = TextWrappingModes.NoWrap;

                if (row.Find("QuestTitleText") is RectTransform titleRect && titleRect.TryGetComponent(out TMP_Text title))
                {
                    titleRect.anchorMin = new Vector2(0f, 0f);
                    titleRect.anchorMax = new Vector2(1f, 1f);
                    titleRect.pivot = new Vector2(0f, 0.5f);
                    titleRect.offsetMin = new Vector2(TitleLeft * unit, 0f);
                    titleRect.offsetMax = new Vector2(-TitleRight * unit, 0f);
                    title.alignment = TextAlignmentOptions.MidlineLeft;
                    title.textWrappingMode = TextWrappingModes.NoWrap;
                    title.overflowMode = TextOverflowModes.Ellipsis;
                }
            }
        }

        private static void SetLabel(Transform target, string value)
        {
            if (target != null && target.TryGetComponent(out TMP_Text text)) text.text = value;
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
