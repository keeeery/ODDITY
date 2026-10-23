using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 닿으면 테이블에서 무작위 칩 하나를 지급하고 사라지는 오브젝트. (데모)
    /// 트리거 콜라이더가 필요하다. 플레이어는 CharacterController라서 이쪽에 Rigidbody가 없어도 트리거가 발생한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChipDispenser : MonoBehaviour
    {
        [SerializeField] private AugmentationChipInstaller installer; // 씬의 Installer. 테이블과 지급 경로를 가지고 있다

        // 컴포넌트를 처음 붙이거나 Reset할 때 콜라이더를 트리거로 맞춰둔다
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 인벤토리를 가진 오브젝트를 플레이어로 본다
            if (other.GetComponentInParent<AugmentationChipInventory>() == null)
                return;

            IReadOnlyCollection<AugmentationChipDef> defs = installer.Table.GetDefs();
            AugmentationChipDef def = defs.ElementAt(Random.Range(0, defs.Count));
            installer.AddChip(def);

            Destroy(gameObject);
        }
    }
}
