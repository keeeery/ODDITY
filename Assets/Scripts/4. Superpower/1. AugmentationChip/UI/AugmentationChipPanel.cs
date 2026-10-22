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
    /// 테이블에서 칩을 만들어 트레이에 넣고, 드래그로 트레이 ↔ 그리드를 오가게 하면서 스탯/정보 표시를 갱신한다.
    /// UI 오브젝트는 에디터 메뉴(Tools/Superpower/Build Augmentation Chip UI)로 미리 만들어 둔다.
    /// </summary>
    public class AugmentationChipPanel : MonoBehaviour
    {
        [SerializeField] private ChipGridView grid;
        [SerializeField] private ChipTrayView tray;
        [SerializeField] private Text statText;
        [SerializeField] private Text infoText;
        [SerializeField] private Font labelFont;
        [SerializeField] private CombatStats baseStats; // 비워두면 데모용 기본값 사용

        private readonly List<ChipItemView> chips = new();
        private RectTransform root; // 드래그 중인 칩을 올려두는 최상단
        private StatHandler statHandler;
        private ChipItemView dragging;
        // 회전할 때 포인터 위치를 다시 읽기 위해 보관
        // PointerEventData는 입력 모듈이 버튼끼리 돌려쓰므로 이벤트 밖에서 읽으면 안 되고, 위치만 복사해 둔다
        private Vector2 pointerPosition;

        public bool IsDragging => dragging != null;

        private void Start()
        {
            root = (RectTransform)transform;
            if (labelFont == null)
                labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Stat baseStat = baseStats != null
                ? new Stat(baseStats.MaxHp, baseStats.Atk, baseStats.Def)
                : new Stat(100, 10, 5);
            statHandler = new StatHandler(baseStat);

            AugmentationChipTable table = new AugmentationChipTable();
            table.Load();
            foreach (AugmentationChipDef def in table.GetDefs())
                AddChip(def);

            tray.Layout();
            RefreshStat();
        }

        private void Update()
        {
            // 드래그 중인 칩은 R키로 시계 방향 90도 회전
            if (dragging != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RotateDragging();
        }

        private void AddChip(AugmentationChipDef def)
        {
            ChipItemView chip = ChipItemView.Create(def, tray.Content, labelFont);
            chip.PointerEntered += ShowInfo;
            chip.DragBegan += OnBeginDrag;
            chip.Dragged += OnDrag;
            chip.DragEnded += OnEndDrag;

            chips.Add(chip);
            tray.Add(chip);
        }

        #region Drag

        private void OnBeginDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = chip;
            pointerPosition = eventData.position;

            // 그리드에서 들어올리면 장착 해제
            if (chip.GridOrigin.HasValue)
            {
                grid.Remove(chip);
                statHandler.Remove(chip.Chip.Effect);
                RefreshStat();
            }
            else
            {
                tray.Remove(chip);
            }

            // 드래그 중에는 최상단(루트)으로 옮겨서 다른 UI에 가리지 않게 한다
            chip.Rect.SetParent(root, true);
            chip.Rect.SetAsLastSibling();

            Vector2 pointer = ScreenToLocal(root, pointerPosition);
            chip.DragOffset = chip.Rect.anchoredPosition - pointer;
            ShowInfo(chip);
        }

        private void OnDrag(ChipItemView chip, PointerEventData eventData)
        {
            pointerPosition = eventData.position;
            Vector2 pointer = ScreenToLocal(root, pointerPosition);
            chip.Rect.anchoredPosition = pointer + chip.DragOffset;
            grid.ShowPreview(chip);
        }

        private void OnEndDrag(ChipItemView chip, PointerEventData eventData)
        {
            dragging = null;
            grid.ClearPreview();

            Vector2Int origin = grid.GetHoveredOrigin(chip);
            if (grid.CanPlace(chip, origin))
            {
                // 그리드에 올리면 장착
                grid.Place(chip, origin);
                statHandler.Add(chip.Chip.Effect);
                RefreshStat();
            }
            else
            {
                tray.Add(chip);
            }

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
            chip.DragOffset += delta;

            grid.ShowPreview(chip);
        }

        #endregion

        #region Display

        private void ShowInfo(ChipItemView chip)
        {
            int placed = 0;
            foreach (ChipItemView c in chips)
                if (c.GridOrigin.HasValue)
                    placed++;

            string state = chip.GridOrigin.HasValue ? $"장착됨 ({chip.GridOrigin.Value.x}, {chip.GridOrigin.Value.y})" : "미장착";
            AugmentationChip info = chip.Chip;
            infoText.text = $"[{info.ID}] {info.Name} - {info.Description} ({info.StatType} +{info.Value})  |  {state}\n장착 칩 {placed}/{chips.Count}";
        }

        // 기본 + 추가 = 최종 스탯 표시
        private void RefreshStat()
        {
            IStat b = statHandler.GetBase();
            IStat a = statHandler.GetAdded();
            IStat c = statHandler.GetCurrent();
            statText.text =
                $"MaxHp  {b.MaxHp} + {a.MaxHp} = {c.MaxHp}\n" +
                $"Atk      {b.Atk} + {a.Atk} = {c.Atk}\n" +
                $"Def      {b.Def} + {a.Def} = {c.Def}";
        }

        #endregion

        // Screen Space - Overlay 캔버스 전제라 카메라 없이(null) 변환한다
        // 드래그는 시작점과의 차이(DragOffset)만 쓰므로 피벗 기준 좌표 그대로 사용해도 된다
        private static Vector2 ScreenToLocal(RectTransform rect, Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out Vector2 local);
            return local;
        }
    }
}
