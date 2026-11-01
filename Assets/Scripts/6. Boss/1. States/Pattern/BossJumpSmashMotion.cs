using System;
using UnityEngine;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.Arena;

namespace TeamOdd.Boss
{
    // 대상 쪽으로 뛰어올라 내려찍는 동작, BossJumpSmash와 돌진 패턴(BossChargePattern)이 함께 씀
    // 클립이 제자리 모션이라 높이는 애니메이션에 맡기고 수평 이동만 직접 처리
    // 사용하는 쪽이 준비 시작에 Warn → 애니메이션 신호에 맞춰 Begin(OnJumpStart) → Lock(OnJumpLock) → Land(OnJumpLand)를 부르고, 매 프레임 Update를 부름
    [Serializable]
    public class BossJumpSmashMotion
    {
        [Title("Jump")]
        [SerializeField] private float airDuration = 0.63f; // OnJumpStart ~ OnJumpLand 사이 시간
        [SerializeField] private float maxJumpDistance = 20f;
        [SerializeField] private float homingSpeed = 6f; // OnJumpLock 전까지 착지 지점이 대상을 따라가는 속도
        [SerializeField] private float impactOffset = 2.5f; // 보스 위치에서 충격 중심까지의 앞쪽 거리

        [Title("Impact")]
        [SerializeField] private float radius = 5f;
        [SerializeField] private int damage = 25;
        [SerializeField] private HitReaction reaction = HitReaction.Knockdown;
        [Tooltip("피격 대상을 밀어내는 거리·높이·시간과 방향, 판정 중심은 착지 충격 지점")]
        [SerializeField] private HitPush push = HitPush.Create(4f, 0.5f, 0.6f, HitPush.DirectionMode.FromHitCenter);

        [Title("Telegraph")]
        [SerializeField, Min(0f)] private float warnLeadTime = 0.2f; // Warn 호출부터 OnJumpStart까지의 대략적인 시간(초)
        [SerializeField, Min(0.01f)] private float impactFadeDuration = 0.3f; // 착지 후 충격 범위 표시가 사라지는 시간(초)

        [Title("Lift")]
        [Tooltip("착지 지점 기준으로 염동력 오브젝트를 띄울 반경(m), 피해 범위(radius)와 따로 정한다.")]
        [SerializeField, Min(0f)] private float liftRadius = 10f;
        [Tooltip("착지 충격 범위 안의 염동력 오브젝트를 띄울 높이의 무작위 범위(m)")]
        [SerializeField, MinMaxSlider(0f, 3f, true)] private Vector2 liftHeight = new Vector2(0.3f, 1f);
        [Tooltip("떠오르는 데 걸리는 시간의 무작위 범위(초), 다 떠오르면 중력으로 떨어진다.")]
        [SerializeField, MinMaxSlider(0f, 1f, true)] private Vector2 liftDuration = new Vector2(0.15f, 0.3f);

        private BossMonster root;
        private WallFragmentSpawner fragmentSpawner;
        private AttackTelegraph impactTelegraph;

        private bool isWarning;
        private bool isJumping;
        private bool isHoming;
        private float jumpStartTime;
        private Vector3 jumpStart;
        private Vector3 aimPoint; // 충격 중심이 떨어질 지점
        private Vector3 fixedJumpDirection;
        private float fixedJumpDistance;

        public TelegraphShape ImpactShape => TelegraphShape.Circle(radius);
        public int Damage => damage;
        public HitReaction Reaction => reaction;
        public HitPush Push => push;

        // 보스 프리팹은 씬 오브젝트를 참조할 수 없으므로 씬에서 찾음
        public void Initialize(BossMonster root)
        {
            this.root = root;
            fragmentSpawner = UnityEngine.Object.FindAnyObjectByType<WallFragmentSpawner>();
        }

        public void Warn() => Warn(Vector3.zero);

        // 방향을 넘기면 준비부터 착지까지 그 방향으로만 뛰고 대상을 추적하지 않는다.
        public void Warn(Vector3 direction)
        {
            Stop();

            direction.y = 0f;
            fixedJumpDirection = direction.normalized;
            jumpStart = root.Movement.Position;
            Vector3 targetPoint = new Vector3(root.Target.position.x, jumpStart.y, root.Target.position.z);
            fixedJumpDistance = Mathf.Min(Vector3.Distance(jumpStart, targetPoint), maxJumpDistance);
            aimPoint = GetInitialAimPoint();
            isWarning = true;

            impactTelegraph = root.ShowTelegraph(ImpactShape, GetWarningPose(), warnLeadTime + airDuration);
        }

        public void Begin()
        {
            jumpStart = root.Movement.Position;
            aimPoint = GetInitialAimPoint();
            jumpStartTime = root.Clock.LocalTime;

            isWarning = false;
            isJumping = true;
            isHoming = fixedJumpDirection == Vector3.zero;
            root.Movement.BeginAirborne(); // 공중에서는 구멍·벽을 넘어감
            FaceJumpDirection();

            if (impactTelegraph == null) impactTelegraph = root.ShowTelegraph(ImpactShape, GetWarningPose(), airDuration);
            else impactTelegraph.SetPlacement(ImpactShape, GetWarningPose());
        }

