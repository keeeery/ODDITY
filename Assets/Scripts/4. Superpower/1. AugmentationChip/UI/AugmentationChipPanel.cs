using System;
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
    /// 스킬 칩은 스킬 슬롯 수(5개)까지만 장착된다. 그리드에 놓으면 인벤토리가 빈 스킬 슬롯에 넣고, 아래 스킬 슬롯 줄(SkillSlotBarView)에서 순서를 바꾼다.
    /// 패널은 테이블도 플레이어도 모른다. 여는 쪽(AugmentationChipToggle, 정비소 등)이 인벤토리를 넘겨준다.
    /// </summary>
    public class AugmentationChipPanel : MonoBehaviour
    {
        private static readonly string SkillSlotsFullMessage = $"스킬 칩은 {AugmentationChipInventory.SkillSlotCount}개까지만 장착할 수 있습니다.";

        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private SkillSlotBarView skillBar; // 비워두면 자식에서 찾는다. AugmentationChipLayoutBuilder의 Build Layout이 만든다
        [SerializeField] private Text infoText;
        [SerializeField] private Font labelFont;

        private readonly List<ChipItemView> chips = new();
        private AugmentationChipInventory inventory;
        private RectTransform root;  // 드래그 중인 칩을 올려두는 최상단
        private string guideText;    // 프리팹에 적어 둔 안내 문구. 열 때마다 이걸로 되돌린다
        private OwnedChip shownChip; // 정보 텍스트에 보여주고 있는 칩. 스킬 순서가 바뀌면 다시 보여준다

        private ChipItemView dragging;
        private Vector2 dragOffset; // 드래그 중인 칩 위치 - 포인터 위치
        // 그리드에서 들어 올린 스킬 칩의 원래 슬롯. 그리드 안에서 자리만 옮기면 같은 번호로 돌려놓는다. 없으면 -1
        private int liftedSkillSlot = -1;
        // 회전할 때 포인터 위치를 다시 읽기 위해 보관
        // PointerEventData는 입력 모듈이 버튼끼리 돌려쓰므로 이벤트 밖에서 읽으면 안 되고, 위치만 복사해 둔다
        private Vector2 pointerPosition;

        public bool IsOpen => gameObject.activeSelf;

        // 칩이나 스킬 슬롯을 드래그 중인지. 이때 닫거나 오른쪽 클릭으로 장착을 바꾸면 들고 있는 것과 꼬인다
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

        public void IsActivePanel(bool isActive, AugmentationChipInventory inventory)
        {
            if(inventory != null && isActive) Open(inventory);
            else Close();
        }
        
        public void Open(AugmentationChipInventory inventory)
        {
            // 다른 인벤토리로 다시 열 수도 있으므로 지난번 구독을 먼저 끊는다
            if (this.inventory != null)
                this.inventory.SkillSlotsChanged -= OnSkillSlotsChanged;

            this.inventory = inventory;
            root = (RectTransform)transform;
            guideText ??= infoText.text;
            infoText.text = guideText;
            shownChip = null;

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
            inventory.SkillSlotsChanged += OnSkillSlotsChanged;
            if (SkillBar != null)
            {
                SkillBar.ChipHovered -= ShowInfo;
                SkillBar.ChipHovered += ShowInfo;
                SkillBar.Open(inventory, root, labelFont);
            }
            else
            {
                Debug.LogWarning("[AugmentationChipPanel] 스킬 슬롯 줄이 없습니다. 프리팹에서 AugmentationChipLayoutBuilder → Build Layout을 실행해 주세요.", this);
            }

            gameObject.SetActive(true);
        }

        // 드래그 중에 닫으면 칩이 루트에 붙은 채 남으므로 여는 쪽이 IsDragging을 확인하고 부른다
        public void Close()
        {
            if (SkillBar != null)
                SkillBar.Close();
            // 정비를 마치면 스킬 쿨타임을 모두 초기화한다. 시작할 때 닫힌 채로 부르는 Close는 아직 인벤토리가 없어 건너뛴다
            if (inventory != null)
            {
                inventory.SkillSlotsChanged -= OnSkillSlotsChanged;
                inventory.ResetAllCooldowns();
            }

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

        private void OnPointerEntered(ChipItemView chip) => ShowInfo(chip.Owned);

        // 스킬 순서가 바뀌면 정보 텍스트의 스킬 번호도 맞춘다
        private void OnSkillSlotsChanged()
        {
            if (shownChip != null)
                ShowInfo(shownChip);
        }

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
                ShowInfo(chip.Owned, SkillSlotsFullMessage);
                return;
            }
            else if (TryQuickEquip(chip) == false)
            {
                ShowInfo(chip.Owned, "장착할 자리가 없습니다.");
                return;
            }

            tray.Layout();
            ShowInfo(chip.Owned);
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

            // 그리드에서 들어올리면 장착 해제. 스킬 칩이면 해제하기 전에 번호를 기억해 둔다
            if (chip.Owned.IsEquipped)
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
            ShowInfo(chip.Owned);
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

            // 놓을 자리는 맞았는데 스킬 슬롯이 가득 차서 트레이로 돌아온 경우 알려준다
            ShowInfo(chip.Owned, fits && equipped == false ? SkillSlotsFullMessage : null);
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

        // 스킬 슬롯이 가득 차 장착할 수 없는 칩은 빈 자리 위에서도 놓을 수 없는 색으로 보여준다
        private void ShowPreview(ChipItemView chip)
        {
            grid.ShowPreview(chip, inventory.CanEquip(chip.Owned) == false);
        }

        #endregion

        private void ShowInfo(OwnedChip info) => ShowInfo(info, null);

        // 정보 텍스트는 세 줄: 칩 이름·설명·장착 상태 / 효과 / 장착 현황
        // notice(장착 실패 안내 등)가 있으면 칸 밖으로 잘리지 않도록 장착 현황 대신 셋째 줄에 쓴다
        private void ShowInfo(OwnedChip info, string notice)
        {
            shownChip = info;

            AugmentationChipDef def = info.Def;
            string state = info.IsEquipped ? $"장착됨 ({info.Origin.x}, {info.Origin.y})" : "미장착";
            int skillSlot = inventory.GetSkillSlot(info);
            if (skillSlot >= 0)
                state += $" · 스킬 {skillSlot + 1}번";

            // 능력치 칩은 오르는 스탯, 스킬 칩은 쿨타임·스태미나 소모량과 스킬마다 다른 매개변수를 스킬이 직접 설명한다
            string effect = def switch
            {
                StatChipDef stat => $"능력치  {stat.StatType} +{stat.Value}",
                SkillChipDef skill => $"스킬({GetCategoryLabel(skill.Category)})  {skill.SkillType} · 쿨타임 {skill.Cooldown}초 · 스태미나 {skill.Cost} · {info.Skill.Describe()}",
                _ => "",
            };

            infoText.text = $"[{def.ID}] {def.Name} - {def.Description}  |  {state}\n{effect}\n{notice ?? GetSummary()}";
        }

        private static string GetCategoryLabel(SkillCategory category) => category switch
        {
            SkillCategory.Attack => "공격",
            SkillCategory.Defense => "방어",
            SkillCategory.Utility => "유틸",
            _ => "",
        };

        private string GetSummary()
        {
            int equipped = 0;
            foreach (OwnedChip owned in inventory.Chips)
                if (owned.IsEquipped)
                    equipped++;

            int skills = 0;
            foreach (OwnedChip slot in inventory.SkillSlots)
                if (slot != null)
                    skills++;

            RuntimeStats stats = inventory.Stats;
            return $"장착 칩 {equipped}/{inventory.Chips.Count}  |  스킬 {skills}/{AugmentationChipInventory.SkillSlotCount}  |  " +
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
