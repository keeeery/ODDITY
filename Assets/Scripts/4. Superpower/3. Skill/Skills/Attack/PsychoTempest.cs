using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 폭풍 프리팹(Resources/Skill/Projectile/PsychoTempest)에 붙어 적을 쫓으며 피해를 준다. 규칙은 PsychoTempestSkill 주석에 있다
    // PsychoTempestSkill이 Instantiate하고 Init으로 값을 넘기면 혼자 움직이고, Duration초가 지나면 스스로 사라진다
    // 겉모습(지금은 임시 납작한 원기둥)과 이펙트는 프리팹에 붙인다. 프리팹 가로 크기는 반경에 맞춰 코드가 바꾸므로 지름 1 기준으로 만든다
    public class PsychoTempest : MonoBehaviour
    {
        private const float SearchRadius = 30f;     // 쫓을 적을 찾는 거리 (m)
        private const float SpinSpeed = 360f;       // 겉모습이 도는 속도 (도/초)

        private CombatEntity owner;
        private float duration;
        private int dps;
        private float trackSpeed;
        private float radius;       // 폭풍이 피해를 주는 반경 (m)
        private float tickInterval; // 피해를 주는 간격 (초)

        // 콜라이더가 여러 개인 적이 한 번에 여러 번 맞지 않도록 이번 틱에 맞은 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();

        public void Init(CombatEntity owner, float duration, int dps, float trackSpeed, float radius, float tickInterval)
        {
            this.owner = owner;
            this.duration = duration;
            this.dps = dps;
            this.trackSpeed = trackSpeed;
            this.radius = radius;
            this.tickInterval = tickInterval;

            transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Collider target = null;
            float tickTimer = 0f;

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (IsAlive(target) == false)
                    target = FindClosestEnemy(transform.position);

                if (target != null)
                {
                    Vector3 goal = target.bounds.center;
                    goal.y = 0f;
                    transform.position = Vector3.MoveTowards(transform.position, goal, trackSpeed * Time.deltaTime);
                }

                transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime);

                tickTimer += Time.deltaTime;
                if (tickTimer >= tickInterval)
                {
                    tickTimer -= tickInterval;
                    DamageAround(transform.position);
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        // 폭풍에서 가장 가까운 살아 있는 적의 콜라이더. 없으면 null
        private static Collider FindClosestEnemy(Vector3 center)
        {
            Collider closest = null;
            float closestDistance = SearchRadius;

            foreach (Collider collider in Physics.OverlapSphere(center, SearchRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsAlive(collider) == false) continue;

                float distance = Vector3.Distance(center, collider.bounds.center);
                if (distance > closestDistance) continue;

                closest = collider;
                closestDistance = distance;
            }

            return closest;
        }

        // 아직 쫓을 수 있는 적인지. 사라졌거나(Destroy) 꺼졌거나 등록이 풀렸거나 죽었으면 false
        private static bool IsAlive(Collider collider)
        {
            if (collider == null || collider.enabled == false || collider.gameObject.activeInHierarchy == false) return false;
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
