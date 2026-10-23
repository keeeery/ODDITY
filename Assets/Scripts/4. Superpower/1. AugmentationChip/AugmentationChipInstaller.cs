using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 증강칩 패널의 주입자. 씬에 하나 둔다.
    /// 테이블을 로드하고, 플레이어의 런타임 스탯으로 장착 상태(AugmentationChipEquipment)를 만들어
    /// 인벤토리와 함께 AugmentationChipPanel.Init으로 넘긴다.
    /// 패널(Canvas 프리팹)과 인벤토리(씬의 Player)를 둘 다 참조해야 하므로 프리팹이 아니라 씬에서 연결한다.
    /// </summary>
    public class AugmentationChipInstaller : MonoBehaviour
    {
        [SerializeField] private AugmentationChipInventory inventory; // 씬의 Player
        [SerializeField] private CombatEntity player;                 // 씬의 Player. 장착 결과를 CurrentStats에 반영한다
        [SerializeField] private AugmentationChipPanel panel;         // Canvas 안의 패널

        private bool panelInitialized;

        // 칩을 지급하는 쪽(ChipDispenser 등)이 무작위로 고를 때 쓴다
        public AugmentationChipTable Table { get; private set; }

        // 장착된 칩 목록과 스탯 반영. 정비소 등 UI 밖에서도 이걸로 장착 상태를 읽는다
        public AugmentationChipEquipment Equipment { get; private set; }

        private void Awake()
        {
            Table = new AugmentationChipTable();
            Table.Load();
        }

        // 다른 컴포넌트의 Awake(토글이 패널을 끄는 것, CombatEntity가 CurrentStats를 만드는 것 등)가 끝난 뒤에 주입한다
        private void Start()
        {
            // 보드 크기는 일단 프리팹 그리드 칸 수에 맞춘다
            Equipment = new AugmentationChipEquipment(panel.GridSize, player.CurrentStats);
            panel.Init(inventory.Chips, Equipment);
            panelInitialized = true;
        }

        // 칩 획득 시 부르면 된다
        // 패널 Init 전에 들어온 칩은 Init이 인벤토리를 읽을 때 같이 들어가므로, Init 이후에 들어온 칩만 패널에 직접 넣는다
        public void AddChip(AugmentationChipDef def)
        {
            OwnedChip owned = inventory.Add(def);

            if (panelInitialized)
                panel.Add(owned);
        }
    }
}
