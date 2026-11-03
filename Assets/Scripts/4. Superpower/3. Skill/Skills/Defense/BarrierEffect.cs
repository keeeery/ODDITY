using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 보호막 이펙트에 붙어, 주인의 보호막이 사라지면(피해를 다 막아 깨지거나 시간이 끝나면) 이펙트를 지운다
    // 보호막이 피해를 막을 때마다 막는 소리(SkillChip.tsv의 Block)를 낸다
    // 이펙트 프리팹은 상용 에셋이라 고치지 않고, BarrierSkill이 이펙트를 띄울 때 이 컴포넌트를 붙인다
    public class BarrierEffect : MonoBehaviour
    {
        private CombatEntity owner;
        private SkillSounds sounds;

        public void Bind(CombatEntity owner, SkillSounds sounds)
        {
            this.owner = owner;
            this.sounds = sounds;
            owner.Hit += OnOwnerHit;
        }

        private void OnDestroy()
        {
            owner.Hit -= OnOwnerHit;
        }

        private void Update()
        {
            if (owner.Shield <= 0)
                Destroy(gameObject);
        }

        // Hit은 보호막이 피해를 깎기 전에 오므로, 이때 보호막이 남아 있으면 이번 피해를 보호막이 막는다
        private void OnOwnerHit(DamageEvent damageEvent)
        {
            if (owner.Shield > 0)
                sounds.Play("Block");
        }
    }
}
