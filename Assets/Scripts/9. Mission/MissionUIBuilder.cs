using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#endif

namespace Study.Utilities
{
    /// <summary>
    /// 미션(Journal) 화면을 코드만으로 조립하는 에디터 툴.
    /// 빈 GameObject 에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
    /// </summary>
    public class MissionUIBuilder : MonoBehaviour
    {
        // 한글 폰트 교체용. 비워두면 TMP 기본 폰트를 사용한다.
        [SerializeField] private TMP_FontAsset font;

#if UNITY_EDITOR
        private const string RootName = "Mission UI";
        private static readonly Vector2 ReferenceResolution = new Vector2(3840f, 2160f);

        // 상단 바
        private const float TitleBarHeight = 240f;
        private const float TitleDividerHeight = 6f;
        private const float ExitButtonSize = 160f;
        private const float ExitButtonMargin = 40f;

        // 본문 영역
        private const float ContentPadding = 30f;
        private const float QuestListWidth = 1290f;
        private const float PanelGap = 30f;
        private const float ScrollbarWidth = 66f;

        // 상세 패널 하단 (설명 스크롤 + 수락 버튼)
        private const float DescriptionScrollbarWidth = 40f;
        private const float AcceptButtonWidth = 800f;
        private const float AcceptButtonHeight = 160f;
        private const float AcceptButtonBottom = 80f;
        private const float AcceptButtonGap = 50f;

        // 퀘스트 목록 (탭 → 목록 헤더 → 분류별 접이식 목록)
        private const float QuestTabHeight = 100f;
        private const float QuestTabWidth = 280f;
        private const float QuestListHeaderHeight = 120f;
        private const float CategoryHeaderHeight = 96f;
        private const float QuestRowHeight = 110f;
        private const float QuestRowSpacing = 6f;
        private const float QuestIconSize = 64f;
        private const int ListPadding = 12;

        #region Colors

        private static readonly Color WindowBackground = new Color(0.12f, 0.13f, 0.15f, 0.98f);
        private static readonly Color FrameGold = new Color(0.55f, 0.43f, 0.25f, 1f);
        private static readonly Color TitleBarColor = new Color(0.11f, 0.21f, 0.35f, 1f);
        private static readonly Color TitleTextColor = new Color(0.95f, 0.90f, 0.78f, 1f);
        private static readonly Color ExitButtonColor = new Color(0.55f, 0.12f, 0.07f, 1f);
        private static readonly Color AcceptButtonColor = new Color(0.16f, 0.36f, 0.20f, 1f);
        private static readonly Color DetailTitleBarColor = new Color(0.17f, 0.16f, 0.15f, 1f);
        private static readonly Color PanelInner = new Color(0.10f, 0.11f, 0.12f, 1f);
        private static readonly Color QuestListBackground = new Color(0.36f, 0.36f, 0.36f, 1f);
        private static readonly Color QuestTabNormal = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color QuestTabSelected = new Color(0.96f, 0.62f, 0.16f, 1f);
        private static readonly Color QuestListHeaderColor = new Color(0.10f, 0.47f, 0.82f, 1f);
        private static readonly Color QuestListHeaderBorder = new Color(0.75f, 0.88f, 1f, 1f);
        private static readonly Color CategoryHeaderColor = new Color(0.45f, 0.33f, 0.20f, 1f);
        private static readonly Color QuestRowColor = new Color(0.68f, 0.56f, 0.38f, 1f);
        private static readonly Color QuestRowBorder = new Color(0.50f, 0.40f, 0.26f, 1f);
        private static readonly Color QuestStateIconColor = new Color(0.45f, 0.76f, 0.15f, 1f);
        private static readonly Color MainTextColor = new Color(0.88f, 0.86f, 0.82f, 1f);
        private static readonly Color SubTextColor = new Color(0.62f, 0.60f, 0.56f, 1f);
        private static readonly Color IllustrationPlaceholder = new Color(0.22f, 0.27f, 0.38f, 1f);
        private static readonly Color ScrollTrackColor = new Color(0.08f, 0.08f, 0.09f, 1f);
        private static readonly Color ScrollHandleColor = new Color(0.45f, 0.36f, 0.24f, 1f);

