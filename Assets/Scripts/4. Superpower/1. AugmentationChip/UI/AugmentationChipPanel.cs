using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 장착 패널의 컨트롤러.
    /// Init으로 받은 보유 칩을 트레이 또는 그리드에 그리고, 드래그로 트레이 ↔ 그리드를 오가게 한다.
    /// 장착 상태(종류·회전·오리진), 배치 규칙, 스탯 반영은 AugmentationChipEquipment가 들고 있다.
    /// 패널은 배치 가능 여부를 묻고 Equip/Unequip을 호출한 뒤 결과를 그리기만 한다.
    /// 칩 목록과 장착 상태는 바깥(AugmentationChipInstaller 등)에서 넣어준다. 패널은 테이블을 모른다.
    /// 마우스 오른쪽 클릭은 빠른 장착/해제다. 트레이 칩은 그리드의 빈 자리에 바로 넣고, 그리드 칩은 트레이로 돌려보낸다.
    /// </summary>
    public class AugmentationChipPanel : MonoBehaviour
    {
        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private Text infoText;
        [SerializeField] private Font labelFont;

        private readonly List<ChipItemView> chips = new();
        private RectTransform root; // 드래그 중인 칩을 올려두는 최상단
        private AugmentationChipEquipment equipment;
        private ChipItemView dragging;
        private Vector2 dragOffset; // 드래그 중인 칩 위치 - 포인터 위치
        // 회전할 때 포인터 위치를 다시 읽기 위해 보관
        // PointerEventData는 입력 모듈이 버튼끼리 돌려쓰므로 이벤트 밖에서 읽으면 안 되고, 위치만 복사해 둔다
        private Vector2 pointerPosition;

        public bool IsDragging => dragging != null;

        // 그리드 칸 수. 인스톨러가 AugmentationChipEquipment의 보드 크기를 이 값에 맞춘다
        public int GridSize => grid.GridSize;

        // 플레이어가 보유한 칩 목록과 장착 상태를 받아, 장착된 칩은 그리드에, 나머지는 트레이(플레이스홀더)에 그린다
        // 패널이 꺼져 있어도(토글로 닫힌 상태) 호출할 수 있다
        public void Init(IEnumerable<OwnedChip> ownedChips, AugmentationChipEquipment equipment)
        {
            root = (RectTransform)transform;
            this.equipment = equipment;

            // 칸과 트레이 폭은 에디터에서 AugmentationChipLayoutBuilder로 미리 만들어 둔 것을 쓴다
            grid.Init();

            foreach (OwnedChip owned in ownedChips)
                AddChip(owned);

            tray.Layout();
        }

        private void Update()
        {
            // 드래그 중인 칩은 R키로 시계 방향 90도 회전
            if (dragging != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RotateDragging();
        }

        // Init 이후에 새로 얻은 칩을 트레이에 넣는다
        public void Add(OwnedChip owned)
        {
            AddChip(owned);
            tray.Layout();
        }

        // 이미 장착된 칩이면 장착 상태에 저장된 회전·오리진대로 그리드에 그리고, 아니면 트레이에 넣는다
        private void AddChip(OwnedChip owned)
        {
            ChipItemView chip = ChipItemView.Create(owned, tray.Content, labelFont);
            chip.PointerEntered += ShowInfo;
            chip.RightClicked += OnRightClick;
            chip.DragBegan += OnBeginDrag;
            chip.Dragged += OnDrag;
            chip.DragEnded += OnEndDrag;
            chips.Add(chip);

            EquippedChip equipped = equipment.Find(owned);
            if (equipped != null)
            {
                chip.SetRotation(equipped.Rotation);
                grid.Place(chip, equipped.Origin);
            }
            else
            {
                tray.Add(chip);
            }
        }

        #region Quick Equip

        private void OnRightClick(ChipItemView chip)
        {
            // 드래그 중에는 들고 있는 칩과 꼬이지 않도록 무시한다
            if (dragging != null)
                return;

            if (IsEquipped(chip))
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
                if (equipment.TryFindOrigin(chip.Owned, chip.Chip.Rotation, out Vector2Int origin) && TryEquip(chip, origin))
                {
                    tray.Remove(chip);
                    return true;
                }

                chip.RotateClockwise();
            }

            return false;
        }

        #endregion

        #region Equip

        private bool IsEquipped(ChipItemView chip) => equipment.IsEquipped(chip.Owned);

        // 장착 상태를 먼저 바꾸고, 받아들여졌을 때만 그리드에 그린다
        private bool TryEquip(ChipItemView chip, Vector2Int origin)
        {
            if (equipment.Equip(chip.Owned, chip.Chip.Rotation, origin) == false)
                return false;

            grid.Place(chip, origin);
            return true;
        }

        private void Unequip(ChipItemView chip) => equipment.Unequip(chip.Owned);

        #endregion

        #region Drag

        private void OnBeginDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = chip;
            pointerPosition = eventData.position;

            // 그리드에서 들어올리면 장착 해제
            if (IsEquipped(chip))
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
            ShowPreview(chip);
        }

        private void OnEndDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = null;
            grid.ClearPreview();

            // 그리드에 올려서 장착되면 그리드에, 아니면 트레이로 돌아간다
            if (TryEquip(chip, grid.GetHoveredOrigin(chip)) == false)
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
            float oldHeight = ChipGridMetrics.Length(chip.Chip.Height);

            chip.RotateClockwise();

            // 시계 방향 회전이면 잡은 지점 (u, v)는 (oldHeight - v, u)로 옮겨진다. 그만큼 칩을 되돌려 놓는다
            Vector2 rotatedGrab = new Vector2(oldHeight - grab.y, grab.x);
            Vector2 delta = new Vector2(grab.x - rotatedGrab.x, rotatedGrab.y - grab.y);
            chip.Rect.anchoredPosition += delta;
            dragOffset += delta;

            ShowPreview(chip);
        }

        // 놓을 수 있는지는 장착 상태에 묻고, 그리드는 그 결과로 칠하기만 한다
        private void ShowPreview(ChipItemView chip)
        {
            Vector2Int origin = grid.GetHoveredOrigin(chip);
            bool valid = equipment.CanPlace(chip.Owned, chip.Chip.Rotation, origin);
            grid.ShowPreview(origin, chip.Chip.Cells, valid);
        }

        #endregion

        #region Display

        private void ShowInfo(ChipItemView chip)
        {
            EquippedChip equipped = equipment.Find(chip.Owned);
            string state = equipped != null ? $"장착됨 ({equipped.Origin.x}, {equipped.Origin.y})" : "미장착";
            AugmentationChip info = chip.Chip;
            IReadOnlyStats stats = equipment.Stats;
            infoText.text = $"[{info.ID}] {info.Name} - {info.Description} ({info.StatType} +{info.Value})  |  {state}\n장착 칩 {equipment.Equipped.Count}/{chips.Count}  |  " +
                            $"MaxHp {stats.MaxHp}(+{equipment.GetBonus(StatType.MaxHp)})  Atk {stats.Atk}(+{equipment.GetBonus(StatType.Atk)})  Def {stats.Def}(+{equipment.GetBonus(StatType.Def)})";
        }

        #endregion

        // Screen Space - Overlay 캔버스 전제라 카메라 없이(null) 변환한다
        // 드래그는 시작점과의 차이(dragOffset)만 쓰므로 피벗 기준 좌표 그대로 사용해도 된다
        private static Vector2 ScreenToLocal(RectTransform rect, Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out Vector2 local);
            return local;
        }
    }
}
