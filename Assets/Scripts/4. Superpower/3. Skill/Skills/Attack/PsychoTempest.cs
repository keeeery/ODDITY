using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 폭풍 프리팹(Resources/Skill/Projectile/PsychoTempest)에 붙어 적을 쫓으며 피해를 준다. 규칙은 PsychoTempestSkill 주석에 있다
    // PsychoTempestSkill이 Instantiate하고 Init으로 값을 넘기면 혼자 움직이고, Duration초가 지나면 이펙트를 서서히 끄고 사라진다
    // 프리팹 = 피해 판정용 트리거 SphereCollider(반경 0.5) + 키네마틱 Rigidbody + 폭풍 이펙트(자식, 반복 재생)
    // 프리팹 전체 크기를 반경에 맞춰 코드가 바꾸므로 지름 1 기준으로 만든다
    // 폭풍은 Ignore Raycast 레이어라 다른 레이캐스트에 걸리지 않는다
    public class PsychoTempest : MonoBehaviour
    {
        private const float SearchRadius = 30f;     // 쫓을 적을 찾는 거리 (m)
        private const float FadeOutTime = 1f;       // 끝날 때 이펙트가 사라지기를 기다리는 시간 (초)

        private CombatEntity owner;
        private float duration;
        private int powerPerSecond;
        private float trackSpeed;
        private float tickInterval; // 피해를 주는 간격 (초)

        // 지금 트리거 안에 들어와 있는 적 콜라이더. 들어오고 나갈 때 OnTriggerEnter/Exit가 고친다
        private readonly List<Collider> touching = new List<Collider>();

        // 콜라이더가 여러 개인 적이 한 번에 여러 번 맞지 않도록 이번 틱에 맞은 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();

        public void Init(CombatEntity owner, float duration, int powerPerSecond, float trackSpeed, float radius, float tickInterval)
        {
            this.owner = owner;
            this.duration = duration;
            this.powerPerSecond = powerPerSecond;
            this.trackSpeed = trackSpeed;
            this.tickInterval = tickInterval;

            transform.localScale = Vector3.one * radius * 2f;
            StartCoroutine(Run());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (MonsterRegistry.TryGetMonster(other, out _) == false) return;
            if (touching.Contains(other)) return;

            touching.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            touching.Remove(other);
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

                tickTimer += Time.deltaTime;
                if (tickTimer >= tickInterval)
                {
                    tickTimer -= tickInterval;
                    DamageTouching();
                }

                yield return null;
            }

            // 새 파티클만 멈추고 이미 나온 파티클은 수명을 마치게 둔 뒤 지운다
            foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>())
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Destroy(gameObject, FadeOutTime);
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

        // 트리거 안의 모든 적에게 한 틱만큼 피해를 준다
        private void DamageTouching()
        {
            hitTargets.Clear();
            int damage = SuperpowerSkill.PowerToDamage(owner, powerPerSecond * tickInterval);

            // 사라지거나 꺼진 콜라이더는 OnTriggerExit가 오지 않으므로 여기서 뺀다. 빼면서 돌기 위해 뒤에서부터 돈다
            for (int i = touching.Count - 1; i >= 0; i--)
            {
                Collider collider = touching[i];
                if (IsAlive(collider) == false)
                {
                    touching.RemoveAt(i);
                    continue;
                }

                MonsterRegistry.TryGetMonster(collider, out ICombatable target);
                if (hitTargets.Contains(target)) continue;

                hitTargets.Add(target);
                DamageInfo info = new DamageInfo(damage, collider.ClosestPoint(transform.position), collider);
                CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
            }
        }
    }
}
