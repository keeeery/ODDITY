using System;
using System.Collections.Generic;
using TeamOdd.Combat;
using TeamOdd.UI;
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
    /// 스킬 칩은 스킬 슬롯 수(5개)까지만 장착된다. 그리드에 놓으면 인벤토리가 빈 스킬 슬롯에 넣고, 아래 스킬 슬롯 줄(SkillSlotBarView)에서 순서를 바꾼다.
    /// 칩·스킬 슬롯에 마우스를 올리면 칩 툴팁(TooltipView)을 띄우고, 안내·장착 실패·스탯 변화는 HUD의 SystemMessage로 알린다.
    /// 모은 칩 종류 수는 패널 위쪽 오른쪽(countText)에 늘 보여 준다.
    /// 패널은 테이블도 플레이어도 모른다. 여는 쪽(AugmentationChipToggle, 정비소 등)이 인벤토리를 넘겨준다.
    /// </summary>
    public class AugmentationChipPanel : MonoBehaviour
    {
        private static readonly string SkillSlotsFullMessage = $"스킬 칩은 {AugmentationChipInventory.SkillSlotCount}개까지만 장착할 수 있습니다.";

        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private SkillSlotBarView skillBar; // 비워두면 자식에서 찾는다. AugmentationChipLayoutBuilder의 Build Layout이 만든다
        [SerializeField] private TooltipView tooltip; // 비워두면 같은 캔버스에서 찾는다. Augmentation Chip Canvas에 Chip Tooltip 프리팹이 있다
        [SerializeField] private Text countText;      // 제목 줄 오른쪽의 보유 칩 수
        [SerializeField] private Font labelFont;

        private static int totalChipCount = -1; // 칩 테이블 크기. 처음 열 때 한 번 읽는다

        private readonly List<ChipItemView> chips = new();
        private AugmentationChipInventory inventory;
        private RectTransform root;  // 드래그 중인 칩을 올려두는 최상단

        private ChipItemView dragging;
        private Vector2 dragOffset; // 드래그 중인 칩 위치 - 포인터 위치
        // 그리드에서 들어 올린 스킬 칩의 원래 슬롯. 그리드 안에서 자리만 옮기면 같은 번호로 돌려놓는다. 없으면 -1
        private int liftedSkillSlot = -1;
        // 드래그 중인 칩을 그리드에서 들어 올렸는지. 드래그 중에 패널을 닫으면 원래 자리에 다시 장착한다
        private bool liftedFromGrid;
        // 회전할 때 포인터 위치를 다시 읽기 위해 보관
        // PointerEventData는 입력 모듈이 버튼끼리 돌려쓰므로 이벤트 밖에서 읽으면 안 되고, 위치만 복사해 둔다
        private Vector2 pointerPosition;

        public bool IsOpen => gameObject.activeSelf;

        // 칩이나 스킬 슬롯을 드래그 중인지. 이때 오른쪽 클릭으로 장착을 바꾸면 들고 있는 것과 꼬인다
        public bool IsDragging => dragging != null || (SkillBar != null && SkillBar.IsDragging);

        // 패널이 꺼진 채로 Close/Open이 먼저 불릴 수 있으므로 Awake에 기대지 않고 쓸 때 찾는다
        private SkillSlotBarView SkillBar
        {
            get
            {
                if (skillBar == null)
                    skillBar = GetComponentInChildren<SkillSlotBarView>(true);
                return skillBar;
            }
        }

        // 패널이 꺼진 채로 Open이 먼저 불릴 수 있으므로 SkillBar와 같이 쓸 때 찾는다
        private TooltipView Tooltip
        {
            get
            {
                if (tooltip == null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>(true);
                    if (canvas != null)
                        tooltip = canvas.rootCanvas.GetComponentInChildren<TooltipView>(true);
                }
                return tooltip;
            }
        }

        public void IsActivePanel(bool isActive, AugmentationChipInventory inventory)
        {
            if(inventory != null && isActive) Open(inventory);
            else Close();
        }
        
        public void Open(AugmentationChipInventory inventory)
        {
            this.inventory = inventory;
            root = (RectTransform)transform;

            // 지난번에 그린 칩을 버리고 인벤토리 기준으로 다시 그린다
            foreach (ChipItemView chip in chips)
                Destroy(chip.gameObject);
            chips.Clear();
            tray.Clear();
            grid.Clear();

            foreach (OwnedChip owned in inventory.Chips)
                AddChip(owned);

            tray.Layout();

            // 칩을 다시 그리다가 해제된 스킬 칩이 있을 수 있으므로 슬롯 줄은 그 뒤에 연다
            if (SkillBar != null)
            {
                SkillBar.Open(inventory, root, labelFont, Tooltip);
            }
            else
            {
                Debug.LogWarning("[AugmentationChipPanel] 스킬 슬롯 줄이 없습니다. 프리팹에서 AugmentationChipLayoutBuilder → Build Layout을 실행해 주세요.", this);
            }

            RefreshCounts();
            gameObject.SetActive(true);
            // 제목 오른쪽 조작 안내. 처음 열 때만 만든다
            ChipControlHint.Attach(transform);
        }

        // 드래그 중에 닫아도 된다. 들고 있던 칩과 그리드 미리보기는 CancelDrag가 정리한다
        public void Close()
        {
            CancelDrag();
            if (SkillBar != null)
                SkillBar.Close();
            // 툴팁은 패널 밖(캔버스)에 있어서 패널을 꺼도 같이 꺼지지 않는다
            if (Tooltip != null)
                Tooltip.Hide();

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
            chip.PointerEntered += OnPointerEntered;
            chip.PointerMoved += OnPointerMoved;
            chip.PointerExited += OnPointerExited;
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

        #region Tooltip

        // 칩이나 스킬 슬롯을 들고 있는 동안에는 지나가는 칩의 툴팁을 띄우지 않는다
        private void OnPointerEntered(ChipItemView chip, PointerEventData eventData)
        {
            if (IsDragging == false)
                ShowTooltip(chip, eventData.position);
        }

        private void OnPointerMoved(ChipItemView chip, PointerEventData eventData)
        {
            if (Tooltip != null)
                Tooltip.SetScreenPosition(chip, eventData.position);
        }

        private void OnPointerExited(ChipItemView chip)
        {
            if (Tooltip != null)
                Tooltip.Hide(chip);
        }

        private void ShowTooltip(ChipItemView chip, Vector2 screenPosition)
        {
            ChipTooltip.Show(Tooltip, chip, chip.Owned, screenPosition, ChipTooltip.GetEquipStatus(chip.Owned, inventory));
        }

        #endregion

        #region Equip

        // 인벤토리가 받아들였을 때만 그리드에 놓는다. 스킬 슬롯이 가득 차 있으면 인벤토리가 거절한다
        private bool Equip(ChipItemView chip, Vector2Int origin, int preferredSkillSlot = -1)
        {
            if (inventory.Equip(chip.Owned, origin, chip.Rotation, preferredSkillSlot) == false)
                return false;

            grid.Place(chip, origin);
            return true;
        }

        private void Unequip(ChipItemView chip)
        {
            grid.Remove(chip);
            inventory.Unequip(chip.Owned);
        }

        private void OnRightClick(ChipItemView chip)
        {
            // 드래그 중에는 들고 있는 칩이나 스킬 슬롯과 꼬이지 않도록 무시한다
            // (스킬 슬롯을 드는 중에 장착이 바뀌면 놓을 때 엉뚱한 스킬의 번호가 바뀐다)
            if (IsDragging)
                return;

            if (chip.Owned.IsEquipped)
            {
                Unequip(chip);
                tray.Add(chip);
            }
            else if (inventory.CanEquip(chip.Owned) == false)
            {
                // 장착 안 된 칩을 거절하는 경우는 스킬 슬롯이 가득 찼을 때뿐이다. 자리를 찾지 않고 돌리지도 않는다
                SystemMessage.Show(SkillSlotsFullMessage, SystemMessage.Tone.Warning);
                return;
            }
            else if (TryQuickEquip(chip) == false)
            {
                SystemMessage.Show("장착할 자리가 없습니다.", SystemMessage.Tone.Warning);
                return;
            }

            tray.Layout();
            // 칩이 포인터 아래에서 다른 곳으로 옮겨 갔으므로 툴팁을 닫는다. 다음에 올라간 칩이 새로 띄운다
            if (Tooltip != null)
                Tooltip.Hide(chip);
            SystemMessage.Show(GetStatSummary());
        }

        // 지금 방향부터 시계 방향으로 돌려 가며 들어갈 자리를 찾는다
        // 네 방향 모두 안 되면 네 번 돌아 원래 방향으로 돌아온 채 트레이에 남는다
        private bool TryQuickEquip(ChipItemView chip)
        {
            for (int i = 0; i < 4; i++)
            {
                if (grid.TryFindOrigin(chip, out Vector2Int origin) && Equip(chip, origin))
                {
                    tray.Remove(chip);
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
            liftedSkillSlot = -1;
            liftedFromGrid = chip.Owned.IsEquipped;

            // 그리드에서 들어올리면 장착 해제. 스킬 칩이면 해제하기 전에 번호를 기억해 둔다
            if (liftedFromGrid)
            {
                liftedSkillSlot = inventory.GetSkillSlot(chip.Owned);
                Unequip(chip);
            }
            else
            {
                tray.Remove(chip);
            }

            // 드래그 중에는 최상단(루트)으로 옮겨서 다른 UI에 가리지 않게 한다
            chip.Rect.SetParent(root, true);
            chip.Rect.SetAsLastSibling();

            Vector2 pointer = ScreenToLocal(root, pointerPosition);
            dragOffset = chip.Rect.anchoredPosition - pointer;
            if (Tooltip != null)
                Tooltip.Hide();
        }

        private void OnDrag(ChipItemView chip, PointerEventData eventData)
        {
            pointerPosition = eventData.position;
            Vector2 pointer = ScreenToLocal(root, pointerPosition);
            chip.Rect.anchoredPosition = pointer + dragOffset;
            ShowPreview(chip);
        }

        private void OnEndDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = null;
            liftedFromGrid = false;
            grid.ClearPreview();

            // 그리드에 올리면 장착, 아니면 트레이로 돌아간다
            // 그리드에서 들어 올린 스킬 칩은 원래 번호가 비어 있으면 그 번호로 돌아간다
            Vector2Int origin = grid.GetHoveredOrigin(chip);
            bool fits = grid.CanPlace(chip, origin);
            bool equipped = fits && Equip(chip, origin, liftedSkillSlot);
            liftedSkillSlot = -1;
            if (equipped == false)
                tray.Add(chip);

            // 트레이로 돌아오거나 빠져나간 자리를 메우도록 다시 정렬한다
            tray.Layout();

            // 놓을 자리는 맞았는데 스킬 슬롯이 가득 차서 트레이로 돌아온 경우 알려준다. 아니면 바뀐 스탯을 보여준다
            if (fits && equipped == false)
                SystemMessage.Show(SkillSlotsFullMessage, SystemMessage.Tone.Warning);
            else
                SystemMessage.Show(GetStatSummary());

            // 내려놓은 뒤에도 포인터가 칩 위에 있으면 바뀐 장착 상태로 툴팁을 다시 띄운다
            if (RectTransformUtility.RectangleContainsScreenPoint(chip.Rect, eventData.position, null))
                ShowTooltip(chip, eventData.position);
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

            ShowPreview(chip);
        }

        // 패널이 꺼지면 OnEndDrag가 오지 않으므로 닫을 때 드래그를 여기서 정리한다
        // 그리드 미리보기를 지우고, 그리드에서 들어 올린 칩은 원래 자리와 방향으로 다시 장착한다
        // 칩 화면은 다음에 열 때 인벤토리 기준으로 다시 그리므로 장착 기록만 되돌리면 된다
        private void CancelDrag()
        {
            if (dragging == null)
                return;

            grid.ClearPreview();
            if (liftedFromGrid)
            {
                OwnedChip owned = dragging.Owned;
                inventory.Equip(owned, owned.Origin, owned.Rotation, liftedSkillSlot);
            }

            dragging = null;
            liftedFromGrid = false;
            liftedSkillSlot = -1;
        }

        // 스킬 슬롯이 가득 차 장착할 수 없는 칩은 빈 자리 위에서도 놓을 수 없는 색으로 보여준다
        private void ShowPreview(ChipItemView chip)
        {
            grid.ShowPreview(chip, inventory.CanEquip(chip.Owned) == false);
        }

        #endregion

        // 모은 칩 종류 수 / 전체 칩 종류 수. 같은 칩을 여러 장 가져도 한 종류로 센다
        private void RefreshCounts()
        {
            if (countText == null)
                return;

            HashSet<int> owned = new();
            foreach (OwnedChip chip in inventory.Chips)
                owned.Add(chip.Def.ID);

            if (totalChipCount < 0)
            {
                AugmentationChipTable table = new();
                table.Load();
                totalChipCount = table.Count;
            }

            countText.text = $"보유 {owned.Count} / 전체 {totalChipCount}";
        }

        // 장착이 바뀔 때 SystemMessage로 띄우는 스탯 한 줄. 칩 수는 countText가 보여 준다
        private string GetStatSummary()
        {
            RuntimeStats stats = inventory.Stats;
            return $"MaxHp {stats.MaxHp}(+{stats.GetBonus(StatType.MaxHp)})  Atk {stats.Atk}(+{stats.GetBonus(StatType.Atk)})  Def {stats.Def}(+{stats.GetBonus(StatType.Def)})";
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
