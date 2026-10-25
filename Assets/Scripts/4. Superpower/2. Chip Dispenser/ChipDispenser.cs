using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 닿으면 테이블에서 무작위 칩 하나를 무작위 모양으로 플레이어 인벤토리에 지급하고 사라지는 오브젝트. (데모)
    /// 트리거 콜라이더가 필요하다. 플레이어는 CharacterController라서 이쪽에 Rigidbody가 없어도 트리거가 발생한다.
    /// 패널은 열 때마다 인벤토리를 다시 읽으므로 이쪽은 UI를 모른다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChipDispenser : MonoBehaviour
    {
        // 디스펜서가 여러 개여도 테이블은 처음 지급할 때 한 번만 읽는다
        private static AugmentationChipTable table;

        // 컴포넌트를 처음 붙이거나 Reset할 때 콜라이더를 트리거로 맞춰둔다
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 인벤토리를 가진 오브젝트를 플레이어로 본다
            AugmentationChipInventory inventory = other.GetComponentInParent<AugmentationChipInventory>();
            if (inventory == null)
                return;

            // 같은 칩이라도 얻을 때마다 칸 수 안에서 모양을 새로 뽑는다
            AugmentationChipDef def = PickRandomDef();
            inventory.Add(def, ChipShapeLibrary.Roll(def.CellCount, ChipGridMetrics.GridSize));
            Destroy(gameObject);
        }

        private static AugmentationChipDef PickRandomDef()
        {
            if (table == null)
            {
                table = new AugmentationChipTable();
                table.Load();
            }

            IReadOnlyCollection<AugmentationChipDef> defs = table.GetDefs();
            return defs.ElementAt(Random.Range(0, defs.Count));
        }
    }
}
