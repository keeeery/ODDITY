using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    public enum ChipSortKey { ID, Name, StatType, Value, Size }

    /// <summary>
    /// 보유 칩 트레이. 장착되지 않은 칩을 정렬 버튼 기준으로 정렬해서 스크롤 영역에 배치한다.
    /// 트레이 크기와 정렬 버튼/스크롤 영역 배치는 에디터에서 AugmentationChipLayoutBuilder가 정해 둔다.
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

        [SerializeField] private RectTransform content;
        [SerializeField] private SortButton[] sortButtons;
        // content 안쪽 여백이자 칩 사이 간격
        [SerializeField] private float chipSpacing = 12f;

        private readonly List<ChipItemView> chips = new();
        private ChipSortKey sortKey = ChipSortKey.ID;
        private bool sortAscending = true;

        public RectTransform Content => content;

        // 아래는 AugmentationChipLayoutBuilder가 트레이 크기와 정렬 버튼을 배치할 때 읽는다
        public float ChipSpacing => chipSpacing;
        public int SortButtonCount => sortButtons.Length;
        public RectTransform GetSortButtonRect(int index) => (RectTransform)sortButtons[index].button.transform;
        public RectTransform GetSortLabelRect(int index) => sortButtons[index].text.rectTransform;

        private void Awake()
        {
            foreach (SortButton sortButton in sortButtons)
            {
                ChipSortKey key = sortButton.key;
                sortButton.button.onClick.AddListener(() => OnClickSort(key));
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
        public void Layout()
        {
            chips.Sort(Compare);

            float contentWidth = content.sizeDelta.x;
            Vector2 cursor = new Vector2(chipSpacing, -chipSpacing);
            float rowHeight = 0f;
            foreach (ChipItemView chip in chips)
            {
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

            if (x is SkillChipDef skillX && y is SkillChipDef skillY)
                return skillX.SkillType.CompareTo(skillY.SkillType);

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