        #endregion

        #region Sample Data

        private static readonly string[] QuestTabLabels = { "시작가능", "진행중", "완료" };
        private const int SelectedQuestTabIndex = 2;

        private const string QuestListTitle = "완료 퀘스트";
        private const string QuestListCount = "513 개";

        // (퀘스트 묶음 이름, 퀘스트 수)
        private static readonly (string name, int count)[] JobQuests =
        {
            ("1차 전직", 3),
            ("2차 전직", 5),
        };

        private static readonly (string name, int count)[] MainQuests =
        {
            ("연합 가입", 7),
            ("루타비스", 7),
            ("소멸의 여로", 21),
            ("리버스 시티", 20),
            ("츄츄 아일랜드", 26),
            ("얌얌 아일랜드", 26),
            ("레헬른", 33),
            ("신비의 숲 아르카나", 27),
            ("기억의 늪 모라스", 36),
            ("태초의 바다 에스페라", 26),
            ("셀라스", 21),
        };

        private const string DetailTitle = "The Sundering";

        private const string DetailDescription =
            "When old portals to the <b><color=#F0C060>Anderworld</color></b> open and strange creatures cross over, " +
            "the <b><color=#F0C060>Alliance</color></b> gathers to fight.\n\n" +
            "<b><color=#F0C060>Dragonknights, Spellweavers, Rangers</color></b> and <b><color=#F0C060>Steam Mechanicus</color></b>' " +
            "have not forgotten the <b><color=#F0C060>enemy of old</color></b> whose minions return from that place of chaos and change.";

        #endregion

        #region Menu

