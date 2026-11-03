using System.Collections;
using System.Collections.Generic;
using TeamOdd.CameraMove;
using TeamOdd.Combat;
using TeamOdd.Player;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 레이저 프리팹(Resources/Skill/Projectile/PsychoBeam)에 붙어 레이저를 쏘고 피해를 준다. 규칙은 PsychoBeamSkill 주석에 있다
    // PsychoBeamSkill이 Instantiate하고 Init으로 값을 넘기면 쏘기 시작하고, 끝나면 스스로 사라진다
    // 키를 떼면 PsychoBeamSkill.Release가 Stop을 부른다
    // 프리팹 = 피해 판정용 트리거 CapsuleCollider(Z 방향, z 0~1) + 키네마틱 Rigidbody + 에네르기파 이펙트(자식, z 0~1)
    // 매 프레임 프리팹을 레이저 시작점에 두고 앞(+Z)을 레이저 방향으로 돌린 뒤, z 크기를 레이저 길이로 바꾼다. 그래서 프리팹은 길이 1 기준으로 만든다
    // 굵기는 프리팹에서 정한다 (콜라이더 반경, 이펙트 x/y 크기)
    // 레이저는 Ignore Raycast 레이어라 다른 레이캐스트에 걸리지 않는다
    public class PsychoBeam : MonoBehaviour
    {
        private const float ShoulderHeight = 1f;     // 플레이어 어깨 높이 (m, 발 기준). 레이저 시작점을 정하는 중심
        private const float ArmLength = 0.6f;           // 플레이어 팔 길이 (m)
        private const float Reach = ArmLength * 1.1f;   // 어깨에서 레이저 시작점까지 거리, 팔보다 10% 길어 두 팔이 쭉 펴진다
        private const float AimDistance = 100f;         // 조준점을 찾을 거리 (m), PsychokinesisController의 투척과 같음
        private const float MinLength = 0.1f;           // 벽에 붙어 쏠 때도 크기가 0이 되지 않게 하는 가장 짧은 길이 (m)
        private const float ShakeTrauma = 0.1f;         // 쏘는 동안 유지할 카메라 흔들림 세기 (0~1, 실제 세기는 제곱이라 0.3이면 약하게)
        private const float ShakeFadeTime = 0.2f;       // 키를 떼면 흔들림이 잦아드는 시간 (초)

        private Transform caster;
        private CombatEntity owner;
        private PlayerController playerController;
        private CameraController cameraController;
        private float duration;
        private float range;
        private int powerPerSecond;
        private float tickInterval;
        private int groggyRate;     // 무력화 배율 (준 피해의 %)
        private float groggyCarry;  // 틱마다 버린 무력화 소수점, 다음 틱에 더한다

        private AudioSource loop; // 쏘는 동안 반복하는 소리 (SkillChip.tsv의 Loop)
        private PsychoBeamPose pose; // 쏘는 동안 플레이어에 붙여 시전 애니메이션을 틀고 상체와 손을 레이저 쪽으로 맞추는 자세
        private bool isStopped; // 키를 뗐다. Run이 다음 프레임에 보고 끝낸다

        // 지금 트리거 안에 들어와 있는 적 콜라이더. 들어오고 나갈 때 OnTriggerEnter/Exit가 고친다
        private readonly List<Collider> touching = new List<Collider>();

        // 콜라이더가 여러 개인 적이 한 번에 여러 번 맞지 않도록 이번 틱에 맞은 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();

        public void Init(Transform caster, CombatEntity owner, float duration, float range, int powerPerSecond, float tickInterval, int groggyRate, SkillSounds sounds)
        {
            loop = sounds.PlayLoop("Loop");

            this.caster = caster;
            this.owner = owner;
            this.duration = duration;
            this.range = range;
            this.powerPerSecond = powerPerSecond;
            this.tickInterval = tickInterval;
            this.groggyRate = groggyRate;

            playerController = caster.GetComponent<PlayerController>();
            playerController.IsMoveBlocked = true;
            cameraController = caster.GetComponent<Player.Player>().CameraController;

            // OnAnimatorIK는 Animator가 붙은 오브젝트에서만 불리므로 플레이어에 붙인다
            pose = caster.gameObject.AddComponent<PsychoBeamPose>();

            StartCoroutine(Run());
        }

        public void Stop()
        {
            isStopped = true;
        }

        // 어떻게 끝나든(키를 뗌, 시간 끝, 죽음, 구르기, 피격) 사라질 때 걷기를 다시 풀어 주고 시전 자세를 끝낸다
        private void OnDestroy()
        {
            // 소리는 카메라 흔들림처럼 짧게 잦아들며 끝난다
            SkillSounds.StopLoop(loop, ShakeFadeTime);

            // 플레이어가 사라져서 끝난 경우에는 풀어 줄 대상이 없다
            if (playerController == null) return;

            playerController.IsMoveBlocked = false;
            pose.Release();
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

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (isStopped || caster == null || owner.IsDead) break;
                if (playerController.IsRolling || playerController.IsHit) break;

                // 쏘는 중에 카메라를 돌려도 따라가도록 SkillContext.AimDirection(누른 순간의 방향) 대신 매 프레임 카메라 방향을 다시 구한다
                Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;

                // 어깨 높이를 중심으로 반지름 Reach인 구 위, 조준점 쪽에서 레이저가 나간다
                Vector3 shoulder = caster.position + Vector3.up * ShoulderHeight;
                Vector3 direction = GetCrosshairDirection(shoulder);
                Vector3 start = shoulder + direction * Reach;

                // 몸은 레이저와 같은 쪽(카메라 방향)을 보고, 두 손은 레이저 시작점에 모은다
                caster.rotation = Quaternion.LookRotation(forward);
                pose.Aim(start, direction);

                transform.position = start;
                transform.rotation = Quaternion.LookRotation(direction);
                transform.localScale = new Vector3(1f, 1f, FindWallDistance(start, direction));

                // 매 프레임 조금씩 쌓고 카메라가 ShakeFadeTime에 걸쳐 빼므로, 쏘는 동안 ShakeTrauma 근처로 유지된다
                cameraController.AddTrauma(ShakeTrauma / ShakeFadeTime * Time.deltaTime, ShakeFadeTime);

                tickTimer += Time.deltaTime;
                if (tickTimer >= tickInterval && DamageTouching())
                    tickTimer = 0f;

                yield return null;
            }

            Destroy(gameObject);
        }

        // start에서 direction으로 레이저가 벽에 막히는 거리. 막는 것이 없으면 range
        // 적은 레이저를 막지 않고 뚫린다. 플레이어 자신과 들고 있는 염동력 오브젝트도 무시한다
        private float FindWallDistance(Vector3 start, Vector3 direction)
        {
            float closestDistance = range;

            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsIgnored(hit)) continue;
                if (MonsterRegistry.TryGetMonster(hit.collider, out _)) continue;

                closestDistance = Mathf.Min(closestDistance, hit.distance);
            }

            return Mathf.Max(closestDistance, MinLength);
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

        // 트리거 안의 모든 적에게 한 틱만큼 피해를 준다. 한 명이라도 맞았으면 true
        private bool DamageTouching()
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
                DamageInfo info = new DamageInfo(damage, GetHitPoint(target, collider), collider, groggy: groggy);
                CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
            }

            return hitTargets.Count > 0;
        }

        // 시작점에서 재면 겨눈 곳과 상관없이 어깨 높이에 나오므로, 레이저 선 위에서 몸 가운데에 가장 가까운 점을 넘김
        // 길이는 localScale.z (콜라이더가 z 0~1)
        private Vector3 GetHitPoint(ICombatable target, Collider collider)
        {
            return HitPoint.OnLineNearBody(target, collider, transform.position, transform.forward, transform.localScale.z);
        }
    }
}
