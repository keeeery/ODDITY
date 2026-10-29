using System;
using System.Collections.Generic;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Superpower;
using UnityEngine;

namespace TeamOdd.Reward
{
    [Serializable]
    public class ChipRewardRule
    {
        [Min(1)] public int CellCount;
        [Tooltip("이 CellCount에서 중복 없이 지급할 칩 종류 수")]
        [Min(0)] public int Count;

        public ChipRewardRule(int cellCount, int count)
        {
            CellCount = cellCount;
            Count = count;
        }
    }

    // 보스 루트에 붙인다. 사망 연출과 칩 데이터는 기존 구현을 사용하고 보상 규칙만 여기서 정한다.
    [DisallowMultipleComponent]
    public sealed class BossChipReward : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비워 두면 자식에서 비활성 상태까지 포함해 찾습니다.")]
        [SerializeField] private BossDeathState deathState;
        [Tooltip("비워 두면 지급 시 Player의 인벤토리를 사용합니다.")]
        [SerializeField] private AugmentationChipInventory inventory;
        [SerializeField] private BossRewardPanel rewardPanel;

        [Header("Reward Rules")]
        [Tooltip("스킬·스탯 칩을 합쳐 CellCount별로 추첨합니다. 기존 보유 칩도 후보에 포함됩니다.")]
        [SerializeField] private List<ChipRewardRule> rules = new()
        {
            new ChipRewardRule(6, 1),
            new ChipRewardRule(5, 2),
            new ChipRewardRule(3, 2),
        };

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

            AugmentationChipTable table = new AugmentationChipTable();
            table.Load();
            List<AugmentationChipDef> selected = PickRewards(table.GetDefs());
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

        private List<AugmentationChipDef> PickRewards(IReadOnlyCollection<AugmentationChipDef> defs)
        {
            List<AugmentationChipDef> selected = new List<AugmentationChipDef>();
            HashSet<int> selectedIds = new HashSet<int>();
            List<AugmentationChipDef> candidates = new List<AugmentationChipDef>();

            foreach (ChipRewardRule rule in rules)
            {
                if (rule == null || rule.CellCount < 1 || rule.Count <= 0) continue;
                candidates.Clear();
                foreach (AugmentationChipDef def in defs)
                {
                    if (def.CellCount == rule.CellCount && selectedIds.Contains(def.ID) == false)
                        candidates.Add(def);
                }

                if (candidates.Count < rule.Count)
                    Debug.LogWarning($"[BossChipReward] CellCount {rule.CellCount}: {rule.Count}종 요청 중 후보가 {candidates.Count}종이어서 가능한 종류만 지급합니다.", this);

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
