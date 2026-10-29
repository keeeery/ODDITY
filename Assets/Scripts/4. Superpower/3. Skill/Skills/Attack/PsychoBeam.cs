using System.Collections;
using TeamOdd.Combat;
using TeamOdd.Player;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 레이저 프리팹(Resources/Skill/PsychoBeam)에 붙어 레이저를 쏘고 피해를 준다. 규칙은 PsychoBeamSkill 주석에 있다
    // PsychoBeamSkill이 Instantiate하고 Init으로 값을 넘기면 쏘기 시작하고, 끝나면 스스로 사라진다
    // 키를 떼면 PsychoBeamSkill.Release가 Stop을 부른다
    // 겉모습(지금은 임시 원기둥)과 이펙트는 프리팹에 붙인다. 프리팹은 매 프레임 레이저 시작과 끝 사이로 옮기고 늘인다 (높이 2인 원기둥 기준)
    public class PsychoBeam : MonoBehaviour
    {
        private const float SpawnHeight = 1.2f;   // 레이저가 나가는 발 기준 높이 (m)
        private const float SpawnDistance = 0.5f; // 레이저가 나가는 플레이어 앞 거리 (m)
        private const float BeamWidth = 1f;       // 겉모습 굵기 (m), 판정에는 쓰지 않음
        private const float AimDistance = 100f;   // 조준점을 찾을 거리 (m), PsychokinesisController의 투척과 같음

        private Transform caster;
        private CombatEntity owner;
        private PlayerController playerController;
        private float duration;
        private float range;
        private int dps;
        private float tickInterval;

        private bool isStopped; // 키를 뗐다. Run이 다음 프레임에 보고 끝낸다

        public void Init(Transform caster, CombatEntity owner, float duration, float range, int dps, float tickInterval)
        {
            this.caster = caster;
            this.owner = owner;
            this.duration = duration;
            this.range = range;
            this.dps = dps;
            this.tickInterval = tickInterval;

            playerController = caster.GetComponent<PlayerController>();
            playerController.IsMoveBlocked = true;

            StartCoroutine(Run());
        }

        public void Stop()
        {
            isStopped = true;
        }

        // 어떻게 끝나든(키를 뗌, 시간 끝, 죽음, 구르기) 사라질 때 걷기를 다시 풀어 준다
        private void OnDestroy()
        {
            // 플레이어가 사라져서 끝난 경우에는 풀어 줄 대상이 없다
            if (playerController != null)
                playerController.IsMoveBlocked = false;
        }

        private IEnumerator Run()
        {
            float tickTimer = tickInterval; // 처음 닿은 순간 바로 피해를 주도록 가득 채워 둔다

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (isStopped || caster == null || owner.IsDead) break;
                if (playerController.IsRolling) break;

                // 쏘는 중에 카메라를 돌려도 따라가도록 SkillContext.AimDirection(누른 순간의 방향) 대신 매 프레임 카메라 방향을 다시 구한다
                Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                Vector3 start = caster.position + Vector3.up * SpawnHeight + forward * SpawnDistance;
                Vector3 direction = GetCrosshairDirection(start);

                // 레이저가 처음 닿는 것. 없으면 사거리 끝까지
                Vector3 end = start + direction * range;
                Collider hitCollider = FindFirstHit(start, direction, ref end);

                tickTimer += Time.deltaTime;
                if (hitCollider != null && IsAlive(hitCollider) && tickTimer >= tickInterval)
                {
                    tickTimer = 0f;
                    Damage(hitCollider, end);
                }

                UpdateVisual(start, end);
                yield return null;
            }

            Destroy(gameObject);
        }

        // start에서 direction으로 range m 안에 처음 닿는 콜라이더. 닿으면 end를 닿은 곳으로 바꾼다. 없으면 null
        // 플레이어 자신과 들고 있는 염동력 오브젝트는 무시한다
        private Collider FindFirstHit(Vector3 start, Vector3 direction, ref Vector3 end)
        {
            Collider closest = null;
            float closestDistance = range;

            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsIgnored(hit)) continue;
                if (hit.distance > closestDistance) continue;

                closest = hit.collider;
                closestDistance = hit.distance;
                end = hit.point;
            }

            return closest;
        }

        // 레이저가 나가는 곳에서 화면 중앙 조준점으로 가는 방향 (PsychoOrbSkill의 같은 이름 메서드와 같은 방식)
        private Vector3 GetCrosshairDirection(Vector3 start)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = AimDistance;

            foreach (RaycastHit hit in Physics.RaycastAll(ray, AimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsIgnored(hit)) continue;

                distance = Mathf.Min(distance, hit.distance);
            }

            Vector3 direction = ray.GetPoint(distance) - start;

            // 카메라와 플레이어 사이의 가까운 장애물을 조준하면 뒤로 쏘게 되므로 그때는 카메라 방향
            if (Vector3.Dot(direction, ray.direction) <= 0f) direction = ray.direction;

            return direction.normalized;
        }

        // 플레이어 자신과 들고 있는 염동력 오브젝트는 레이저를 막지 않는다
        private bool IsIgnored(RaycastHit hit)
        {
            if (hit.collider.transform.IsChildOf(caster)) return true;
            if (hit.rigidbody != null && hit.rigidbody.TryGetComponent(out PsychokinesisObject held) && held.IsHeld) return true;

            return false;
        }

        // 피해를 줄 수 있는 적인지. 등록되지 않았거나 이미 죽었으면 false
        private static bool IsAlive(Collider collider)
        {
            if (MonsterRegistry.TryGetMonster(collider, out ICombatable monster) == false) return false;
            if (monster.GameObject.TryGetComponent(out CombatEntity entity) && entity.IsDead) return false;

            return true;
        }

        private void Damage(Collider collider, Vector3 point)
        {
            MonsterRegistry.TryGetMonster(collider, out ICombatable target);
            int damage = Mathf.RoundToInt(dps * tickInterval);

            DamageInfo info = new DamageInfo(damage, point, collider);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
        }

        // 프리팹을 start에서 end까지 늘인다. 원기둥은 높이 2(위아래 1씩)가 기본이라 y 크기는 길이의 절반
        private void UpdateVisual(Vector3 start, Vector3 end)
        {
            Vector3 offset = end - start;
            transform.position = start + offset * 0.5f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, offset);
            transform.localScale = new Vector3(BeamWidth, offset.magnitude * 0.5f, BeamWidth);
        }
    }
}