        public void Lock() => isHoming = false;

        public void Update()
        {
            if (isJumping == false)
            {
                if (isWarning)
                {
                    jumpStart = root.Movement.Position;
                    aimPoint = GetInitialAimPoint();
                    if (impactTelegraph != null) impactTelegraph.SetPlacement(ImpactShape, GetWarningPose());
                }

                return;
            }

            if (isHoming)
            {
                Vector3 targetPoint = new Vector3(root.Target.position.x, jumpStart.y, root.Target.position.z);
                aimPoint = ClampAim(Vector3.MoveTowards(aimPoint, targetPoint, homingSpeed * root.Clock.LocalDeltaTime));
                FaceJumpDirection();
            }

            if (impactTelegraph != null) impactTelegraph.SetPlacement(ImpactShape, GetWarningPose());

            // 남은 체공 시간 안에 착지 지점에 닿도록 수평 속도를 맞춤
            float remainTime = Mathf.Max(airDuration - (root.Clock.LocalTime - jumpStartTime), root.Clock.LocalDeltaTime);
            Vector3 velocity = (GetLandingPosition() - root.Movement.Position) / remainTime;
            root.Movement.Move(velocity * root.Clock.LocalDeltaTime);
        }

        // 착지하며 멈추고 충격 범위 안의 염동력 오브젝트를 띄운 뒤 충격 범위의 위치를 돌려줌, 피해 판정은 사용하는 쪽이 이 위치로 처리
        // 벽에 막혀 덜 갔어도 실제 착지한 위치 기준
        public Pose Land()
        {
            // 충돌 처리 전의 대기 이동량을 빼고, 실제 착지 위치와 몸 방향에 맞춘다.
            root.Movement.Teleport(root.Body.position, root.Body.rotation);
            // 구멍·벽 안에 내려앉았으면 가장 가까운 NavMesh 위로 옮긴 뒤 그 자리로 충격 판정
            root.Movement.EndAirborne();
            Vector3 center = root.Body.position + TelegraphShape.FlatForward(root.Body.rotation) * impactOffset;
            Pose pose = new Pose(center, Quaternion.identity);
            if (impactTelegraph != null)
            {
                impactTelegraph.SetPlacement(ImpactShape, pose);
                impactTelegraph.Resolve(impactFadeDuration);
                impactTelegraph = null;
            }

            Stop();

            BossFragmentLift.LiftInRange(fragmentSpawner, TelegraphShape.Circle(liftRadius), pose, liftHeight, liftDuration);
            return pose;
        }

        public void Stop()
        {
            // 공중에서 끊겨도(그로기·사망) 구멍·벽 안에 남지 않게 착지 처리
            if (isJumping) root.Movement.EndAirborne();
            isWarning = false;
            isJumping = false;
            isHoming = false;
            fixedJumpDirection = Vector3.zero;
            fixedJumpDistance = 0f;

            if (impactTelegraph == null) return;
            impactTelegraph.Cancel();
            impactTelegraph = null;
        }

        private void FaceJumpDirection()
        {
            float yaw = Quaternion.LookRotation(GetJumpDirection()).eulerAngles.y;
            root.Movement.Rotate(Mathf.DeltaAngle(root.Movement.Rotation.eulerAngles.y, yaw));
        }

        private Vector3 GetInitialAimPoint() => fixedJumpDirection != Vector3.zero
            ? jumpStart + fixedJumpDirection * fixedJumpDistance
            : ClampAim(new Vector3(root.Target.position.x, jumpStart.y, root.Target.position.z));

        // 대상이 impactOffset보다 가까우면 제자리에서 내려찍으므로 예고도 실제 예정 중심에 둔다.
        private Pose GetWarningPose() => new Pose(GetLandingPosition() + GetJumpDirection() * impactOffset, Quaternion.identity);

        // 충격 중심이 aimPoint에 오도록 impactOffset만큼 앞에서 멈춤, 대상이 너무 가까우면 제자리
        private Vector3 GetLandingPosition()
        {
            float distance = Vector3.Distance(jumpStart, aimPoint);
            return jumpStart + GetJumpDirection() * Mathf.Max(0f, distance - impactOffset);
        }

        private Vector3 GetJumpDirection()
        {
            if (fixedJumpDirection != Vector3.zero) return fixedJumpDirection;
            Vector3 direction = aimPoint - jumpStart;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : TelegraphShape.FlatForward(root.transform.rotation);
        }

        private Vector3 ClampAim(Vector3 point) => jumpStart + Vector3.ClampMagnitude(point - jumpStart, maxJumpDistance);

        #region JUMP_VISUALIZATION

#if UNITY_EDITOR
        public void DrawHandles()
        {
            if (Application.isPlaying == false || isJumping == false) return;

            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = isHoming ? Color.yellow : new Color(1f, 0.5f, 0f);
            ImpactShape.DrawHandles(GetWarningPose());
            UnityEditor.Handles.DrawLine(jumpStart, GetLandingPosition());

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
