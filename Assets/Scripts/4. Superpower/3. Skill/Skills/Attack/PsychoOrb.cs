using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 구체 프리팹(Resources/Skill/Projectile/PsychoOrb)에 붙어 구체를 날리고 피해를 준다. 규칙은 PsychoOrbSkill 주석에 있다
    // PsychoOrbSkill이 Instantiate하고 Init으로 값을 넘기면 혼자 날아가고, 다 날아가면 안으로 쪼그라든 뒤 터지며 사라진다
    // 프리팹 = 피해 판정용 트리거 SphereCollider(반경 0.5) + 키네마틱 Rigidbody + 블랙홀 이펙트(자식, 반복 재생)
    //          + 중력렌즈(자식 GravityLens, 구 둘레의 배경을 휘게 보이는 사각형. 셰이더가 카메라를 보게 세운다)
    // 프리팹 전체 크기를 반경에 맞춰 코드가 바꾸므로 지름 1 기준으로 만든다. 렌즈도 함께 커지고 쪼그라든다
    // 구체는 Ignore Raycast 레이어라 다른 레이캐스트에 걸리지 않는다
    public class PsychoOrb : MonoBehaviour
    {
        private const float CollapseTime = 0.15f;       // 끝날 때 안으로 쪼그라드는 시간 (초)
        private const float CollapseScale = 0.15f;      // 다 쪼그라들었을 때 크기 (처음 크기 대비)
        private const float BurstLifetime = 1f;         // 터지는 이펙트를 지우기까지 기다리는 시간 (초)

        [SerializeField] private GameObject burstPrefab; // 쪼그라든 뒤 터지는 이펙트 (Resources/Skill/Effect/PsychoOrbBurst)

        private Vector3 direction;
        private CombatEntity owner;
        private float speed;
        private float slowSpeed;
        private float range;
        private float radius;
        private int powerPerSecond;
        private float tickInterval;
        private int groggyRate;     // 무력화 배율 (준 피해의 %)
        private float groggyCarry;  // 틱마다 버린 무력화 소수점, 다음 틱에 더한다
        private SkillSounds sounds;
        private AudioSource hum; // 날아가는 동안 반복하는 웅웅거림 (SkillChip.tsv의 Hum)

        // 지금 트리거 안에 들어와 있는 적 콜라이더. 들어오고 나갈 때 OnTriggerEnter/Exit가 고친다
        private readonly List<Collider> touching = new List<Collider>();

        // 콜라이더가 여러 개인 적이 한 번에 여러 번 맞지 않도록 이번 틱에 맞은 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();

        public void Init(Vector3 direction, CombatEntity owner, float speed, float slowSpeed, float range, float radius, int powerPerSecond, float tickInterval, int groggyRate, SkillSounds sounds)
        {
            this.direction = direction;
            this.owner = owner;
            this.speed = speed;
            this.slowSpeed = slowSpeed;
            this.range = range;
            this.radius = radius;
            this.powerPerSecond = powerPerSecond;
            this.tickInterval = tickInterval;
            this.groggyRate = groggyRate;
            this.sounds = sounds;

            transform.localScale = Vector3.one * radius * 2f;
            hum = sounds.PlayLoop("Hum");
            StartCoroutine(Run());
        }

        // 다 날기 전에 씬이 바뀌어 사라져도 소리가 남지 않게 끈다
        private void OnDestroy()
        {
            SkillSounds.StopLoop(hum);
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
            float tickTimer = tickInterval; // 처음 닿은 순간 바로 피해를 주도록 가득 채워 둔다

            for (float traveled = 0f; traveled < range; )
            {
                tickTimer += Time.deltaTime;

                bool isTouching = RemoveGoneAndCheckTouching();
                if (isTouching && tickTimer >= tickInterval)
                {
                    tickTimer = 0f;
                    DamageTouching();
                }

                float step = (isTouching ? slowSpeed : speed) * Time.deltaTime;
                transform.position += direction * step;
                traveled += step;

                yield return null;
            }

            yield return Collapse();
        }

        // 제자리에서 점점 빠르게 쪼그라든 뒤 터지는 이펙트를 남기고 사라진다. 이때는 피해를 주지 않는다
        private IEnumerator Collapse()
        {
            Vector3 startScale = transform.localScale;

            // 웅웅거림도 쪼그라드는 동안 줄인다. 줄이는 중에 OnDestroy가 다시 끄지 않도록 비워 둔다
            SkillSounds.StopLoop(hum, CollapseTime);
            hum = null;

            for (float elapsed = 0f; elapsed < CollapseTime; elapsed += Time.deltaTime)
            {
                float progress = elapsed / CollapseTime;
                transform.localScale = startScale * Mathf.Lerp(1f, CollapseScale, progress * progress); // 제곱이라 끝으로 갈수록 빨라진다

                yield return null;
            }

            // 터지는 이펙트의 충격파 고리가 날아온 방향을 마주 보게 돌린다
            GameObject burst = Instantiate(burstPrefab, transform.position, Quaternion.LookRotation(direction));
            burst.transform.localScale = Vector3.one * radius;
            Destroy(burst, BurstLifetime);

            Destroy(gameObject);
        }

        // 사라지거나 꺼지거나 죽은 콜라이더는 OnTriggerExit가 오지 않으므로 여기서 뺀다. 남은 적이 하나라도 있으면 true
        // 빼면서 돌기 위해 뒤에서부터 돈다
        private bool RemoveGoneAndCheckTouching()
        {
            for (int i = touching.Count - 1; i >= 0; i--)
            {
                if (IsAlive(touching[i]) == false) touching.RemoveAt(i);
            }

            return touching.Count > 0;
        }

        // 피해를 줄 수 있는 적인지. 사라졌거나(Destroy) 꺼졌거나 등록이 풀렸거나 죽었으면 false
        private static bool IsAlive(Collider collider)
        {
            if (collider == null || collider.enabled == false || collider.gameObject.activeInHierarchy == false) return false;
            if (MonsterRegistry.TryGetMonster(collider, out ICombatable monster) == false) return false;

            if (monster.GameObject.TryGetComponent(out CombatEntity entity) && entity.IsDead) return false;

            return true;
        }

        // 트리거 안의 모든 적에게 한 틱만큼 피해를 준다. 죽은 적은 RemoveGoneAndCheckTouching이 방금 뺐다
        private void DamageTouching()
        {
            hitTargets.Clear();
            int damage = SuperpowerSkill.PowerToDamage(owner, powerPerSecond * tickInterval);
            int groggy = -1;

            foreach (Collider collider in touching)
            {
                MonsterRegistry.TryGetMonster(collider, out ICombatable target);
                if (hitTargets.Contains(target)) continue;

                hitTargets.Add(target);
                // 무력화는 틱마다 한 번 계산해 이번 틱에 맞은 적 모두에게 같은 양을 준다. 아무도 안 맞은 틱은 소수점도 쌓지 않음
                if (groggy < 0) groggy = SuperpowerSkill.DamageToGroggy(damage, groggyRate, ref groggyCarry);
                DamageInfo info = new DamageInfo(damage, collider.ClosestPoint(transform.position), collider, groggy: groggy);
                CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
            }

            // 적이 여럿이어도 틱마다 한 번만 탁
            sounds.Play("Tick");
        }
    }
}
