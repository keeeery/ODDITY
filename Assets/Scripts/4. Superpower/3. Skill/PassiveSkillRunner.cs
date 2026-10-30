using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 트리거가 일어나면 스킬 슬롯의 패시브 스킬을 발동한다. 인벤토리가 있는 플레이어 오브젝트에 같이 붙인다.
    /// 슬롯에 어떤 패시브가 있는지는 모르고 트리거가 같은 패시브의 TryUse만 부른다. (전략 패턴에서 전략을 쓰는 쪽. SkillCaster와 같은 자리)
    /// 피해를 준 뒤·받은 뒤는 CombatManager의 피해 이벤트를 듣고,
    /// 피해를 받기 직전·죽기 직전은 CombatEntity의 Guard로 자기를 넣어 CombatEntity가 물어볼 때 답한다.
    /// </summary>
    public class PassiveSkillRunner : MonoBehaviour, IDamageGuard
    {
        private AugmentationChipInventory inventory;
        private CombatEntity owner;
        private CombatManager combatManager;

        private void Awake()
        {
            inventory = GetComponent<AugmentationChipInventory>();
            owner = GetComponent<CombatEntity>();
            owner.Guard = this;
        }

        private void Start()
        {
            combatManager = CombatManager.Instance;
            combatManager.OnDamageEventExecuted += HandleDamageExecuted;
        }

        private void OnDestroy()
        {
            if (combatManager != null)
                combatManager.OnDamageEventExecuted -= HandleDamageExecuted;
        }

        public bool BlockDamage(DamageEvent damageEvent) => Trigger(PassiveTrigger.BeforeTakeDamage);

        public bool PreventDeath() => Trigger(PassiveTrigger.BeforeDeath);

        // 실제로 들어간 피해만 본다. 보호막에 다 막혔거나 회피한 피해는 트리거가 아니다
        private void HandleDamageExecuted(DamageEvent damageEvent, int receivedDamage)
        {
            if (receivedDamage <= 0)
                return;

            if (ReferenceEquals(damageEvent.Receiver, owner))
                Trigger(PassiveTrigger.TookDamage);
            else if (ReferenceEquals(damageEvent.Sender, owner))
                Trigger(PassiveTrigger.DealtDamage);
        }

        // 트리거가 같은 패시브를 슬롯 순서대로 발동해 본다. 하나라도 발동했으면 true
        private bool Trigger(PassiveTrigger trigger)
        {
            SkillContext context = new SkillContext(transform, owner, transform.forward);
            bool triggered = false;
            foreach (OwnedChip chip in inventory.SkillSlots)
            {
                if (chip == null)
                    continue;

                PassiveSkill passive = chip.Skill as PassiveSkill;
                if (passive == null || passive.Trigger != trigger)
                    continue;

                if (passive.TryUse(context) == false)
                    continue;

                triggered = true;

                // 피해나 죽음을 막는 트리거는 하나가 막으면 충분하다. 같은 칩을 두 장 끼웠을 때 둘 다 내부 쿨타임을 쓰지 않게 멈춘다
                if (trigger == PassiveTrigger.BeforeTakeDamage || trigger == PassiveTrigger.BeforeDeath)
                    break;
            }

            return triggered;
        }
    }
}
