using System.Collections.Generic;
using Study.MiniDefence;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Superpower;
using UnityEngine;

namespace TeamOdd.Reward
{
    // 보스 루트에 붙인다. 사망 연출과 칩 데이터는 기존 구현을 사용하고, 보상 규칙은 BossReward.tsv에서 RewardId로 고른다.
    // RewardId는 missions.tsv에서 이 보스 씬으로 가는 임무(NextStageName)의 Reward를 쓴다. 그래야 임무 창에 보이는 보상과 같다.
    [DisallowMultipleComponent]
    public sealed class BossChipReward : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비워 두면 자식에서 비활성 상태까지 포함해 찾습니다.")]
        [SerializeField] private BossDeathState deathState;
        [Tooltip("비워 두면 지급 시 Player의 인벤토리를 사용합니다.")]
        [SerializeField] private AugmentationChipInventory inventory;
        [SerializeField] private BossRewardPanel rewardPanel;

        [Header("Reward")]
        [Tooltip("missions.tsv에 이 보스 씬으로 가는 임무가 없을 때(테스트 씬 등) 쓰는 BossReward.tsv의 RewardId입니다. 임무가 있으면 임무의 Reward를 씁니다.")]
        [SerializeField] private int rewardId = 1;

        private bool hasGranted;

        private void Awake()
        {
            if (deathState == null)
                deathState = GetComponentInChildren<BossDeathState>(true);
        }

        private void OnEnable()
        {
            if (deathState == null)
            {
                Debug.LogError("[BossChipReward] BossDeathState를 연결해 주세요.", this);
                return;
            }

            deathState.Completed += GrantRewards;
            if (deathState.HasCompleted) GrantRewards();
        }

        private void OnDisable()
        {
            if (deathState != null) deathState.Completed -= GrantRewards;
        }

        private void GrantRewards()
        {
            if (hasGranted) return;

            if (inventory == null && Player.Player.Instance != null)
                inventory = Player.Player.Instance.GetComponent<AugmentationChipInventory>();

            if (inventory == null)
            {
                Debug.LogError("[BossChipReward] 지급할 AugmentationChipInventory가 없습니다.", this);
                if (SlowMotion.IsSlowed) SlowMotion.Resume();
                return;
            }

            int currentRewardId = ResolveRewardId();
            BossRewardTable rewardTable = new BossRewardTable();
            rewardTable.Load();
            if (rewardTable.TryGet(currentRewardId, out IReadOnlyList<BossRewardDef> rules) == false)
            {
                Debug.LogError($"[BossChipReward] BossReward.tsv에 {currentRewardId}번 보상이 없습니다.", this);
                if (SlowMotion.IsSlowed) SlowMotion.Resume();
                return;
            }

            AugmentationChipTable table = new AugmentationChipTable();
            table.Load();
            List<AugmentationChipDef> selected = PickRewards(rules, table.GetDefs());
            List<OwnedChip> granted = new List<OwnedChip>(selected.Count);

            // 같은 개체의 완료 알림이나 컴포넌트 재활성화로 재지급하지 않는다.
            hasGranted = true;
            foreach (AugmentationChipDef def in selected)
            {
                Vector2Int[] cells = ChipShapeLibrary.Roll(def.CellCount, ChipGridMetrics.GridSize);
                if (cells == null || cells.Length == 0) continue;
                granted.Add(inventory.Add(def, cells));
            }

            if (rewardPanel != null)
                rewardPanel.Open(granted);
            else
            {
                Debug.LogError("[BossChipReward] 보상은 지급했지만 Reward Panel이 연결되지 않아 결과창을 표시하지 못했습니다.", this);
                if (SlowMotion.IsSlowed) SlowMotion.Resume();
            }
        }

        // 이 보스가 있는 씬으로 가는 임무의 Reward, 없으면 인스펙터 값
        private int ResolveRewardId()
        {
            MissionTable missionTable = new MissionTable();
            missionTable.Load();
            string sceneName = gameObject.scene.name;
            foreach (MissionDef mission in missionTable.GetDefs)
            {
                if (mission.NextStageName == sceneName)
                    return mission.Reward;
            }

            return rewardId;
        }

        private List<AugmentationChipDef> PickRewards(IReadOnlyList<BossRewardDef> rules, IReadOnlyCollection<AugmentationChipDef> defs)
        {
            List<AugmentationChipDef> selected = new List<AugmentationChipDef>();
            HashSet<int> selectedIds = new HashSet<int>();
            List<AugmentationChipDef> candidates = new List<AugmentationChipDef>();

            foreach (BossRewardDef rule in rules)
            {
                candidates.Clear();
                foreach (AugmentationChipDef def in defs)
                {
                    if (rule.Contains(def.CellCount) && selectedIds.Contains(def.ID) == false)
                        candidates.Add(def);
                }

                if (candidates.Count < rule.Count)
                    Debug.LogWarning($"[BossChipReward] {rule.MinCell}~{rule.MaxCell}칸: {rule.Count}종 요청 중 후보가 {candidates.Count}종이어서 가능한 종류만 지급합니다.", this);

                int count = Mathf.Min(rule.Count, candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    int index = UnityEngine.Random.Range(0, candidates.Count);
                    AugmentationChipDef picked = candidates[index];
                    selected.Add(picked);
                    selectedIds.Add(picked.ID);
                    candidates[index] = candidates[candidates.Count - 1];
                    candidates.RemoveAt(candidates.Count - 1);
                }
            }

            selected.Sort((left, right) => right.CellCount.CompareTo(left.CellCount));
            return selected;
        }
    }
}
