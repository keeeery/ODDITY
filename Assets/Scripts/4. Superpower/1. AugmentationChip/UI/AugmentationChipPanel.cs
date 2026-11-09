using System;
using System.Collections.Generic;
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
    /// 칩·스킬 슬롯에 마우스를 올리면 칩 툴팁(TooltipView)을 띄우고, 안내·장착 실패는 HUD의 SystemMessage로 알린다.
    /// 모은 칩 종류 수는 패널 위쪽 오른쪽(countText)에 늘 보여 준다.
    /// 그리드 위에는 "보유 칩" 제목과 같은 높이로 "장착 칩" 제목 줄을 만들고, 그 오른쪽 끝의 전체 해제 버튼은 장착된 칩을 모두 트레이로 돌려보낸다.
    /// 패널은 테이블도 플레이어도 모른다. 여는 쪽(AugmentationChipToggle, 정비소 등)이 인벤토리를 넘겨준다.
    /// </summary>
    public class AugmentationChipPanel : MonoBehaviour
    {
        private static readonly string SkillSlotsFullMessage = $"스킬 칩은 {AugmentationChipInventory.SkillSlotCount}개까지만 장착할 수 있습니다.";

        private const string UnequipAllLabel = "전체 해제";
        private const string GridTitleLabel = "장착 칩";
        // 전체 해제 버튼 크기와 간격 (칩 캔버스 3840x2160 기준)
        private const float UnequipAllPadding = 48f; // 버튼 가장자리 ~ 글자
        private const float UnequipAllGap = 48f;     // 그리드 위에 자리가 없어 제목 줄에 둘 때 버튼 ~ 보유 칩 수 글자

        private static readonly Vector3[] corners = new Vector3[4];

        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private SkillSlotBarView skillBar; // 비워두면 자식에서 찾는다. AugmentationChipLayoutBuilder의 Build Layout이 만든다
        [SerializeField] private TooltipView tooltip; // 비워두면 같은 캔버스에서 찾는다. Augmentation Chip Canvas에 Chip Tooltip 프리팹이 있다
        [SerializeField] private Text countText;      // 제목 줄 오른쪽의 보유 칩 수
        [SerializeField] private Font labelFont;
        [SerializeField] private SoundData equipSound;   // 칩을 그리드에 붙일 때. 후보는 Resources/Audios/UI/Chip/Equip
        [SerializeField] private SoundData unequipSound; // 칩을 그리드에서 뗄 때. 후보는 Resources/Audios/UI/Chip/Unequip

        private static int totalChipCount = -1; // 칩 테이블 크기. 처음 열 때 한 번 읽는다

        private readonly List<ChipItemView> chips = new();
        private AugmentationChipInventory inventory;
        private RectTransform root;  // 드래그 중인 칩을 올려두는 최상단
        private Button unequipAllButton; // 처음 열 때 정렬 버튼을 복제해 만든다
        private Text gridTitle;          // "장착 칩". 처음 열 때 "보유 칩" 제목을 복제해 만든다

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
            // 지난번 검색어가 남아 있으면 칩이 빠진 것처럼 보이므로 다시 열 때마다 지운다
            tray.ResetSearch();

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
            PlaceUnequipAllButton();
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
            equipSound.Play();
            return true;
        }

        private void Unequip(ChipItemView chip)
        {
            grid.Remove(chip);
            inventory.Unequip(chip.Owned);
            unequipSound.Play();
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
        }

        // 그리드에 장착된 칩을 모두 해제해 트레이로 돌려보낸다. 칩마다 방향은 그대로 두고 소리는 한 번만 낸다
        private void UnequipAll()
        {
            // 드래그 중에는 들고 있는 칩이나 스킬 슬롯과 꼬이지 않도록 무시한다 (OnRightClick과 같은 이유)
            if (IsDragging)
                return;

            bool any = false;
            foreach (ChipItemView chip in chips)
            {
                if (chip.Owned.IsEquipped == false)
                    continue;

                grid.Remove(chip);
                inventory.Unequip(chip.Owned);
                tray.Add(chip);
                any = true;
            }

            if (any == false)
            {
                SystemMessage.Show("장착된 칩이 없습니다.", SystemMessage.Tone.Warning);
                return;
            }

            unequipSound.Play();
            tray.Layout();
            if (Tooltip != null)
                Tooltip.Hide();
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

            // 놓을 자리는 맞았는데 스킬 슬롯이 가득 차서 트레이로 돌아온 경우 알려준다
            if (fits && equipped == false)
                SystemMessage.Show(SkillSlotsFullMessage, SystemMessage.Tone.Warning);

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

        // 그리드 위, "보유 칩" 제목과 같은 높이의 줄 왼쪽에 "장착 칩" 제목을, 오른쪽 끝(그리드 오른쪽 끝)에 전체 해제 버튼을 둔다
        // 프리팹은 그리드를 이 줄만큼 내려 두었다. Build Layout으로 다시 배치해 그리드 위에 자리가 없어지면
        // 제목 줄 오른쪽, 보유 칩 수 글자 바로 왼쪽에 둔다 (그 글자는 오른쪽 끝에 맞춰 왼쪽으로 늘어나므로 열 때마다 폭을 다시 잰다)
        private void PlaceUnequipAllButton()
        {
            if (unequipAllButton == null)
                unequipAllButton = CreateUnequipAllButton();
            if (unequipAllButton == null)
                return;

            RectTransform rect = (RectTransform)unequipAllButton.transform;
            Text trayTitle = tray.TitleText;
            if (trayTitle != null && TryGetGridHeaderRow(trayTitle.rectTransform, out Rect row))
            {
                if (gridTitle == null)
                    gridTitle = CreateGridTitle(trayTitle);
                gridTitle.gameObject.SetActive(true);
                PlaceAt(gridTitle.rectTransform, new Vector2(0f, 1f), new Vector2(row.xMin, row.yMax));
                PlaceAt(rect, new Vector2(1f, 0.5f), new Vector2(row.xMax, row.center.y));
                return;
            }

            if (gridTitle != null)
                gridTitle.gameObject.SetActive(false);
            if (countText == null)
                return;

            RectTransform count = countText.rectTransform;
            rect.anchorMin = rect.anchorMax = count.anchorMin;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(count.anchoredPosition.x - countText.preferredWidth - UnequipAllGap, count.anchoredPosition.y);
        }

        // 그리드 위 제목 줄 (패널 로컬 좌표). 높이는 "보유 칩" 제목 칸, 왼쪽·오른쪽 끝은 그리드에 맞춘다
        // 그리드 위쪽 끝이 그 줄 아래에 있어야 자리가 있다
        private bool TryGetGridHeaderRow(RectTransform trayTitle, out Rect row)
        {
            Rect title = GetLocalRect(trayTitle);
            Rect area = GetLocalRect((RectTransform)grid.transform);
            row = Rect.MinMaxRect(area.xMin, title.yMin, area.xMax, title.yMax);
            return area.yMax <= title.yMin + 0.5f;
        }

        // 다른 부모 아래 있는 칸의 사각형을 패널 로컬 좌표로 바꾼다
        private Rect GetLocalRect(RectTransform target)
        {
            target.GetWorldCorners(corners);
            Vector2 min = transform.InverseTransformPoint(corners[0]);
            Vector2 max = transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // 피벗이 패널 로컬 좌표 point에 오도록 놓는다
        private static void PlaceAt(RectTransform rect, Vector2 pivot, Vector2 point)
        {
            rect.pivot = pivot;
            rect.localPosition = point;
        }

        // "보유 칩" 제목을 복제해 글꼴·크기·색을 맞춘다
        private Text CreateGridTitle(Text template)
        {
            Text title = Instantiate(template, transform);
            title.name = "GridTitle";
            title.text = GridTitleLabel;
            return title;
        }

        // 트레이의 정렬 버튼을 복제해 테두리·마우스 반응·글꼴을 맞춘다. 복제본에는 정렬 기능(실행 중에 붙인 리스너)이 따라오지 않는다
        private Button CreateUnequipAllButton()
        {
            Button template = tray.SortButtonTemplate;
            if (template == null)
                return null;

            Button button = Instantiate(template, transform);
            button.name = "Unequip All";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(UnequipAll);
            // 정렬 버튼과 달리 바탕 없이 테두리 선만 그린다
            if (button.TryGetComponent(out VectorPanel frame))
                frame.OutlineOnly = true;

            Text label = button.GetComponentInChildren<Text>(true);
            label.text = UnequipAllLabel;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            RectTransform rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(label.preferredWidth + UnequipAllPadding * 2f, rect.sizeDelta.y);
            return button;
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
