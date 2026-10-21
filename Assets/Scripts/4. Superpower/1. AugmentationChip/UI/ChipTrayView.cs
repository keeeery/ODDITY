using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    public enum ChipSortKey { ID, Name, StatType, Value, Size }

    /// <summary>
    /// 보유 칩 트레이. 장착되지 않은 칩을 정렬 버튼 기준으로 정렬해서 스크롤 영역에 배치한다.
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
        [SerializeField] private float padding = 12f;

        private readonly List<ChipItemView> chips = new();
        private ChipSortKey sortKey = ChipSortKey.ID;
        private bool sortAscending = true;

        public Transform Content => content;

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

        // 트레이에 남아있는 칩을 정렬해서 왼쪽부터 채우고, 폭을 넘으면 다음 줄로 내린다
        public void Layout()
        {
            chips.Sort(Compare);

            float contentWidth = content.sizeDelta.x;
            Vector2 cursor = new Vector2(padding, -padding);
            float rowHeight = 0f;
            foreach (ChipItemView chip in chips)
            {
                float chipWidth = ChipGridMetrics.Length(chip.Chip.Width);
                float chipHeight = ChipGridMetrics.Length(chip.Chip.Height);

                if (cursor.x > padding && cursor.x + chipWidth > contentWidth - padding)
                {
                    cursor = new Vector2(padding, cursor.y - rowHeight - padding);
                    rowHeight = 0f;
                }

                chip.Rect.SetParent(content, false);
                chip.Rect.anchoredPosition = cursor;
                cursor.x += chipWidth + padding;
                rowHeight = Mathf.Max(rowHeight, chipHeight);
            }

            // 마지막 줄까지 포함한 높이로 content 크기를 맞춰야 스크롤 범위가 정해진다
            float contentHeight = -cursor.y + rowHeight + padding;
            content.sizeDelta = new Vector2(contentWidth, contentHeight);
        }

        private void OnClickSort(ChipSortKey key)
        {
            // 같은 키를 다시 누르면 오름/내림차순 전환
            sortAscending = (sortKey == key) ? !sortAscending : true;
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
            AugmentationChip x = a.Chip;
            AugmentationChip y = b.Chip;
            int result = sortKey switch
            {
                ChipSortKey.Name => string.CompareOrdinal(x.Name, y.Name),
                ChipSortKey.StatType => x.StatType.CompareTo(y.StatType),
                ChipSortKey.Value => x.Value.CompareTo(y.Value),
                ChipSortKey.Size => x.Cells.Length.CompareTo(y.Cells.Length),
                _ => 0,
            };

            // 값이 같으면 ID 순으로 고정
            if (result == 0)
                result = x.ID.CompareTo(y.ID);

            return sortAscending ? result : -result;
        }
    }
}
