using System;
using UnityEngine;
using Sirenix.OdinInspector;
using TeamOdd.Combat;

namespace TeamOdd.Boss
{
    // 대상 쪽으로 뛰어올라 내려찍는 동작, BossJumpSmash와 돌진 패턴(BossChargePattern)이 함께 씀
    // 클립이 제자리 모션이라 높이는 애니메이션에 맡기고 수평 이동만 직접 처리
    // 사용하는 쪽이 애니메이션 신호에 맞춰 Begin(OnJumpStart) → Lock(OnJumpLock) → Land(OnJumpLand)를 부르고, 매 프레임 Update를 부름
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

        private BossMonster root;

        private bool isJumping;
        private bool isHoming;
        private float jumpStartTime;
        private Vector3 jumpStart;
        private Vector3 aimPoint; // 충격 중심이 떨어질 지점

        public TelegraphShape ImpactShape => TelegraphShape.Circle(radius);
        public int Damage => damage;
        public HitReaction Reaction => reaction;

        public void Initialize(BossMonster root) => this.root = root;

        public void Begin()
        {
            jumpStart = root.Movement.Position;
            aimPoint = ClampAim(new Vector3(root.Target.position.x, jumpStart.y, root.Target.position.z));
            jumpStartTime = root.Clock.LocalTime;

            isJumping = true;
            isHoming = true;
        }

        public void Lock() => isHoming = false;

        public void Update()
        {
            if (isJumping == false) return;

            if (isHoming)
            {
                Vector3 targetPoint = new Vector3(root.Target.position.x, jumpStart.y, root.Target.position.z);
                aimPoint = ClampAim(Vector3.MoveTowards(aimPoint, targetPoint, homingSpeed * root.Clock.LocalDeltaTime));
            }

            // 남은 체공 시간 안에 착지 지점에 닿도록 수평 속도를 맞춤
            float remainTime = Mathf.Max(airDuration - (root.Clock.LocalTime - jumpStartTime), root.Clock.LocalDeltaTime);
            Vector3 velocity = (GetLandingPosition() - root.Movement.Position) / remainTime;
            root.Movement.Move(velocity * root.Clock.LocalDeltaTime);
        }

        // 착지하며 멈추고 충격 범위의 위치를 돌려줌, 피해 판정은 사용하는 쪽이 이 위치로 처리
        // 벽에 막혀 덜 갔어도 실제 착지한 위치 기준
        public Pose Land()
        {
            Stop();

            Vector3 center = root.Movement.Position + GetJumpDirection() * impactOffset;
            return new Pose(center, Quaternion.identity);
        }

        public void Stop()
        {
            isJumping = false;
            isHoming = false;
        }

        // 충격 중심이 aimPoint에 오도록 impactOffset만큼 앞에서 멈춤, 대상이 너무 가까우면 제자리
        private Vector3 GetLandingPosition()
        {
            float distance = Vector3.Distance(jumpStart, aimPoint);
            return jumpStart + GetJumpDirection() * Mathf.Max(0f, distance - impactOffset);
        }

        private Vector3 GetJumpDirection()
        {
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
            ImpactShape.DrawHandles(new Pose(aimPoint, Quaternion.identity));
            UnityEditor.Handles.DrawLine(jumpStart, GetLandingPosition());

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
