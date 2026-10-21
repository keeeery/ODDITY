using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 트레이/그리드 위에 놓이는 칩 하나의 UI 상태와 포인터 입력.
    /// 입력은 이벤트로만 넘기고, 어디에 놓을지는 AugmentationChipPanel이 결정한다.
    /// </summary>
    // 휠(IScrollHandler)은 구현하지 않아야 부모 ScrollRect로 그대로 전달된다
    public class ChipItemView : MonoBehaviour, IPointerEnterHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public AugmentationChip Chip { get; private set; }
        public RectTransform Rect { get; private set; }
        public Vector2Int? GridOrigin { get; set; }
        public Vector2 DragOffset { get; set; }

        private RectTransform label;

        public event Action<ChipItemView> PointerEntered;
        public event Action<ChipItemView, PointerEventData> DragBegan;
        public event Action<ChipItemView, PointerEventData> Dragged;
        public event Action<ChipItemView, PointerEventData> DragEnded;

        // 테이블 데이터로 칩 UI를 만든다. 칩 목록은 런타임에 테이블에서 읽으므로 생성도 런타임에 한다
        public static ChipItemView Create(AugmentationChipDef def, Transform parent, Font font)
        {
            GameObject go = new GameObject($"Chip_{def.ID}", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            rect.sizeDelta = Vector2.zero;

            AugmentationChip chip = go.AddComponent<AugmentationChip>();
            chip.Init(def);

            // 첫 칸에 칩 이름을 표시
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.layer = go.layer;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0f, 1f);
            labelRect.sizeDelta = new Vector2(ChipGridMetrics.CellSize, ChipGridMetrics.CellSize);

            Text text = labelGo.AddComponent<Text>();
            text.font = font;
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = def.Name;
            text.raycastTarget = false;

            ChipItemView view = go.AddComponent<ChipItemView>();
            view.Chip = chip;
            view.Rect = rect;
            view.label = labelRect;
            view.RefreshLabel();
            return view;
        }

        public void RotateClockwise()
        {
            Chip.RotateClockwise();
            RefreshLabel();
        }

        // 라벨이 Cells[0] 칸을 따라가고, 새로 그린 외곽선에 가리지 않게 맨 위로 올린다
        private void RefreshLabel()
        {
            label.anchoredPosition = ChipGridMetrics.CellPosition(Chip.Cells[0]);
            label.SetAsLastSibling();
        }

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke(this);
        public void OnBeginDrag(PointerEventData eventData) => DragBegan?.Invoke(this, eventData);
        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(this, eventData);
        public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(this, eventData);
    }
}
