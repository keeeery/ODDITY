using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 장착 패널.
    /// Open(inventory)로 열 때마다 인벤토리를 읽어 칩을 새로 그린다. 장착된 칩은 기록된 위치·방향대로 그리드에, 나머지는 트레이에 놓는다.
    /// 드래그로 트레이 ↔ 그리드를 오가게 하고, 그리드에 놓으면 inventory.Equip, 들어 올리면 inventory.Unequip을 부른다.
    /// 마우스 오른쪽 클릭은 빠른 장착/해제다. 트레이 칩은 그리드의 빈 자리에 바로 넣고, 그리드 칩은 트레이로 돌려보낸다.
    /// 패널은 테이블도 플레이어도 모른다. 여는 쪽(AugmentationChipToggle, 정비소 등)이 인벤토리를 넘겨준다.
    /// </summary>
    public class AugmentationChipPanel : MonoBehaviour
    {
        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private Text infoText;
        [SerializeField] private Font labelFont;

        private readonly List<ChipItemView> chips = new();
        private AugmentationChipInventory inventory;
        private RectTransform root; // 드래그 중인 칩을 올려두는 최상단
        private string guideText;   // 프리팹에 적어 둔 안내 문구. 열 때마다 이걸로 되돌린다

        private ChipItemView dragging;
        private Vector2 dragOffset; // 드래그 중인 칩 위치 - 포인터 위치
        // 회전할 때 포인터 위치를 다시 읽기 위해 보관
        // PointerEventData는 입력 모듈이 버튼끼리 돌려쓰므로 이벤트 밖에서 읽으면 안 되고, 위치만 복사해 둔다
        private Vector2 pointerPosition;

        public bool IsOpen => gameObject.activeSelf;
        public bool IsDragging => dragging != null;

        public void Open(AugmentationChipInventory inventory)
        {
            this.inventory = inventory;
            root = (RectTransform)transform;
            guideText ??= infoText.text;
            infoText.text = guideText;

            // 지난번에 그린 칩을 버리고 인벤토리 기준으로 다시 그린다
            foreach (ChipItemView chip in chips)
                Destroy(chip.gameObject);
            chips.Clear();
            tray.Clear();
            grid.Clear();

            foreach (OwnedChip owned in inventory.Chips)
                AddChip(owned);

            tray.Layout();
            gameObject.SetActive(true);
        }

        // 드래그 중에 닫으면 칩이 루트에 붙은 채 남으므로 여는 쪽이 IsDragging을 확인하고 부른다
        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 드래그 중인 칩은 R키로 시계 방향 90도 회전
            if (dragging != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RotateDragging();
        }

        private void AddChip(OwnedChip owned)
        {
            ChipItemView chip = ChipItemView.Create(owned, tray.Content, labelFont);
            chip.PointerEntered += ShowInfo;
            chip.RightClicked += OnRightClick;
            chip.DragBegan += OnBeginDrag;
            chip.Dragged += OnDrag;
            chip.DragEnded += OnEndDrag;
            chips.Add(chip);

            if (owned.IsEquipped)
            {
                chip.SetRotation(owned.Rotation);
                if (grid.CanPlace(chip, owned.Origin))
                {
                    grid.Place(chip, owned.Origin);
                    return;
                }

                // 그리드 크기가 바뀌어 기록된 자리에 못 놓으면 해제해서 트레이로 보낸다
                inventory.Unequip(owned);
            }

            tray.Add(chip);
        }

        #region Equip

        private void Equip(ChipItemView chip, Vector2Int origin)
        {
            grid.Place(chip, origin);
            inventory.Equip(chip.Owned, origin, chip.Rotation);
        }

        private void Unequip(ChipItemView chip)
        {
            grid.Remove(chip);
            inventory.Unequip(chip.Owned);
        }

        private void OnRightClick(ChipItemView chip)
        {
            // 드래그 중에는 들고 있는 칩과 꼬이지 않도록 무시한다
            if (dragging != null)
                return;

            if (chip.Owned.IsEquipped)
            {
                Unequip(chip);
                tray.Add(chip);
            }
            else if (TryQuickEquip(chip) == false)
            {
                ShowInfo(chip);
                infoText.text += "\n장착할 자리가 없습니다.";
                return;
            }

            tray.Layout();
            ShowInfo(chip);
        }

        // 지금 방향부터 시계 방향으로 돌려 가며 들어갈 자리를 찾는다
        // 네 방향 모두 안 되면 네 번 돌아 원래 방향으로 돌아온 채 트레이에 남는다
        private bool TryQuickEquip(ChipItemView chip)
        {
            for (int i = 0; i < 4; i++)
            {
                if (grid.TryFindOrigin(chip, out Vector2Int origin))
                {
                    tray.Remove(chip);
                    Equip(chip, origin);
                    return true;
                }

                chip.RotateClockwise();
            }

            return false;
        }

        #endregion

        #region Drag

        private void OnBeginDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = chip;
            pointerPosition = eventData.position;

            // 그리드에서 들어올리면 장착 해제
            if (chip.Owned.IsEquipped)
                Unequip(chip);
            else
                tray.Remove(chip);

            // 드래그 중에는 최상단(루트)으로 옮겨서 다른 UI에 가리지 않게 한다
            chip.Rect.SetParent(root, true);
            chip.Rect.SetAsLastSibling();

            Vector2 pointer = ScreenToLocal(root, pointerPosition);
            dragOffset = chip.Rect.anchoredPosition - pointer;
            ShowInfo(chip);
        }

        private void OnDrag(ChipItemView chip, PointerEventData eventData)
        {
            pointerPosition = eventData.position;
            Vector2 pointer = ScreenToLocal(root, pointerPosition);
            chip.Rect.anchoredPosition = pointer + dragOffset;
            grid.ShowPreview(chip);
        }

        private void OnEndDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = null;
            grid.ClearPreview();

            // 그리드에 올리면 장착, 아니면 트레이로 돌아간다
            Vector2Int origin = grid.GetHoveredOrigin(chip);
            if (grid.CanPlace(chip, origin))
                Equip(chip, origin);
            else
                tray.Add(chip);

            // 트레이로 돌아오거나 빠져나간 자리를 메우도록 다시 정렬한다
            tray.Layout();
            ShowInfo(chip);
        }

        // 잡고 있는 지점이 포인터 아래에 그대로 남도록 칩을 회전시킨다
        private void RotateDragging()
        {
            ChipItemView chip = dragging;

            // 포인터가 칩의 어느 지점을 잡고 있는지 (칩 좌상단 기준, y는 아래로)
            Vector2 local = ScreenToLocal(chip.Rect, pointerPosition);
            Vector2 grab = new Vector2(local.x, -local.y);
            float oldHeight = ChipGridMetrics.Length(chip.Height);

            chip.RotateClockwise();

            // 시계 방향 회전이면 잡은 지점 (u, v)는 (oldHeight - v, u)로 옮겨진다. 그만큼 칩을 되돌려 놓는다
            Vector2 rotatedGrab = new Vector2(oldHeight - grab.y, grab.x);
            Vector2 delta = new Vector2(grab.x - rotatedGrab.x, rotatedGrab.y - grab.y);
            chip.Rect.anchoredPosition += delta;
            dragOffset += delta;

            grid.ShowPreview(chip);
        }

        #endregion

        private void ShowInfo(ChipItemView chip)
        {
            int equipped = 0;
            foreach (OwnedChip owned in inventory.Chips)
                if (owned.IsEquipped)
                    equipped++;

            OwnedChip info = chip.Owned;
            AugmentationChipDef def = info.Def;
            string state = info.IsEquipped ? $"장착됨 ({info.Origin.x}, {info.Origin.y})" : "미장착";
            RuntimeStats stats = inventory.Stats;
            infoText.text = $"[{def.ID}] {def.Name} - {def.Description} ({def.StatType} +{def.Value})  |  {state}\n장착 칩 {equipped}/{inventory.Chips.Count}  |  " +
                            $"MaxHp {stats.MaxHp}(+{stats.GetBonus(StatType.MaxHp)})  Atk {stats.Atk}(+{stats.GetBonus(StatType.Atk)})  Def {stats.Def}(+{stats.GetBonus(StatType.Def)})";
        }

        // Screen Space - Overlay 캔버스 전제라 카메라 없이(null) 변환한다
        // 드래그는 시작점과의 차이(dragOffset)만 쓰므로 피벗 기준 좌표 그대로 사용해도 된다
        private static Vector2 ScreenToLocal(RectTransform rect, Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out Vector2 local);
            return local;
        }
    }
}
