using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 보호막 이펙트에 붙어, 주인의 보호막이 사라지면(피해를 다 막아 깨지거나 시간이 끝나면) 이펙트를 지운다
    // 이펙트 프리팹은 상용 에셋이라 고치지 않고, BarrierSkill이 이펙트를 띄울 때 이 컴포넌트를 붙인다
    public class BarrierEffect : MonoBehaviour
    {
        private CombatEntity owner;

        public void Bind(CombatEntity owner)
        {
            this.owner = owner;
        }

        private void Update()
        {
            if (owner.Shield <= 0)
                Destroy(gameObject);
        }
    }
}
