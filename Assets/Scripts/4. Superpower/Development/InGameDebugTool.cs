using Study.Utilities;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower.Development
{
    // 플레이 중에 스킬을 확인할 때 쓰는 디버그 키 모음. 플레이어 오브젝트에 붙인다
    // - F10: 가지고 있는 모든 칩의 쿨타임을 바로 끝낸다 (장착 여부와 상관없이)
    // - F11: 플레이어가 자기 자신에게 damage만큼 피해를 준다 (두 번째 심장 같은 죽을 때 발동하는 칩 확인용)
    //   몬스터에게 맞을 때와 똑같이 CombatManager를 거치므로 보호막·회피·두 번째 심장이 그대로 끼어든다
    public class InGameDebugTool : MonoBehaviour
    {
        private const Key ResetCooldownKey = Key.F10;
        private const Key SelfDamageKey = Key.F11;
        private const Key PlusMaxHPKey = Key.F12;

        [SerializeField] private int damage = 999999;
        [SerializeField] private int hp = 999999; 

        private CombatEntity owner;
        private AugmentationChipInventory inventory;

        private void Awake()
        {
            owner = GetComponent<CombatEntity>();
            inventory = GetComponent<AugmentationChipInventory>();
        }

        private void Update()
        {
            if (ResetCooldownKey.GetKeyDown())
                ResetAllCooldowns();

            if (SelfDamageKey.GetKeyDown())
                DamageSelf();

            if (PlusMaxHPKey.GetKeyDown())
            {
                owner.CurrentStats.Add(StatType.MaxHp, hp);
                HealEvent healEvent = new HealEvent(owner, owner, hp);
                owner.TakeHeal(healEvent);
            }

        }

        private void ResetAllCooldowns()
        {
            foreach (OwnedChip chip in inventory.Chips)
            {
                // 능력치 칩은 스킬이 없다
                if (chip.Skill != null)
                    chip.Skill.ResetCooldown();
            }

            Debug.Log("[디버그] 모든 칩 쿨타임 초기화", this);
        }

        private void DamageSelf()
        {
            DamageInfo info = new DamageInfo(damage, owner.Collider.bounds.center, owner.Collider);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, owner, info));
            Debug.Log($"[디버그] 자해 {damage} 피해", this);
        }
    }
}
