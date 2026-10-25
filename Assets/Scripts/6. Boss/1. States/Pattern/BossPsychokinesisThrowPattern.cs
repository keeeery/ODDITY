using System.Collections;
using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Arena;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 투척 시작 시점(추후 보스 손 같은 지점)에 큰 염동력 오브젝트를 띄웠다가 플레이어 방향으로 던지는 패턴
    // 내부쿨이 돌아오고 플레이어가 minUseDistance보다 멀리 있을 때만 사용
    // 조준하는 동안 보스에서 대상 방향으로 날아갈 경로를 바닥에 예고하고, 던지는 순간 방향을 고정해 비행 시간 동안 채움
    // 경로 예고는 길어서 지나가는 시점이 위치마다 다르므로, 피격 판정은 날아가는 오브젝트가 대상과 겹치는지로 처리
    public sealed class BossPsychokinesisThrowPattern : BossMeleePattern
    {
        [Title("References")]
        [Tooltip("비어 있으면 씬에서 찾는다.")]
        [SerializeField] private WallFragmentSpawner fragmentSpawner;
        [Tooltip("오브젝트를 띄우고 던질 위치, 비어 있으면 이 오브젝트의 Transform을 사용한다.")]
        [SerializeField] private Transform throwOrigin;

        [Title("Timing")]
        // 오브젝트 등장: OnThrowPrepare 신호, 투척: OnThrow 신호(둘 다 GetHitHeavy 애니메이션), 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)
        // 경로 예고: OnThrowWarn 신호, 없으면 OnThrowPrepare 신호에서 시작
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

        [Title("Hit")]
        [SerializeField, Min(0)] private int damage = 20;
        [Tooltip("피격 대상에게 줄 반응, 실제 경직/다운 처리는 피격 대상 쪽에서 담당")]
        [SerializeField] private HitReaction reaction = HitReaction.Stagger;
        [Tooltip("오브젝트 박스 바깥으로 더할 판정 여유(m), 대상 몸에 부딪혀 튕겨도 겹침이 잡히도록 한다.")]
        [SerializeField, Min(0f)] private float hitPadding = 0.3f;
        [Tooltip("이 속도(m/s)보다 느려지면 판정을 끝낸다. 떨어져 구르는 오브젝트에는 맞지 않는다.")]
        [SerializeField, Min(0f)] private float minHitSpeed = 5f;
        [Tooltip("던진 뒤 판정을 유지하는 최대 시간(초)")]
        [SerializeField, Min(0f)] private float maxHitDuration = 2f;

        [Title("Telegraph")]
        [Tooltip("경로 예고의 폭(m), 던지는 오브젝트 크기에 맞춘다.")]
        [SerializeField, Min(0f)] private float pathWidth = 1.5f;
        [Tooltip("대상 너머로 더 늘일 경로 길이(m)")]
        [SerializeField, Min(0f)] private float pathOvershoot = 2f;
        [Tooltip("조준하는 동안 경로 예고가 다 차오르는 데 걸리는 시간(초), 예고 신호부터 OnThrow까지의 시간에 맞춘다.")]
        [SerializeField, Min(0f)] private float aimWarnDuration = 0.5f;
        [Tooltip("경로 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float telegraphFadeDuration = 0.3f;

        private Rigidbody heldBody;
        private Collider[] ownerColliders;
        private Collider[] ignoredColliders;
        private AttackTelegraph pathTelegraph;
        private readonly Collider[] hitOverlaps = new Collider[16];

        private Transform Origin => throwOrigin != null ? throwOrigin : transform;

        protected override bool UsesMeleeHits => false;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            ownerColliders = root.GetComponentsInChildren<Collider>();

            // 보스 프리팹은 씬 오브젝트를 참조할 수 없으므로 비어 있으면 씬에서 찾음
            if (fragmentSpawner == null) fragmentSpawner = FindAnyObjectByType<WallFragmentSpawner>();
        }

        protected override void OnPatternExit()
        {
            // 띄운 채로 패턴이 끊기면 그 자리에 떨어뜨림
            if (heldBody != null) heldBody.isKinematic = false;
            heldBody = null;
            RestoreOwnerCollisions();

            if (pathTelegraph != null) pathTelegraph.Cancel();
            pathTelegraph = null;
        }

        // 던지기 전까지 경로 예고가 대상을 따라 조준
        protected override void Update()
        {
            base.Update();
            if (pathTelegraph != null) pathTelegraph.SetPlacement(GetPathShape(), GetPathPose());
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (name == "OnThrowWarn")
            {
                ShowPathTelegraph();
                return;
            }

            // 오브젝트가 보스 몸과 겹쳐 보이지 않도록 던지기 직전의 OnThrowPrepare 신호에 등장시킴
            if (name == "OnThrowPrepare")
            {
                ShowPathTelegraph();
                Spawn();
                StartCoroutine(Hold());
                return;
            }

            if (name != "OnThrow") return;

            float flightTime = Throw();
            // 패턴이 끝나 이 오브젝트가 비활성화돼도 비행 중 판정은 이어지도록 보스에서 실행
            root.StartCoroutine(HitWhileFlying(heldBody));
            heldBody = null;
            StartCoroutine(RestoreOwnerCollisionsAfterDelay());
            LockPathTelegraph(flightTime);
        }

        // 이미 예고 중이면 그대로 둠
        private void ShowPathTelegraph()
        {
            if (pathTelegraph != null) return;
            pathTelegraph = root.ShowTelegraph(GetPathShape(), GetPathPose(), aimWarnDuration);
        }

        // 던진 방향으로 고정하고 비행 시간 동안 채운 뒤 스스로 사라지게 맡김
        private void LockPathTelegraph(float flightTime)
        {
            if (pathTelegraph == null) return;

            pathTelegraph.SetPlacement(GetPathShape(), GetPathPose());
            pathTelegraph.ResolveWhenFilled(flightTime, telegraphFadeDuration);
            pathTelegraph = null;
        }

        private TelegraphShape GetPathShape()
        {
            Vector3 offset = Target.position - Origin.position;
            offset.y = 0f;
            return TelegraphShape.Rectangle(pathWidth, offset.magnitude + pathOvershoot);
        }

        // 투척 지점 아래 보스 바닥 높이에서 대상 방향으로 시작
        private Pose GetPathPose()
        {
            Vector3 start = Origin.position;
            start.y = root.transform.position.y;

            Vector3 direction = Target.position - start;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : root.transform.rotation;
            return new Pose(start, rotation);
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

        // 물리 스텝마다 오브젝트 박스와 대상이 겹치는지 검사해 처음 겹칠 때 한 번만 피해
        // 대상에 부딪히면 속도가 줄어드므로 스텝 직전 속도로 판정 유지 여부를 정함
        // 파괴되거나, 플레이어가 붙잡거나, 충격파로 떠올라(kinematic) 다른 상태가 되면 판정을 끝냄
        private IEnumerator HitWhileFlying(Rigidbody body)
        {
            BoxCollider box = body.GetComponent<BoxCollider>();
            body.TryGetComponent(out PsychokinesisObject psychokinesisObject);
            float endTime = Time.time + maxHitDuration;

            while (Time.time < endTime)
            {
                float speed = body.linearVelocity.magnitude;
                yield return new WaitForFixedUpdate();

                if (body.gameObject.activeInHierarchy == false || body.isKinematic) yield break;
                if (psychokinesisObject != null && psychokinesisObject.IsHeld) yield break;
                if (speed < minHitSpeed) yield break;

                if (TryFindTargetOverlap(box, out Collider targetCollider) == false) continue;

                SendDamage(damage, reaction, targetCollider.ClosestPoint(body.worldCenterOfMass));
                yield break;
            }
        }

        private bool TryFindTargetOverlap(BoxCollider box, out Collider targetCollider)
        {
            Transform boxTransform = box.transform;
            Vector3 scale = boxTransform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)))
                                  + Vector3.one * hitPadding;
            int count = Physics.OverlapBoxNonAlloc(boxTransform.TransformPoint(box.center), halfExtents, hitOverlaps,
                boxTransform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int index = 0; index < count; index++)
            {
                if (hitOverlaps[index].transform.IsChildOf(Target) == false) continue;

                targetCollider = hitOverlaps[index];
                return true;
            }

            targetCollider = null;
            return false;
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

        // 조준 지점까지의 예상 비행 시간(초)을 반환
        private float Throw()
        {
            heldBody.isKinematic = false;
            Vector3 aimPoint = Target.position + Vector3.up * aimHeight;
            heldBody.linearVelocity = GetThrowVelocity(heldBody.worldCenterOfMass, aimPoint);
            heldBody.angularVelocity = Random.onUnitSphere * throwSpinSpeed;
            return Vector3.Distance(heldBody.worldCenterOfMass, aimPoint) / throwSpeed;
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
        }
#endif
    }
}
