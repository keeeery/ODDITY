using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 구체 프리팹(Resources/Skill/PsychoOrb)에 붙어 구체를 날리고 피해를 준다. 규칙은 PsychoOrbSkill 주석에 있다
    // PsychoOrbSkill이 Instantiate하고 Init으로 값을 넘기면 혼자 날아가고, 다 날아가면 스스로 사라진다
    // 겉모습(지금은 임시 구)과 이펙트는 프리팹에 붙인다. 프리팹 크기는 반경에 맞춰 코드가 바꾸므로 지름 1 기준으로 만든다
    public class PsychoOrb : MonoBehaviour
    {
        private Vector3 direction;
        private CombatEntity owner;
        private float speed;
        private float slowSpeed;
        private float range;
        private float radius;
        private int dps;
        private float tickInterval;

        // 콜라이더가 여러 개인 적이 한 번에 여러 번 맞지 않도록 이번 틱에 맞은 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();

        public void Init(Vector3 direction, CombatEntity owner, float speed, float slowSpeed, float range, float radius, int dps, float tickInterval)
        {
            this.direction = direction;
            this.owner = owner;
            this.speed = speed;
            this.slowSpeed = slowSpeed;
            this.range = range;
            this.radius = radius;
            this.dps = dps;
            this.tickInterval = tickInterval;

            transform.localScale = Vector3.one * radius * 2f;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float tickTimer = tickInterval; // 처음 닿은 순간 바로 피해를 주도록 가득 채워 둔다

            for (float traveled = 0f; traveled < range; )
            {
                tickTimer += Time.deltaTime;

                bool touching = IsTouchingEnemy(transform.position);
                if (touching && tickTimer >= tickInterval)
                {
                    tickTimer = 0f;
                    DamageAround(transform.position);
                }

                float step = (touching ? slowSpeed : speed) * Time.deltaTime;
                transform.position += direction * step;
                traveled += step;

                yield return null;
            }

            Destroy(gameObject);
        }

        private bool IsTouchingEnemy(Vector3 center)
        {
            foreach (Collider collider in Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsAlive(collider)) return true;
            }

            return false;
        }

        // 피해를 줄 수 있는 적인지. 등록되지 않았거나 이미 죽었으면 false
        private static bool IsAlive(Collider collider)
        {
            if (MonsterRegistry.TryGetMonster(collider, out ICombatable monster) == false) return false;
            if (monster.GameObject.TryGetComponent(out CombatEntity entity) && entity.IsDead) return false;

            return true;
        }

        private void DamageAround(Vector3 center)
        {
            hitTargets.Clear();
            int damage = Mathf.RoundToInt(dps * tickInterval);

            foreach (Collider collider in Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsAlive(collider) == false) continue;

                MonsterRegistry.TryGetMonster(collider, out ICombatable target);
                if (hitTargets.Contains(target)) continue;

                hitTargets.Add(target);
                DamageInfo info = new DamageInfo(damage, collider.ClosestPoint(center), collider);
                CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
            }
        }
    }
}
