using System.Collections;
using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Arena;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 투척 시작 시점(추후 보스 손 같은 지점)에 큰 염동력 오브젝트를 띄웠다가 플레이어 방향으로 던지는 패턴
    // 내부쿨이 돌아오고 플레이어가 minUseDistance보다 멀리 있을 때만 사용
    public sealed class BossPsychokinesisThrowPattern : BossCooldownPatternBase
    {
        [Title("Use Distance")]
        [Tooltip("플레이어가 이 거리보다 가까우면 쓰지 않음(m)")]
        [SerializeField, Min(0f)] private float minUseDistance = 6f;

        [Title("References")]
        [SerializeField] private WallFragmentSpawner fragmentSpawner;
        [Tooltip("오브젝트를 띄우고 던질 위치, 비어 있으면 이 오브젝트의 Transform을 사용한다.")]
        [SerializeField] private Transform throwOrigin;

        [Title("Timing")]
        // 오브젝트 등장: OnThrowPrepare 신호, 투척: OnThrow 신호(둘 다 GetHitHeavy 애니메이션), 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)
        [Tooltip("투척 후 보스와의 충돌을 계속 무시하는 시간(초)")]
        [SerializeField, Min(0f)] private float ownerIgnoreDuration = 0.3f;

        [Title("Hold")]
        [Tooltip("띄워 두는 동안의 회전 속도(도/초)")]
        [SerializeField, Min(0f)] private float holdSpinSpeed = 90f;

        [Title("Throw")]
        [SerializeField, Min(0.1f)] private float throwSpeed = 25f;
        [Tooltip("대상 Transform 기준 조준 높이(m)")]
        [SerializeField] private float aimHeight = 1f;
        [Tooltip("켜면 중력으로 떨어지는 만큼 위로 보정해 조준 지점에 도달하도록 던진다.")]
        [SerializeField] private bool compensateGravity = true;
        [SerializeField, Min(0f)] private float throwSpinSpeed = 3f;

        private Rigidbody heldBody;
        private Collider[] ownerColliders;
        private Collider[] ignoredColliders;

        private Transform Origin => throwOrigin != null ? throwOrigin : transform;

        public override bool CanUse => base.CanUse
            && root.IsTargetInRange(minUseDistance) == false;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            ownerColliders = root.GetComponentsInChildren<Collider>();
        }

        protected override void OnEnter()
        {
            // TODO: 거리 조건 원인 확인용 임시 로그, 확인 후 제거
            Debug.Log($"[Throw] 진입 거리 {root.DistanceToTarget:F2}m, minUseDistance {minUseDistance}m, 예약 상태 {StateMachine.ReservedState}", this);

            StartCooldown();
            root.AnimEventListener.OnOccursAnimStateEvent += OnAnimEvent;
            root.Animator.SetTrigger(BossAnimParams.THROW);
        }

        protected override void OnExit()
        {
            root.AnimEventListener.OnOccursAnimStateEvent -= OnAnimEvent;
            root.Animator.ResetTrigger(BossAnimParams.THROW);

            // 띄운 채로 패턴이 끊기면 그 자리에 떨어뜨림
            if (heldBody != null) heldBody.isKinematic = false;
            heldBody = null;
            RestoreOwnerCollisions();
        }

        private void OnAnimEvent(string eventName, string eventParam)
        {
            // 오브젝트가 보스 몸과 겹쳐 보이지 않도록 던지기 직전의 OnThrowPrepare 신호에 등장시킴
            if (eventName == "OnThrowPrepare")
            {
                Spawn();
                StartCoroutine(Hold());
                return;
            }

            if (eventName != "OnThrow") return;

            Throw();
            heldBody = null;
            StartCoroutine(RestoreOwnerCollisionsAfterDelay());
        }

        // OnThrow 신호가 올 때까지 투척 지점에 띄워 둠
        private IEnumerator Hold()
        {
            Quaternion spin = Random.rotation;

            while (heldBody != null)
            {
                yield return new WaitForFixedUpdate();

                // 기다리는 사이 OnThrow 신호로 이미 던졌다면 종료
                if (heldBody == null) yield break;

                spin =Quaternion.AngleAxis(holdSpinSpeed * Time.fixedDeltaTime, Vector3.up) * spin;
                heldBody.MovePosition(Origin.position);
                heldBody.MoveRotation(spin);
            }
        }

        private IEnumerator RestoreOwnerCollisionsAfterDelay()
        {
            yield return new WaitForSeconds(ownerIgnoreDuration);
            RestoreOwnerCollisions();
        }

        private void Spawn()
        {
            WallFragment fragment = fragmentSpawner.SpawnFragment(FragmentSize.Large, Origin.position, Random.rotation);

            heldBody = fragment.GetComponent<Rigidbody>();
            heldBody.isKinematic = true;
            ignoredColliders = fragment.GetComponentsInChildren<Collider>();
            SetOwnerCollisionsIgnored(true);
        }

        private void Throw()
        {
            heldBody.isKinematic = false;
            Vector3 aimPoint = Target.position + Vector3.up * aimHeight;
            heldBody.linearVelocity = GetThrowVelocity(heldBody.worldCenterOfMass, aimPoint);
            heldBody.angularVelocity = Random.onUnitSphere * throwSpinSpeed;
        }

        private Vector3 GetThrowVelocity(Vector3 start, Vector3 aimPoint)
        {
            Vector3 offset = aimPoint - start;

            if (compensateGravity == false) return offset.normalized * throwSpeed;

            // 직선 거리를 throwSpeed로 이동하는 시간 동안 중력에 의한 낙하량만큼 초기 속도 보정
            float flightTime = offset.magnitude / throwSpeed;
            return offset / flightTime - 0.5f * flightTime * Physics.gravity;
        }

        private void SetOwnerCollisionsIgnored(bool ignored)
        {
            for (int index = 0; index < ignoredColliders.Length; index++)
                for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
                    Physics.IgnoreCollision(ignoredColliders[index], ownerColliders[ownerIndex], ignored);
        }

        private void RestoreOwnerCollisions()
        {
            if (ignoredColliders == null) return;

            SetOwnerCollisionsIgnored(false);
            ignoredColliders = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 에디트 모드에서는 Initialize가 호출되지 않아 Target을 알 수 없으므로 실행 중에만 표시
            if (Application.isPlaying == false) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Origin.position, 0.3f);
            Gizmos.DrawLine(Origin.position, Target.position + Vector3.up * aimHeight);

            // 사용 거리 판정이 IsTargetInRange(XZ 평면)이므로 바닥 원으로 표시
            Vector3 center = root.transform.position;
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, minUseDistance);
        }
#endif
    }
}
