using Sirenix.OdinInspector;
using TeamOdd.Arena;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    // 가드로 받아낸 물체는 없애고, 같은 모양의 잡을 수 없는 투사체(HumanoidDeflectedProjectile)를 대상에게 되돌려 보냄
    public class HumanoidGuardCounter : HumanoidReactionPattern
    {
        [Title("Guard")]
        [SerializeField] private float acceptRadius = 6f;
        [SerializeField] private float maxGuardTime = 1.5f; // 이 시간 안에 물체가 안 오면 반격 없이 가드를 풂

        // 막은 물체를 보스 기준 이 위치(x 오른쪽, y 위, z 앞)에 붙잡아 두고, 반격 모션의 OnDeflectRelease 신호에 되돌려 보냄
        [Title("Deflect")]
        [SerializeField] private string projectilePoolKey = "HumanoidDeflectedProjectile";
        [SerializeField] private Vector3 holdOffset = new Vector3(0f, 2.5f, 3.5f);
        [SerializeField] private float catchSpeed = 40f; // 막힌 자리에서 붙잡을 위치까지 끌려오는 속도(m/s)
        [SerializeField] private float deflectSpeed = 30f;
        [SerializeField] private float aimHeight = 1f;

        private float guardEndTime;
        private bool isGuardDone;
        private HumanoidDeflectedProjectile heldProjectile;

        protected override void OnReactionEnter()
        {
            guardEndTime = Clock.LocalTime + maxGuardTime;
            isGuardDone = false;

            SetGuarding(true);
            Animator.SetBool(BossAnimParams.IS_GUARDING, true);
        }

        protected override void OnReactionExit()
        {
            Animator.SetBool(BossAnimParams.IS_GUARDING, false);
            Animator.ResetTrigger(BossAnimParams.GUARD_ACCEPT);

            if (heldProjectile != null) heldProjectile.Shatter();
            heldProjectile = null;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (heldProjectile != null)
            {
                // 보스 스케일(3.25)이 곱해지지 않도록 회전만 적용
                Vector3 holdPoint = root.transform.position + root.transform.rotation * holdOffset;
                heldProjectile.Hold(holdPoint, catchSpeed * Clock.LocalDeltaTime);
            }

            if (isGuardDone) return;

            if (IsProjectileFlying && ProjectileDistance <= acceptRadius)
            {
                Accept();
                return;
            }

            if (IsProjectileFlying && Clock.LocalTime < guardEndTime) return;

            // 물체가 사라졌거나 오지 않으면 Revenge_Guard_End로 풂
            EndGuard();
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (name == "OnDeflectRelease") Release();
        }

        // 받아낸 순간 붙잡고 Revenge_Guard_Accept → Attack으로 이어 감, 발사는 반격 모션의 신호에서
        private void Accept()
        {
            Catch();
            EndGuard();
            Animator.SetTrigger(BossAnimParams.GUARD_ACCEPT);
        }

        private void EndGuard()
        {
            isGuardDone = true;
            SetGuarding(false);
            Animator.SetBool(BossAnimParams.IS_GUARDING, false);
        }

        private void Catch()
        {
            HumanoidDeflectedProjectile deflected = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.GetObjectOrNull(projectilePoolKey) as HumanoidDeflectedProjectile
                : null;

            // 풀이 없으면 되돌려 보내지 못하고 그 자리에서 부숨
            if (deflected == null)
            {
                Projectile.ShatterAt(Projectile.Body.worldCenterOfMass);
                return;
            }

            deflected.Catch(Projectile, root.GetComponentsInChildren<Collider>(), Target);
            heldProjectile = deflected;

            RemoveOriginal();
        }

        // 쏘는 순간의 대상 위치로 조준
        private void Release()
        {
            if (heldProjectile == null) return;

            Vector3 start = heldProjectile.transform.position;
            Vector3 aimPoint = Target.position + Vector3.up * aimHeight;
            heldProjectile.Fire(GetDeflectVelocity(start, aimPoint));
            heldProjectile = null;
        }

        // 직선 거리를 deflectSpeed로 가는 시간 동안 중력으로 떨어지는 만큼 위로 보정 (보스 투척과 같은 방식)
        private Vector3 GetDeflectVelocity(Vector3 start, Vector3 aimPoint)
        {
            Vector3 offset = aimPoint - start;
            float flightTime = offset.magnitude / deflectSpeed;
            return offset / flightTime - 0.5f * flightTime * Physics.gravity;
        }

        // 풀 물체는 풀로, 맵 배치 물체는 끔 (파편화하면 바닥에 조각이 남아 되돌린 물체와 겹쳐 보임)
        private void RemoveOriginal()
        {
            if (Projectile.TryGetComponent(out WallFragment fragment))
            {
                fragment.ReturnToPool();
                return;
            }

            Projectile.gameObject.SetActive(false);
        }
    }
}
