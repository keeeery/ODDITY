using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 닿으면 테이블의 모든 칩을 하나씩 무작위 모양으로 플레이어 인벤토리에 지급하고 사라지는 오브젝트. (시연용)
    /// 트리거 콜라이더가 필요하다. 플레이어는 CharacterController라서 이쪽에 Rigidbody가 없어도 트리거가 발생한다.
    /// 패널은 열 때마다 인벤토리를 다시 읽으므로 이쪽은 UI를 모른다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChipDispenser : MonoBehaviour
    {
        // 디스펜서가 여러 개여도 테이블은 처음 지급할 때 한 번만 읽는다
        private static AugmentationChipTable table;

        // 인벤토리 칩 목록처럼 씬을 넘어 남는다. 씬을 다시 불러와 디스펜서가 새로 생겨도 한 번만 지급한다
        private static bool dispensed;

        // 컴포넌트를 처음 붙이거나 Reset할 때 콜라이더를 트리거로 맞춰둔다
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            if (dispensed)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 인벤토리를 가진 오브젝트를 플레이어로 본다
            AugmentationChipInventory inventory = other.GetComponentInParent<AugmentationChipInventory>();
            if (inventory == null)
                return;

            if (table == null)
            {
                table = new AugmentationChipTable();
                table.Load();
            }

            // 모양은 칩마다 칸 수 안에서 새로 뽑는다
            IReadOnlyCollection<AugmentationChipDef> defs = table.GetDefs();
            foreach (AugmentationChipDef def in defs)
            {
                inventory.Add(def, ChipShapeLibrary.Roll(def.CellCount, ChipGridMetrics.GridSize));
            }

            dispensed = true;
            Destroy(gameObject);
        }
    }
}
