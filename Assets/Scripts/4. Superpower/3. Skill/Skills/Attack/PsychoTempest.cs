using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 폭풍 프리팹(Resources/Skill/Projectile/PsychoTempest)에 붙어 적을 쫓으며 피해를 준다. 규칙은 PsychoTempestSkill 주석에 있다
    // PsychoTempestSkill이 Instantiate하고 Init으로 값을 넘기면 혼자 움직이고, Duration초가 지나면 이펙트를 서서히 끄고 사라진다
    // 이펙트는 생길 때 FadeInTime초 동안 서서히 나타나고, 끝날 때 FadeOutTime초 동안 서서히 사라진다
    // - 이펙트 셰이더(Shader_IntegratedEffect)가 _MaskCutOut을 투명도에 곱하므로, 렌더러마다 MaterialPropertyBlock으로 이 값을 0~1 사이로 바꾼다
    // 프리팹 = 피해 판정용 트리거 SphereCollider(반경 0.5) + 키네마틱 Rigidbody + 폭풍 이펙트(자식, 반복 재생)
    // 프리팹 전체 크기를 반경에 맞춰 코드가 바꾸므로 지름 1 기준으로 만든다
    // 폭풍은 Ignore Raycast 레이어라 다른 레이캐스트에 걸리지 않는다
    public class PsychoTempest : MonoBehaviour
    {
        private const float SearchRadius = 30f;     // 쫓을 적을 찾는 거리 (m)
        private const float FadeInTime = 0.5f;      // 생길 때 이펙트가 서서히 나타나는 시간 (초)
        private const float FadeOutTime = 1f;       // 끝날 때 이펙트가 서서히 사라지는 시간 (초)

        private static readonly int FadeId = Shader.PropertyToID("_MaskCutOut");

        private CombatEntity owner;
        private float duration;
        private int powerPerSecond;
        private float trackSpeed;
        private float tickInterval; // 피해를 주는 간격 (초)
        private int groggyRate;     // 무력화 배율 (준 피해의 %)
        private float groggyCarry;  // 틱마다 버린 무력화 소수점, 다음 틱에 더한다
        private AudioSource loop;   // 폭풍이 도는 동안 반복하는 소리 (SkillChip.tsv의 Loop)
        private Renderer[] effectRenderers; // 페이드를 걸 이펙트 렌더러
        private MaterialPropertyBlock fadeBlock;

        // 지금 트리거 안에 들어와 있는 적 콜라이더. 들어오고 나갈 때 OnTriggerEnter/Exit가 고친다
        private readonly List<Collider> touching = new List<Collider>();

        // 콜라이더가 여러 개인 적이 한 번에 여러 번 맞지 않도록 이번 틱에 맞은 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();

        public void Init(CombatEntity owner, float duration, int powerPerSecond, float trackSpeed, float radius, float tickInterval, int groggyRate, SkillSounds sounds)
        {
            this.owner = owner;
            this.duration = duration;
            this.powerPerSecond = powerPerSecond;
            this.trackSpeed = trackSpeed;
            this.tickInterval = tickInterval;
            this.groggyRate = groggyRate;

            transform.localScale = Vector3.one * radius * 2f;
            loop = sounds.PlayLoop("Loop");

            effectRenderers = GetComponentsInChildren<ParticleSystemRenderer>();
            fadeBlock = new MaterialPropertyBlock();
            SetFade(0f);

            StartCoroutine(Run());
        }

        // 다 돌기 전에 씬이 바뀌어 사라져도 소리가 남지 않게 끈다
        private void OnDestroy()
        {
            SkillSounds.StopLoop(loop);
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
                SetFade(elapsed / FadeInTime);

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

            // 소리도 이펙트와 함께 서서히 줄인다. 줄이는 중에 OnDestroy가 다시 끄지 않도록 비워 둔다
            SkillSounds.StopLoop(loop, FadeOutTime);
            loop = null;

            // 페이드 인이 끝나기 전에 끝났어도 그때 보이던 정도에서 줄여 나감
            float startFade = Mathf.Clamp01(duration / FadeInTime);
            for (float elapsed = 0f; elapsed < FadeOutTime; elapsed += Time.deltaTime)
            {
                SetFade(startFade * (1f - elapsed / FadeOutTime));
                yield return null;
            }

            Destroy(gameObject);
        }

        // 이펙트가 보이는 정도(0~1), 갑자기 나타나거나 꺼지지 않도록 부드럽게(smoothstep) 바꿔 넣는다
        private void SetFade(float amount)
        {
            float t = Mathf.Clamp01(amount);
            fadeBlock.SetFloat(FadeId, t * t * (3f - 2f * t));

            foreach (Renderer effect in effectRenderers)
                effect.SetPropertyBlock(fadeBlock);
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
            int groggy = -1;

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
                // 무력화는 틱마다 한 번 계산해 이번 틱에 맞은 적 모두에게 같은 양을 준다. 아무도 안 맞은 틱은 소수점도 쌓지 않음
                if (groggy < 0) groggy = SuperpowerSkill.DamageToGroggy(damage, groggyRate, ref groggyCarry);
                // 회오리는 바닥에 붙어 다녀서 그대로 재면 히트 이펙트가 발밑에 나옴
                DamageInfo info = new DamageInfo(damage, HitPoint.TowardBody(target, collider, transform.position), collider, groggy: groggy);
                CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
            }
        }
    }
}
