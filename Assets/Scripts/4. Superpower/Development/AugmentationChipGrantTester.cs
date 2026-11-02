using System.Linq;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower.Development
{
    // 스킬 확인용, 시작할 때와 grantKey를 누를 때 chipIds의 칩을 플레이어 인벤토리에 넣는다.
    // 넣기만 하므로 장착은 증강칩 패널(X)에서 우클릭이나 드래그로 한다.
    // 칩 모양은 ChipDispenser처럼 얻을 때마다 칸 수 안에서 새로 뽑는다.
    // grantAllToggleKey로 모든 칩 지급을 켜면 chipIds를 무시하고 테이블의 모든 칩을 넣는다. 이때는 이미 가진 칩을 다시 넣지 않는다.
    public class AugmentationChipGrantTester : MonoBehaviour
    {
        [Tooltip("넣을 칩의 Id (StatChip.tsv, SkillChip.tsv)")]
        [SerializeField] private int[] chipIds = { 2302, 2102 };
        [SerializeField] private bool grantOnStart = true;
        [SerializeField] private Key grantKey = Key.F9;
        [Tooltip("켜져 있으면 chipIds를 무시하고 모든 칩을 한 장씩 넣는다. 이미 가진 칩은 다시 넣지 않는다.")]
        [SerializeField] private bool grantAll;
        [Tooltip("모든 칩 지급을 켜고 끈다. 켜는 순간 바로 지급한다.")]
        [SerializeField] private Key grantAllToggleKey = Key.F8;
        [Tooltip("비어 있으면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private AugmentationChipInventory inventory;

        private AugmentationChipTable table;

        private void Start()
        {
            if (inventory == null) inventory = GetComponent<AugmentationChipInventory>();
            if (grantOnStart) Grant();
        }

        private void Update()
        {
            if (grantKey.GetKeyDown()) Grant();

            if (grantAllToggleKey.GetKeyDown())
            {
                grantAll = !grantAll;
                Debug.Log($"[칩 지급 테스트] 모든 칩 지급 {(grantAll ? "켬" : "끔")}", this);
                if (grantAll) Grant();
            }
        }

        private void Grant()
        {
            if (inventory == null)
            {
                Debug.LogWarning($"[칩 지급 테스트] {name}에 AugmentationChipInventory가 없어 칩을 넣지 못했습니다.", this);
                return;
            }

            if (table == null)
            {
                table = new AugmentationChipTable();
                table.Load();
            }

            if (grantAll)
            {
                GrantAll();
                return;
            }

            foreach (int id in chipIds)
            {
                AugmentationChipDef def = table.GetDefs().FirstOrDefault(chip => chip.ID == id);
                if (def == null)
                {
                    Debug.LogWarning($"[칩 지급 테스트] 테이블에 {id}번 칩이 없습니다.", this);
                    continue;
                }

                inventory.Add(def, ChipShapeLibrary.Roll(def.CellCount, ChipGridMetrics.GridSize));
                Debug.Log($"[칩 지급 테스트] {def.Name}({id}) 지급", this);
            }
        }

        // 테이블의 모든 칩 중 아직 없는 칩만 한 장씩 넣는다.
        private void GrantAll()
        {
            int count = 0;
            foreach (AugmentationChipDef def in table.GetDefs())
            {
                if (HasChip(def)) continue;

                inventory.Add(def, ChipShapeLibrary.Roll(def.CellCount, ChipGridMetrics.GridSize));
                count++;
            }

            Debug.Log($"[칩 지급 테스트] 모든 칩 지급, 새로 넣은 칩 {count}장", this);
        }

        private bool HasChip(AugmentationChipDef def)
        {
            foreach (OwnedChip chip in inventory.Chips)
                if (chip.Def.ID == def.ID) return true;
            return false;
        }
    }
}
