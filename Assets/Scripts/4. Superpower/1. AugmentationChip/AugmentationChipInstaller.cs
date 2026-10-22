using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 패널의 주입자. 씬에 하나 둔다.
    /// 테이블을 로드하고, 플레이어의 인벤토리를 AugmentationChipPanel.Init으로 넘긴다.
    /// 패널(Canvas 프리팹)과 인벤토리(씬의 Player)를 둘 다 참조해야 하므로 프리팹이 아니라 씬에서 연결한다.
    /// </summary>
    public class AugmentationChipInstaller : MonoBehaviour
    {
        [SerializeField] private AugmentationChipInventory inventory; // 씬의 Player
        [SerializeField] private AugmentationChipPanel panel;         // Canvas 안의 패널

        // 다른 컴포넌트의 Awake(토글이 패널을 끄는 것 등)가 끝난 뒤에 주입한다
        private void Start()
        {
            AugmentationChipTable table = new AugmentationChipTable();
            table.Load();

            // 아직 칩을 얻는 경로가 없으므로 데모로 테이블의 칩 전부를 지급한다
            foreach (AugmentationChipDef def in table.GetDefs())
                AddChip(def);

            panel.Init(inventory.Chips);
        }

        // 칩 획득 시 부르면 된다
        public void AddChip(AugmentationChipDef def)
        {
            inventory.Add(def);
        }
    }
}
