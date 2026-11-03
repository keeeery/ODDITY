using TeamOdd.Mission;
using TeamOdd.Superpower;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 환경설정·임무 목록·증강 칩 장착 창의 우상단에 같은 모양·같은 거리의 X 버튼(UICloseButton)을 둔다.
    // - 크기는 1920x1080 기준 값으로 만들고, 실행 중 UICloseButton이 캔버스·부모 배율에 맞춰 화면에서 같은 크기·거리로 맞춘다
    // - 임무 목록: 제목줄의 기존 닫기 버튼(ExitButton)은 끄고 MissionUI.CloseUI를 부른다
    // - 증강 칩: AugmentationChipPanel.Close 후 커서를 잠그고, 우상단 칩 수 글자는 X와 겹치지 않게 왼쪽으로 민다
    // - 환경설정: 타이틀 빌더(Apply Title Menu)가 이 클래스의 Create로 만든다. 열린 씬에 Settings Popup이 있으면 여기서도 바꾼다
    // 다시 실행하면 같은 이름의 X를 지우고 새로 만든다.
    public static class WindowCloseButtonInstaller
    {
        public const string ButtonName = "Close X";

        // 1080p 기준: 창 모서리에서 X 가운데까지, 누름 영역, X 선 길이·굵기
        private const float CenterInset = 48f;
        private const float HitSize = 56f;
        private const float StrokeLength = 24f;
        private const float StrokeThickness = 2.5f;

        // 1080p 기준: 창 오른쪽 끝에서 보이는 X 선 오른쪽 끝까지(45° 선의 가로 반폭을 뺀 거리). 제목 왼쪽 여백을 이 값에 맞춘다
        public static readonly float MarkEdgeInset =
            CenterInset - (StrokeLength + StrokeThickness) * 0.5f * Mathf.Cos(45f * Mathf.Deg2Rad);

        private const string UICanvasPath = "Assets/Prefabs/10. UI/UI Canvas.prefab";
        private const string ChipPanelPath = "Assets/Prefabs/4. Superpower/Augmentation Chip Panel.prefab";

        [MenuItem("Tools/Team Odd/Add Window Close Buttons")]
        private static void Install()
        {
            InstallMission();
            InstallChipPanel();
            InstallOpenSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[CloseButton] 임무 목록·증강 칩 창에 X 버튼을 넣었습니다.");
        }

        // 창 우상단에 X 버튼을 만든다. 닫는 동작은 부르는 쪽이 onClick에 연결한다.
        public static Button Create(RectTransform window, bool lockCursorOnClose = false)
        {
            Transform old = window.Find(ButtonName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            RectTransform rect = NewUI(ButtonName, window);
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-CenterInset, -CenterInset);
            rect.sizeDelta = Vector2.one * HitSize;
            rect.SetAsLastSibling();

            // 누름 영역: 보이지 않는 그래픽
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            RectTransform mark = NewUI("Mark", rect);
            mark.sizeDelta = Vector2.zero;
            var strokes = new Graphic[2];
            float[] angles = { 45f, -45f };
            for (int i = 0; i < angles.Length; i++)
            {
                RectTransform stroke = NewUI("Stroke", mark);
                stroke.sizeDelta = new Vector2(StrokeLength, StrokeThickness);
                stroke.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                Image image = stroke.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                strokes[i] = image;
            }

            UICloseButton close = rect.gameObject.AddComponent<UICloseButton>();
            var serialized = new SerializedObject(close);
            serialized.FindProperty("mark").objectReferenceValue = mark;
            SerializedProperty strokeArray = serialized.FindProperty("strokes");
            strokeArray.arraySize = strokes.Length;
            for (int i = 0; i < strokes.Length; i++)
                strokeArray.GetArrayElementAtIndex(i).objectReferenceValue = strokes[i];
            serialized.FindProperty("lockCursorOnClose").boolValue = lockCursorOnClose;
            serialized.FindProperty("inset").floatValue = CenterInset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (Graphic stroke in strokes)
                stroke.color = serialized.FindProperty("normalColor").colorValue;

            return button;
        }

        private static void InstallMission()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UICanvasPath);
            try
            {
                Transform window = FindDeep(root.transform, "JournalWindow");
                MissionUI mission = window != null ? window.GetComponentInParent<MissionUI>(true) : null;
                if (mission == null)
                {
                    Debug.LogError("[CloseButton] UI Canvas 프리팹에서 MissionUI 아래 JournalWindow를 찾지 못했습니다.");
                    return;
                }

                Button button = Create((RectTransform)window);
                UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(mission.CloseUI));

                // 제목줄의 글자 닫기 버튼은 X로 대신한다
                Transform exit = window.Find("TitleBar/ExitButton");
                if (exit != null) exit.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, UICanvasPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallChipPanel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ChipPanelPath);
            try
            {
                if (root.TryGetComponent(out AugmentationChipPanel panel) == false)
                {
                    Debug.LogError("[CloseButton] Augmentation Chip Panel 프리팹 루트에 AugmentationChipPanel이 없습니다.");
                    return;
                }

                Button button = Create((RectTransform)root.transform, true);
                UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(panel.Close));

                // 우상단 칩 수 글자의 오른쪽 끝을 X 누름 영역 왼쪽으로 민다
                if (root.transform.Find("Counts") is RectTransform counts)
                {
                    float right = (CenterInset + HitSize * 0.5f + 16f) * 2f;
                    counts.anchoredPosition = new Vector2(-right, counts.anchoredPosition.y);
                }

                PrefabUtility.SaveAsPrefabAsset(root, ChipPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 타이틀 씬이 열려 있으면 환경설정 창의 X도 바꾼다(빌더로 새로 만들 때는 빌더가 Create를 부른다).
        private static void InstallOpenSettings()
        {
            TitleSettingsPopup popup = Object.FindFirstObjectByType<TitleSettingsPopup>(FindObjectsInactive.Include);
            if (popup == null) return;

            var window = (RectTransform)popup.transform;
            Transform oldButton = window.Find("Close Button");
            if (oldButton != null) Object.DestroyImmediate(oldButton.gameObject);
            Button button = Create(window);

            var serialized = new SerializedObject(popup);
            serialized.FindProperty("closeButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
            Debug.Log("[CloseButton] 열린 씬의 환경설정 창 X를 바꿨습니다. 씬을 저장해 주세요.");
        }

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
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
