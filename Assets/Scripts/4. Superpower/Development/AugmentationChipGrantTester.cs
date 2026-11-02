using System.Linq;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower.Development
{
    // 스킬 확인용, 시작할 때와 grantKey를 누를 때 chipIds의 칩을 플레이어 인벤토리에 넣는다.
    // 넣기만 하므로 장착은 증강칩 패널(X)에서 우클릭이나 드래그로 한다.
    // 칩 모양은 ChipDispenser처럼 얻을 때마다 칸 수 안에서 새로 뽑는다.
    public class AugmentationChipGrantTester : MonoBehaviour
    {
        [Tooltip("넣을 칩의 Id (StatChip.tsv, SkillChip.tsv)")]
        [SerializeField] private int[] chipIds = { 2302, 2102 };
        [SerializeField] private bool grantOnStart = true;
        [SerializeField] private Key grantKey = Key.F9;
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
    }
}
