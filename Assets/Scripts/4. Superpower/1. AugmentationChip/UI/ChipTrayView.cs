using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    // 정렬 버튼의 기준. 프리팹에 번호로 저장되지만 버튼 구성과 순서는 Build Layout이 다시 맞춘다
    public enum ChipSortKey { Recent, Type, Size, Name }

    // 트레이에 보여줄 칩 묶음. 스킬 칩은 분류(SkillCategory)별, 능력치 칩은 하나로 묶는다
    // 프리팹에 번호로 저장되므로 새 값은 맨 뒤에 붙인다
    public enum ChipFilter { Attack, Defense, Utility, Stat, Passive }

    /// <summary>
    /// 보유 칩 트레이. 장착되지 않은 칩을 정렬 버튼 기준으로 정렬해서 스크롤 영역에 배치한다.
    /// 필터 토글은 모두 켜진 채(전체) 시작하고, 켜진 묶음의 칩만 보여준다. 모두 끄면 아무 칩도 보이지 않는다.
    /// 전체 토글은 "필터가 모두 켜져 있음"을 체크로 보여준다. 켜고 끄면 필터를 모두 같이 켜고 끈다.
    /// 유형별 필터 글자 뒤에는 그 유형의 칩 색 음영을 깔아 칩 색과 필터를 이어 보이게 한다.
    /// 위쪽 오른쪽 검색 칸(ChipSearchField)에 글자를 치면 필터를 통과한 칩 가운데 이름이 맞는 칩만 보여준다.
    /// 검색 칸의 자동 완성 후보도 필터를 통과한, 트레이에 있는 칩 이름이다. 검색 칸은 처음 깨어날 때 정렬 버튼을 복제해 만든다.
    /// 트레이 크기와 필터 토글/정렬 버튼/스크롤 영역 배치는 에디터에서 AugmentationChipLayoutBuilder가 정해 둔다.
    /// </summary>
    public class ChipTrayView : MonoBehaviour
    {
        [Serializable]
        private class SortButton
        {
            public ChipSortKey key;
            public Button button;
            public Text text;
            public string label;
        }

        [Serializable]
        private class FilterToggle
        {
            public ChipFilter filter;
            public Toggle toggle;
            public Text text;
        }

        [SerializeField] private RectTransform content;
        [SerializeField] private FilterToggle[] filterToggles; // 빠진 필터는 Build Layout이 만든다
        [SerializeField] private Toggle allToggle; // 필터 토글 맨 앞. 없으면 Build Layout이 만든다
        [SerializeField] private Text allLabel;
        [SerializeField] private SortButton[] sortButtons; // 기준과 순서는 Build Layout이 맞춘다
        // content 안쪽 여백이자 칩 사이 간격
        [SerializeField] private float chipSpacing = 36f;

        private const string TitleName = "TrayTitle"; // "보유 칩" 제목. 검색 칸을 이 줄에 둔다
        // 필터 글자 뒤 유형 색 음영 (칩 캔버스 3840x2160 기준)
        private const float FilterShadeAlpha = 0.75f;
        private const float FilterShadePaddingX = 10f; // 글자 ~ 음영 왼쪽·오른쪽 끝. 글자는 체크 상자에서 28 떨어져 있어 음영과 상자 사이가 18 빈다
        private const float FilterShadePaddingY = 6f;  // 글자 크기 ~ 음영 위·아래 끝

        private readonly List<ChipItemView> chips = new();
        private ChipSortKey sortKey = ChipSortKey.Recent; // 방금 얻은 칩을 찾기 쉽게 최신순으로 시작한다
        private bool sortAscending = true;
        // 처음 깨어날 때 만든다. 패널은 트레이가 깨어나기 전에 Layout을 먼저 부를 수 있으므로 비어 있을 수 있다
        private ChipSearchField search;

        public RectTransform Content => content;
        // "보유 칩" 제목. 검색 칸이 이 줄 오른쪽에 붙고, 패널은 같은 높이에 그리드 쪽 "장착 칩" 제목 줄을 만든다
        public Text TitleText
        {
            get
            {
                Transform title = transform.Find(TitleName);
                return title != null ? title.GetComponent<Text>() : null;
            }
        }
        // 전체 해제 버튼처럼 정렬 버튼과 같은 생김새가 필요한 곳이 복제해 간다
        public Button SortButtonTemplate => sortButtons.Length > 0 ? sortButtons[0].button : null;

        // 아래는 AugmentationChipLayoutBuilder가 트레이 크기와 정렬 버튼을 배치할 때 읽는다
        public float ChipSpacing => chipSpacing;
        public int SortButtonCount => sortButtons.Length;
        public RectTransform GetSortButtonRect(int index) => (RectTransform)sortButtons[index].button.transform;
        public RectTransform GetSortLabelRect(int index) => sortButtons[index].text.rectTransform;
        public int FilterToggleCount => filterToggles.Length;
        public Toggle GetFilterToggle(int index) => filterToggles[index].toggle;
        public RectTransform GetFilterLabelRect(int index) => filterToggles[index].text.rectTransform;
        public Toggle AllToggle => allToggle;
        public RectTransform AllLabelRect => allLabel.rectTransform;

        private void Awake()
        {
            foreach (SortButton sortButton in sortButtons)
            {
                ChipSortKey key = sortButton.key;
                sortButton.button.onClick.AddListener(() => OnClickSort(key));
            }

            // 필터는 모두 켜진 채(전체) 시작한다. 프리팹에 꺼진 채 저장돼 있어도 여기서 켠다
            // 토글을 켜고 끌 때마다 보이는 칩을 다시 배치한다
            foreach (FilterToggle filterToggle in filterToggles)
            {
                filterToggle.toggle.isOn = true;
                filterToggle.toggle.onValueChanged.AddListener(OnFilterChanged);
            }

            if (allToggle != null)
            {
                allToggle.isOn = true;
                allToggle.onValueChanged.AddListener(OnAllChanged);
            }

            RefreshSortLabels();
            CreateFilterShades();
            CreateSearch();
        }

        // 필터 토글 글자 뒤에 그 유형의 칩 색 음영을 깐다. 글자 폭에 맞춰 세로 가운데에 두고, 누르는 것은 그대로 토글이 받는다
        private void CreateFilterShades()
        {
            foreach (FilterToggle filterToggle in filterToggles)
            {
                Text label = filterToggle.text;
                if (label == null)
                    continue;

                RectTransform labelRect = label.rectTransform;
                GameObject go = new("Shade", typeof(RectTransform), typeof(Image));
                go.layer = label.gameObject.layer;
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(labelRect.parent, false);
                rect.SetSiblingIndex(labelRect.GetSiblingIndex()); // 글자 바로 뒤에 그린다

                // 글자는 칸 왼쪽 가운데 정렬이므로 칸 왼쪽 끝과 세로 가운데를 기준으로 글자 폭만큼 덮는다
                Vector2 size = labelRect.sizeDelta;
                Vector2 position = labelRect.anchoredPosition;
                rect.anchorMin = labelRect.anchorMin;
                rect.anchorMax = labelRect.anchorMax;
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(position.x - labelRect.pivot.x * size.x - FilterShadePaddingX,
                                                    position.y + (0.5f - labelRect.pivot.y) * size.y);
                rect.sizeDelta = new Vector2(Mathf.Min(label.preferredWidth, size.x) + FilterShadePaddingX * 2f,
                                             label.fontSize + FilterShadePaddingY * 2f);

                Image shade = go.GetComponent<Image>();
                shade.color = GetFilterColor(filterToggle.filter);
                shade.raycastTarget = false;
            }
        }

        // 칩 칸 스프라이트(Resources/AugmentationChip/ChipUnit_*) 가운데 색에 음영 투명도를 곱한다
        private static Color GetFilterColor(ChipFilter filter)
        {
            Color color = filter switch
            {
                ChipFilter.Attack => new Color(0.51f, 0.196f, 0.208f),   // ChipUnit_Red
                ChipFilter.Defense => new Color(0.227f, 0.239f, 0.439f), // ChipUnit_Blue
                ChipFilter.Utility => new Color(0.51f, 0.51f, 0.208f),   // ChipUnit_Yellow
                ChipFilter.Passive => new Color(0.369f, 0.188f, 0.486f), // ChipUnit_Purple
                ChipFilter.Stat => new Color(0.086f, 0.086f, 0.102f),    // ChipUnit_Black
                _ => Color.clear,
            };
            color.a *= FilterShadeAlpha;
            return color;
        }

        // 검색 칸을 "보유 칩" 제목 줄 오른쪽에, 오른쪽 끝을 정렬 버튼 줄 오른쪽 끝에 맞춰 만든다
        private void CreateSearch()
        {
            if (sortButtons.Length == 0)
                return;

            RectTransform lastSortButton = GetSortButtonRect(sortButtons.Length - 1);
            float right = lastSortButton.anchoredPosition.x + lastSortButton.sizeDelta.x;
            search = ChipSearchField.Create((RectTransform)transform, sortButtons[0].button, TitleText, right, GetSearchCandidates);
            search.QueryChanged += _ => Layout();
        }

        // 패널을 다시 열 때 지난번 검색어를 지운다
        public void ResetSearch()
        {
            if (search != null)
                search.Clear();
        }

        public void Add(ChipItemView chip)
        {
            if (chips.Contains(chip) == false)
                chips.Add(chip);
        }

        public void Remove(ChipItemView chip) => chips.Remove(chip);

        // 패널을 열 때 칩 뷰를 새로 만들기 전에 비운다
        public void Clear() => chips.Clear();

        // 트레이에 남아있는 칩을 정렬해서 왼쪽부터 채우고, 폭을 넘으면 다음 줄로 내린다
        // 필터에 걸러진 칩은 트레이 목록에 남겨 둔 채 숨긴다
        public void Layout()
        {
            chips.Sort(Compare);

            float contentWidth = content.sizeDelta.x;
            Vector2 cursor = new Vector2(chipSpacing, -chipSpacing);
            float rowHeight = 0f;
            foreach (ChipItemView chip in chips)
            {
                bool shown = IsShown(chip.Owned.Def);
                chip.gameObject.SetActive(shown);
                if (shown == false)
                    continue;

                float chipWidth = ChipGridMetrics.Length(chip.Width);
                float chipHeight = ChipGridMetrics.Length(chip.Height);

                if (cursor.x > chipSpacing && cursor.x + chipWidth > contentWidth - chipSpacing)
                {
                    cursor = new Vector2(chipSpacing, cursor.y - rowHeight - chipSpacing);
                    rowHeight = 0f;
                }

                chip.Rect.SetParent(content, false);
                chip.Rect.anchoredPosition = cursor;
                cursor.x += chipWidth + chipSpacing;
                rowHeight = Mathf.Max(rowHeight, chipHeight);
            }

            // 마지막 줄까지 포함한 높이로 content 크기를 맞춰야 스크롤 범위가 정해진다
            float contentHeight = -cursor.y + rowHeight + chipSpacing;
            content.sizeDelta = new Vector2(contentWidth, contentHeight);
        }

        private void OnClickSort(ChipSortKey key)
        {
            // 같은 키를 다시 누르면 오름/내림차순 전환
            sortAscending = (sortKey != key) || !sortAscending;
            sortKey = key;

            RefreshSortLabels();
            Layout();
        }

        // 필터를 바꾸면 전체 토글은 모든 필터가 켜져 있을 때만 켜 둔다
        private void OnFilterChanged(bool isOn)
        {
            if (allToggle != null)
                allToggle.SetIsOnWithoutNotify(AllFiltersOn());
            Layout();
        }

        // 전체를 켜고 끄면 필터를 모두 같이 켜고 끈다
        private void OnAllChanged(bool isOn)
        {
            foreach (FilterToggle filterToggle in filterToggles)
                filterToggle.toggle.SetIsOnWithoutNotify(isOn);
            Layout();
        }

        private bool AllFiltersOn()
        {
            foreach (FilterToggle filterToggle in filterToggles)
                if (filterToggle.toggle.isOn == false)
                    return false;
            return true;
        }

        // 켜진 묶음의 칩 가운데 이름이 검색어와 맞는 칩만 보여준다
        private bool IsShown(AugmentationChipDef def) => IsFilterOn(def) && (search == null || search.Matches(def.Name));

        // 자동 완성 후보: 지금 트레이에 있고 필터를 통과한 칩의 이름 (같은 칩을 여러 장 가져도 한 번)
        private IEnumerable<string> GetSearchCandidates()
        {
            HashSet<string> names = new();
            foreach (ChipItemView chip in chips)
            {
                if (IsFilterOn(chip.Owned.Def))
                    names.Add(chip.Owned.Def.Name);
            }
            return names;
        }

        private bool IsFilterOn(AugmentationChipDef def)
        {
            ChipFilter filter = GetFilter(def);
            foreach (FilterToggle filterToggle in filterToggles)
                if (filterToggle.filter == filter)
                    return filterToggle.toggle.isOn;
            return true;
        }

        private static ChipFilter GetFilter(AugmentationChipDef def)
        {
            if (def is StatChipDef)
                return ChipFilter.Stat;

            SkillChipDef skill = (SkillChipDef)def;
            return skill.Category switch
            {
                SkillCategory.Attack => ChipFilter.Attack,
                SkillCategory.Defense => ChipFilter.Defense,
                SkillCategory.Utility => ChipFilter.Utility,
                SkillCategory.Passive => ChipFilter.Passive,
                _ => throw new ArgumentOutOfRangeException(nameof(def), skill.Category, "필터가 없는 스킬 분류입니다."),
            };
        }

        private void RefreshSortLabels()
        {
            foreach (SortButton sortButton in sortButtons)
            {
                string arrow = (sortButton.key == sortKey) ? (sortAscending ? " ▲" : " ▼") : string.Empty;
                sortButton.text.text = sortButton.label + arrow;
            }
        }

        private int Compare(ChipItemView a, ChipItemView b)
        {
            AugmentationChipDef x = a.Owned.Def;
            AugmentationChipDef y = b.Owned.Def;
            int result = sortKey switch
            {
                // 최신순은 ▲(오름차순)일 때 나중에 얻은 칩이 앞에 온다
                ChipSortKey.Recent => b.Owned.AcquiredOrder.CompareTo(a.Owned.AcquiredOrder),
                ChipSortKey.Type => CompareType(x, y),
                ChipSortKey.Size => x.CellCount.CompareTo(y.CellCount),
                ChipSortKey.Name => string.CompareOrdinal(x.Name, y.Name),
                _ => 0,
            };

            // 값이 같으면 ID 순으로 고정
            if (result == 0)
                result = x.ID.CompareTo(y.ID);

            return sortAscending ? result : -result;
        }

        // 종류 정렬은 능력치 칩끼리, 스킬 칩끼리 먼저 모은다
        private static int CompareType(AugmentationChipDef x, AugmentationChipDef y)
        {
            int result = KindOrder(x).CompareTo(KindOrder(y));
            if (result != 0)
                return result;

            if (x is StatChipDef statX && y is StatChipDef statY)
                return statX.StatType.CompareTo(statY.StatType);

            // 스킬 칩은 공격/방어/유틸/패시브로 먼저 묶고, 같은 분류 안에서 스킬 종류 순
            if (x is SkillChipDef skillX && y is SkillChipDef skillY)
            {
                result = skillX.Category.CompareTo(skillY.Category);
                if (result != 0)
                    return result;

                return skillX.SkillType.CompareTo(skillY.SkillType);
            }

            return 0;
        }

        // 능력치 칩을 스킬 칩보다 앞에 둔다
        private static int KindOrder(AugmentationChipDef def) => def is StatChipDef ? 0 : 1;
    }
}
