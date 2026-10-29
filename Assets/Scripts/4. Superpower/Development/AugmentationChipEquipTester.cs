using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TeamOdd.Superpower.Development
{
    // 스킬 확인용, 증강칩 패널(X)을 열지 않아도 시작할 때 chipIds의 칩을 장착해 1~5번 키로 바로 쓰게 한다.
    // 인벤토리에 그 칩이 있으면 그것을, 없으면 테이블에서 새로 넣어 장착한다.
    // AugmentationChipGrantTester와 같이 붙여도 그쪽이 시작할 때 넣은 칩을 쓰도록 한 프레임 뒤에 장착한다.
    // 그리드 자리는 회전 없이 좌상단부터 비어 있는 첫 자리에 둔다.
    public class AugmentationChipEquipTester : MonoBehaviour
    {
        [Tooltip("장착할 칩의 Id (AugmentationChip.tsv), 스킬 칩은 이 순서대로 빈 스킬 슬롯에 들어간다.")]
        [SerializeField] private int[] chipIds = { 2006, 2007 };
        [Tooltip("비어 있으면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private AugmentationChipInventory inventory;

        private AugmentationChipTable table;

        private IEnumerator Start()
        {
            if (inventory == null) inventory = GetComponent<AugmentationChipInventory>();
            yield return null;

            if (inventory == null)
            {
                Debug.LogWarning($"[칩 장착 테스트] {name}에 AugmentationChipInventory가 없어 칩을 장착하지 못했습니다.", this);
                yield break;
            }

            foreach (int id in chipIds) Equip(id);
        }

        private void Equip(int id)
        {
            // 인벤토리는 씬을 다시 불러도 남아 있으므로 이미 장착한 칩은 건너뜀
            if (inventory.Chips.Any(chip => chip.Def.ID == id && chip.IsEquipped)) return;

            OwnedChip owned = inventory.Chips.FirstOrDefault(chip => chip.Def.ID == id) ?? Grant(id);
            if (owned == null) return;

            if (TryFindOrigin(owned.Cells, out Vector2Int origin) == false)
            {
                Debug.LogWarning($"[칩 장착 테스트] 그리드에 {owned.Def.Name}({id})을 놓을 빈 자리가 없습니다.", this);
                return;
            }

            if (inventory.Equip(owned, origin, 0) == false)
            {
                Debug.LogWarning($"[칩 장착 테스트] 스킬 슬롯이 가득 차 {owned.Def.Name}({id})을 장착하지 못했습니다.", this);
                return;
            }

            Debug.Log($"[칩 장착 테스트] {owned.Def.Name}({id}) 장착", this);
        }

        private OwnedChip Grant(int id)
        {
            if (table == null)
            {
                table = new AugmentationChipTable();
                table.Load();
            }

            AugmentationChipDef def = table.GetDefs().FirstOrDefault(chip => chip.ID == id);
            if (def == null)
            {
                Debug.LogWarning($"[칩 장착 테스트] 테이블에 {id}번 칩이 없습니다.", this);
                return null;
            }

            inventory.Add(def, ChipShapeLibrary.Roll(def.CellCount, ChipGridMetrics.GridSize));
            return inventory.Chips[inventory.Chips.Count - 1];
        }

        // 이미 장착한 칩과 겹치지 않고 그리드 안에 들어가는 첫 자리
        private bool TryFindOrigin(Vector2Int[] cells, out Vector2Int origin)
        {
            const int gridSize = ChipGridMetrics.GridSize;

            var occupied = new HashSet<Vector2Int>();
            foreach (OwnedChip chip in inventory.Chips.Where(chip => chip.IsEquipped))
                foreach (Vector2Int cell in Rotate(chip.Cells, chip.Rotation))
                    occupied.Add(chip.Origin + cell);

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (cells.All(cell => IsFree(candidate + cell, occupied, gridSize)) == false) continue;

                    origin = candidate;
                    return true;
                }
            }

            origin = default;
            return false;
        }

        private static bool IsFree(Vector2Int p, HashSet<Vector2Int> occupied, int gridSize) =>
            p.x >= 0 && p.y >= 0 && p.x < gridSize && p.y < gridSize && occupied.Contains(p) == false;

        // ChipShapeLibrary·ChipItemView와 같은 시계 방향 90도 회전을 count번. (x, y) -> (Height-1-y, x)
        private static Vector2Int[] Rotate(Vector2Int[] cells, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int height = cells.Max(cell => cell.y) + 1;
                cells = cells.Select(cell => new Vector2Int(height - 1 - cell.y, cell.x)).ToArray();
            }

            return cells;
        }
    }
}
