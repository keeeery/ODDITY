using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    public enum ChipSortKey { ID, Name, StatType, Value, Size }

    // 트레이에 보여줄 칩 묶음. 스킬 칩은 분류(SkillCategory)별, 능력치 칩은 하나로 묶는다
    // 프리팹에 번호로 저장되므로 새 값은 맨 뒤에 붙인다
    public enum ChipFilter { Attack, Defense, Utility, Stat, Passive }

    /// <summary>
    /// 보유 칩 트레이. 장착되지 않은 칩을 정렬 버튼 기준으로 정렬해서 스크롤 영역에 배치한다.
    /// 필터 토글은 모두 꺼진 채 시작하고 그때는 모든 칩을 보여준다. 하나라도 켜면 켜진 묶음의 칩만 보여준다.
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
        [SerializeField] private FilterToggle[] filterToggles; // 비워두면 Build Layout이 만든다
        [SerializeField] private SortButton[] sortButtons;
        // content 안쪽 여백이자 칩 사이 간격
        [SerializeField] private float chipSpacing = 36f;

        private readonly List<ChipItemView> chips = new();
        private ChipSortKey sortKey = ChipSortKey.ID;
        private bool sortAscending = true;

        public RectTransform Content => content;

        // 아래는 AugmentationChipLayoutBuilder가 트레이 크기와 정렬 버튼을 배치할 때 읽는다
        public float ChipSpacing => chipSpacing;
        public int SortButtonCount => sortButtons.Length;
        public RectTransform GetSortButtonRect(int index) => (RectTransform)sortButtons[index].button.transform;
        public RectTransform GetSortLabelRect(int index) => sortButtons[index].text.rectTransform;
        public int FilterToggleCount => filterToggles.Length;
        public Toggle GetFilterToggle(int index) => filterToggles[index].toggle;
        public RectTransform GetFilterLabelRect(int index) => filterToggles[index].text.rectTransform;

        private void Awake()
        {
            foreach (SortButton sortButton in sortButtons)
            {
                ChipSortKey key = sortButton.key;
                sortButton.button.onClick.AddListener(() => OnClickSort(key));
            }

            // 필터는 모두 꺼진 채(모든 칩 보기) 시작한다. 프리팹에 켜진 채 저장돼 있어도 여기서 끈다
            // 토글을 켜고 끌 때마다 보이는 칩을 다시 배치한다
            foreach (FilterToggle filterToggle in filterToggles)
            {
                filterToggle.toggle.isOn = false;
                filterToggle.toggle.onValueChanged.AddListener(OnFilterChanged);
            }

            RefreshSortLabels();
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

        private void OnFilterChanged(bool isOn) => Layout();

        // 켜진 토글이 하나도 없으면 필터가 없는 것과 같으므로 모든 칩을 보여준다
        // 하나라도 켜져 있으면 켜진 묶음의 칩만 보여준다
        private bool IsShown(AugmentationChipDef def)
        {
            ChipFilter filter = GetFilter(def);
            bool anyOn = false;
            bool ownOn = false;
            foreach (FilterToggle filterToggle in filterToggles)
            {
                if (filterToggle.toggle.isOn == false)
                    continue;

                anyOn = true;
                if (filterToggle.filter == filter)
                    ownOn = true;
            }

            return anyOn == false || ownOn;
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
                ChipSortKey.Name => string.CompareOrdinal(x.Name, y.Name),
                ChipSortKey.StatType => CompareType(x, y),
                ChipSortKey.Value => CompareValue(x, y),
                ChipSortKey.Size => x.CellCount.CompareTo(y.CellCount),
                _ => 0,
            };

            // 값이 같으면 ID 순으로 고정
            if (result == 0)
                result = x.ID.CompareTo(y.ID);

            return sortAscending ? result : -result;
        }

        // 스탯/수치 정렬은 능력치 칩끼리, 스킬 칩끼리 먼저 모은다
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

        // 스킬 칩은 수치 대신 쿨타임으로 비교한다
        private static int CompareValue(AugmentationChipDef x, AugmentationChipDef y)
        {
            int result = KindOrder(x).CompareTo(KindOrder(y));
            if (result != 0)
                return result;

            if (x is StatChipDef statX && y is StatChipDef statY)
                return statX.Value.CompareTo(statY.Value);

            if (x is SkillChipDef skillX && y is SkillChipDef skillY)
                return skillX.Cooldown.CompareTo(skillY.Cooldown);

            return 0;
        }

        // 능력치 칩을 스킬 칩보다 앞에 둔다
        private static int KindOrder(AugmentationChipDef def) => def is StatChipDef ? 0 : 1;
    }
}