        [ContextMenu("1. Build UI")]
        private void BuildUI()
        {
            if (FindRoot() != null)
            {
                Debug.LogWarning($"[MissionUIBuilder] '{RootName}' 이(가) 이미 있습니다. 먼저 '2. Clear UI' 를 실행하세요.", this);
                return;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                Debug.LogWarning("[MissionUIBuilder] 씬에 EventSystem 이 없습니다. 버튼 입력을 받으려면 추가하세요.", this);

            Transform canvas = CreateCanvas();
            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Build Mission UI");

            Transform window = CreateJournalWindow(canvas);
            CreateTitleBar(window);

            Transform page = CreateMissionPage(window);
            CreateQuestList(page);
            CreateDetailPanel(page);

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[MissionUIBuilder] '{RootName}' 생성 완료.", canvas);
        }

        [ContextMenu("2. Clear UI")]
        private void ClearUI()
        {
            int count = 0;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != RootName)
                    continue;

                Undo.DestroyObjectImmediate(root);
                count++;
            }

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                Debug.Log($"[MissionUIBuilder] '{RootName}' {count}개 삭제.", this);
            }
            else
            {
                Debug.Log($"[MissionUIBuilder] 삭제할 '{RootName}' 이(가) 없습니다.", this);
            }
        }

        // 이미 만들어진 UI 에서 DescriptionText 를 스크롤 영역으로 바꾸고 수락 버튼을 추가한다.
        // (전체를 다시 빌드하지 않아 다른 오브젝트의 참조가 유지된다.)
        [ContextMenu("3. Add Description Scroll & Accept Button")]
        private void UpgradeDetailPanel()
        {
            GameObject root = FindRoot();
            Transform detail = root != null ? root.transform.Find("JournalWindow/MissionPage/DetailPanel") : null;
            if (detail == null)
            {
                Debug.LogWarning("[MissionUIBuilder] DetailPanel 을 찾을 수 없습니다. 먼저 '1. Build UI' 를 실행하세요.", this);
                return;
            }

            foreach (string childName in new[] { "DescriptionText", "DescriptionScrollView", "AcceptButton" })
            {
                Transform old = detail.Find(childName);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
            }

            CreateDescriptionArea(detail, out GameObject scrollView, out GameObject acceptButton);
            Undo.RegisterCreatedObjectUndo(scrollView, "Add Description Scroll");
            Undo.RegisterCreatedObjectUndo(acceptButton, "Add Accept Button");

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("[MissionUIBuilder] 설명 스크롤 · 수락 버튼 추가 완료.", detail);
        }

        private GameObject FindRoot()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name == RootName)
                    return root;
            }

            return null;
        }

        #endregion

        #region Canvas

        private Transform CreateCanvas()
        {
            GameObject go = new GameObject(RootName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            MoveToBuilderScene(go);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        // 빌더가 붙은 오브젝트와 같은 씬에 루트를 둔다. (멀티 씬 편집 대비)
        private void MoveToBuilderScene(GameObject go)
        {
            if (go.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        }

        #endregion

        #region Journal Window

        private Transform CreateJournalWindow(Transform canvas)
        {
            // 화면 꽉 채움
            GameObject window = NewPanel("JournalWindow", canvas, Vector2.zero);
            window.GetComponent<Image>().color = WindowBackground;
            Stretch(window.GetComponent<RectTransform>(), 0f);

            return window.transform;
        }

        private void CreateTitleBar(Transform window)
        {
            // 상단 가로 띠: 본문과 구분되도록 남색 + 아래쪽 금색 구분선
            GameObject titleBar = NewPanel("TitleBar", window, Vector2.zero);
            titleBar.GetComponent<Image>().color = TitleBarColor;
            SetRect(titleBar.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, TitleBarHeight));

            GameObject divider = NewPanel("TitleDivider", titleBar.transform, Vector2.zero);
            divider.GetComponent<Image>().color = FrameGold;
            divider.GetComponent<Image>().raycastTarget = false;
            SetRect(divider.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, TitleDividerHeight));

            TMP_Text title = NewText("TitleText", titleBar.transform, "Journal", 96f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.color = TitleTextColor;
            Stretch(title.rectTransform, 0f);

            CreateExitButton(titleBar.transform);
        }

        private void CreateExitButton(Transform titleBar)
        {
            // 우측 상단 나가기 버튼
            Button exit = NewButton("ExitButton", titleBar, "Exit", out TMP_Text label);
            exit.GetComponent<Image>().color = ExitButtonColor;
            AddFrame(exit.gameObject, FrameGold, 4f);
            SetRect(exit.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-ExitButtonMargin, 0f), new Vector2(ExitButtonSize, ExitButtonSize));

            label.fontSize = 52f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
        }

        // 상단 바 아래 영역을 꽉 채움
        private Transform CreateMissionPage(Transform window)
        {
            GameObject page = NewUI("MissionPage", window);
            float top = TitleBarHeight + ContentPadding;
            SetRect(page.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, (ContentPadding - top) * 0.5f),
                new Vector2(-ContentPadding * 2f, -(top + ContentPadding)));

            return page.transform;
        }

        #endregion

        #region Quest List (좌측)

        private void CreateQuestList(Transform page)
        {
            // 좌측 세로 패널: 왼쪽 가장자리에 붙고 위아래로 늘어난다.
            GameObject listPanel = NewPanel("QuestListPanel", page, Vector2.zero);
            listPanel.GetComponent<Image>().color = QuestListBackground;
            AddFrame(listPanel, FrameGold, 4f);
            SetRect(listPanel.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(QuestListWidth, 0f));

            CreateQuestTabBar(listPanel.transform);
            CreateQuestListHeader(listPanel.transform);
            Transform content = CreateQuestScrollView(listPanel.transform);

            // 분류별 목록. 시안처럼 '직업'은 접힌 상태, '메인'은 펼친 상태로 시작한다.
            GameObject jobItems = CreateQuestCategory("Job", content, "직업", JobQuests, false);
            CreateQuestCategory("Main", content, "메인", MainQuests, true);

            jobItems.SetActive(false);
        }

        private void CreateQuestTabBar(Transform listPanel)
        {
            // 상단 탭 (시작가능 / 진행중 / 완료)
            GameObject tabBar = NewUI("QuestTabBar", listPanel);
            SetRect(tabBar.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -ListPadding), new Vector2(-ListPadding * 2f, QuestTabHeight));

            HorizontalLayoutGroup layout = tabBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            string[] tabNames = { "AvailableTab", "InProgressTab", "CompletedTab" };
            for (int i = 0; i < QuestTabLabels.Length; i++)
            {
                bool selected = i == SelectedQuestTabIndex;

                Button tab = NewButton(tabNames[i], tabBar.transform, QuestTabLabels[i], out TMP_Text label);
                tab.GetComponent<Image>().color = selected ? QuestTabSelected : QuestTabNormal;
                tab.gameObject.AddComponent<LayoutElement>().preferredWidth = QuestTabWidth;

                label.fontSize = 52f;
                label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                label.color = Color.white;
            }
        }

        private void CreateQuestListHeader(Transform listPanel)
        {
            // 탭 아래 파란 띠: 왼쪽 목록 이름, 오른쪽 퀘스트 개수
            GameObject header = NewPanel("QuestListHeader", listPanel, Vector2.zero);
            header.GetComponent<Image>().color = QuestListHeaderColor;
            AddFrame(header, QuestListHeaderBorder, 3f);
            SetRect(header.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(ListPadding + QuestTabHeight)), new Vector2(-ListPadding * 2f, QuestListHeaderHeight));

            TMP_Text title = NewText("QuestListTitleText", header.transform, QuestListTitle, 64f, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            SetRect(title.rectTransform,
                new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(0f, 0.5f),
                new Vector2(40f, 0f), new Vector2(-40f, 0f));

            TMP_Text count = NewText("QuestCountText", header.transform, QuestListCount, 60f, TextAlignmentOptions.MidlineRight);
            count.fontStyle = FontStyles.Bold;
            count.color = Color.white;
            SetRect(count.rectTransform,
                new Vector2(0.6f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-40f, 0f), new Vector2(-40f, 0f));
        }

        private Transform CreateQuestScrollView(Transform listPanel)
        {
            // 목록 헤더 아래 ~ 패널 하단
            float top = ListPadding + QuestTabHeight + QuestListHeaderHeight + 12f;

            GameObject scrollView = NewUI("QuestScrollView", listPanel);
            SetRect(scrollView.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, (ListPadding - top) * 0.5f),
                new Vector2(-ListPadding * 2f, -(top + ListPadding)));

            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 80f;

            GameObject viewport = NewUI("Viewport", scrollView.transform);
            viewport.AddComponent<RectMask2D>();
            SetRect(viewport.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(-(ScrollbarWidth + 10f), 0f));

            GameObject content = NewUI("Content", viewport.transform);
            SetRect(content.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);

            AddVerticalLayout(content, QuestRowSpacing);

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scrollbar scrollbar = CreateVerticalScrollbar(scrollView.transform, ScrollbarWidth);

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = content.GetComponent<RectTransform>();
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            return content.transform;
        }

        // 분류 헤더 + 퀘스트 행 묶음. 반환값은 접고 펼 대상인 행 묶음(QuestCategoryItems_xxx).
        private GameObject CreateQuestCategory(string id, Transform content, string label,
            (string name, int count)[] quests, bool expanded)
        {
            GameObject group = NewUI($"QuestCategory_{id}", content);
            AddVerticalLayout(group, QuestRowSpacing);

            // 접이식 헤더 (펼침 ▲ / 접힘 ▼)
            string arrow = expanded ? "▲" : "▼";
            Button header = NewButton($"QuestCategoryHeader_{id}", group.transform, $"{arrow} {label}", out TMP_Text text);
            header.GetComponent<Image>().color = CategoryHeaderColor;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = CategoryHeaderHeight;

            text.fontSize = 56f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            SetRect(text.rectTransform,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                new Vector2(30f, 0f), new Vector2(-60f, 0f));

            GameObject items = NewUI($"QuestCategoryItems_{id}", group.transform);
            AddVerticalLayout(items, QuestRowSpacing);

            for (int i = 0; i < quests.Length; i++)
                CreateQuestRow($"QuestRow_{id}_{i + 1:00}", items.transform, quests[i]);

            return items;
        }

        private void CreateQuestRow(string name, Transform parent, (string name, int count) data)
        {
            GameObject row = NewPanel(name, parent, Vector2.zero);
            row.GetComponent<Image>().color = QuestRowColor;
            AddFrame(row, QuestRowBorder, 2f);
            row.AddComponent<Button>().targetGraphic = row.GetComponent<Image>();
            row.AddComponent<LayoutElement>().preferredHeight = QuestRowHeight;

            // 좌측 초록 "+" 아이콘 (스프라이트 대신 색상 블록)
            GameObject icon = NewPanel("QuestStateIcon", row.transform, new Vector2(QuestIconSize, QuestIconSize));
            icon.GetComponent<Image>().color = QuestStateIconColor;
            icon.GetComponent<Image>().raycastTarget = false;
            SetRect(icon.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 0f), new Vector2(QuestIconSize, QuestIconSize));

            TMP_Text plus = NewText("PlusText", icon.transform, "+", 56f, TextAlignmentOptions.Center);
            plus.fontStyle = FontStyles.Bold;
            plus.color = Color.white;
            Stretch(plus.rectTransform, 0f);

            // "* 퀘스트 묶음 이름 (수)"
            const float textLeft = 24f + QuestIconSize + 24f;
            TMP_Text text = NewText("QuestNameText", row.transform, $"* {data.name} ({data.count})", 54f, TextAlignmentOptions.MidlineLeft);
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            SetRect(text.rectTransform,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                new Vector2(textLeft, 0f), new Vector2(-(textLeft + 24f), 0f));
        }

        private static void AddVerticalLayout(GameObject go, float spacing)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private Scrollbar CreateVerticalScrollbar(Transform parent, float width)
        {
            // 우측 가장자리 세로 스크롤바
            GameObject track = NewPanel("ScrollbarVertical", parent, Vector2.zero);
            track.GetComponent<Image>().color = ScrollTrackColor;
            SetRect(track.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(width, 0f));

            GameObject slidingArea = NewUI("SlidingArea", track.transform);
            Stretch(slidingArea.GetComponent<RectTransform>(), 6f);

            GameObject handle = NewPanel("Handle", slidingArea.transform, Vector2.zero);
            handle.GetComponent<Image>().color = ScrollHandleColor;
            Stretch(handle.GetComponent<RectTransform>(), 0f);

            Scrollbar scrollbar = track.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            return scrollbar;
        }

        #endregion

        #region Detail Panel (우측)

        private void CreateDetailPanel(Transform page)
        {
            // 우측 상세 패널: 좌측 목록 옆 남은 공간을 채운다.
            float left = QuestListWidth + PanelGap;

            GameObject detail = NewPanel("DetailPanel", page, Vector2.zero);
            detail.GetComponent<Image>().color = PanelInner;
            AddFrame(detail, FrameGold, 4f);
            SetRect(detail.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                new Vector2(left, 0f), new Vector2(-left, 0f));

            // 제목 띠
            const float titleHeight = 180f;
            GameObject titleBar = NewPanel("DetailTitleBar", detail.transform, Vector2.zero);
            titleBar.GetComponent<Image>().color = DetailTitleBarColor;
            SetRect(titleBar.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, titleHeight));

            TMP_Text title = NewText("DetailTitleText", titleBar.transform, DetailTitle, 88f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            Stretch(title.rectTransform, 0f);

            // 삽화 (스프라이트 대신 색상 블록)
            const float illustrationTop = titleHeight + 40f;
            const float illustrationHeight = 860f;

            GameObject illustration = NewPanel("IllustrationImage", detail.transform, Vector2.zero);
            illustration.GetComponent<Image>().color = IllustrationPlaceholder;
            illustration.GetComponent<Image>().raycastTarget = false;
            AddFrame(illustration, FrameGold, 3f);
            SetRect(illustration.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -illustrationTop), new Vector2(-80f, illustrationHeight));

            CreateDescriptionArea(detail.transform, out _, out _);
        }

        private void CreateDescriptionArea(Transform detail, out GameObject scrollView, out GameObject acceptButton)
        {
            // 설명 영역 (삽화 아래 ~ 수락 버튼 위). 상단 위치는 CreateDetailPanel 의 제목 띠 · 삽화 배치와 맞춘다.
            const float descriptionTop = 180f + 40f + 860f + 60f;
            const float descriptionBottom = AcceptButtonBottom + AcceptButtonHeight + AcceptButtonGap;

            scrollView = NewUI("DescriptionScrollView", detail);
            SetRect(scrollView.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, (descriptionBottom - descriptionTop) * 0.5f),
                new Vector2(-160f, -(descriptionTop + descriptionBottom)));

            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 80f;

            // 텍스트는 레이캐스트를 받지 않으므로, 휠 · 드래그 입력을 받을 투명 이미지를 뷰포트에 둔다.
            GameObject viewport = NewUI("Viewport", scrollView.transform);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            Stretch(viewport.GetComponent<RectTransform>(), 0f);

            // 설명 텍스트 = 스크롤 Content. 글 길이에 맞춰 높이가 늘어난다.
            TMP_Text description = NewText("DescriptionText", viewport.transform, DetailDescription, 64f, TextAlignmentOptions.TopLeft);
            description.color = MainTextColor;
            description.richText = true;
            description.lineSpacing = 10f;
            description.textWrappingMode = TextWrappingModes.Normal;
            SetRect(description.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);

            ContentSizeFitter fitter = description.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scrollbar scrollbar = CreateVerticalScrollbar(scrollView.transform, DescriptionScrollbarWidth);

            // 글이 짧으면 스크롤바를 숨기고 뷰포트를 넓힌다.
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = description.rectTransform;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 20f;

            // 미션 수락 버튼 (패널 하단 중앙)
            Button accept = NewButton("AcceptButton", detail, "Accept", out TMP_Text label);
            accept.GetComponent<Image>().color = AcceptButtonColor;
            AddFrame(accept.gameObject, FrameGold, 4f);
            SetRect(accept.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, AcceptButtonBottom), new Vector2(AcceptButtonWidth, AcceptButtonHeight));

            label.fontSize = 72f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;

            acceptButton = accept.gameObject;
        }

        #endregion

        #region Helpers

        private GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private GameObject NewPanel(string name, Transform parent, Vector2 size)
        {
            GameObject go = NewUI(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = PanelInner;
            return go;
        }

        private TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            GameObject go = NewUI(name, parent);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;

            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = MainTextColor;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button NewButton(string name, Transform parent, string label, out TMP_Text labelText)
        {
            GameObject go = NewPanel(name, parent, new Vector2(400f, 100f));
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            labelText = NewText("Label", go.transform, label, 40f, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 10f);
            return button;
        }

        private void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-padding * 2f, -padding * 2f);
        }

        // anchorMin · anchorMax · pivot · anchoredPosition · sizeDelta 를 한 번에 명시한다.
        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        // 금색 테두리 (외부 에셋 없이 Outline 효과로 표현)
        private static void AddFrame(GameObject go, Color color, float thickness)
        {
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
        }

        #endregion
#endif
    }
}
