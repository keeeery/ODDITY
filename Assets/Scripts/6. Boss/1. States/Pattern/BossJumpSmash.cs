using UnityEngine;
using Sirenix.OdinInspector;
using TeamOdd.Combat;

namespace TeamOdd.Boss
{
    public class BossJumpSmash : BossMeleePattern
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

        private bool isJumping;
        private bool isHoming;
        private float jumpStartTime;
        private Vector3 jumpStart;
        private Vector3 aimPoint; // 충격 중심이 떨어질 지점

        protected override bool UsesMeleeHits => false;

        // 클립이 제자리 모션이라 높이는 애니메이션에 맡기고 수평 이동만 직접 처리
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (isJumping == false) return;

            if (isHoming)
            {
                Vector3 targetPoint = new Vector3(Target.position.x, jumpStart.y, Target.position.z);
                aimPoint = ClampAim(Vector3.MoveTowards(aimPoint, targetPoint, homingSpeed * Clock.LocalDeltaTime));
            }

            // 남은 체공 시간 안에 착지 지점에 닿도록 수평 속도를 맞춤
            float remainTime = Mathf.Max(airDuration - (Clock.LocalTime - jumpStartTime), Clock.LocalDeltaTime);
            Vector3 velocity = (GetLandingPosition() - Body.position) / remainTime;
            Body.linearVelocity = new Vector3(velocity.x, Body.linearVelocity.y, velocity.z);
        }

        protected override void OnPatternExit()
        {
            StopJump();
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (string.Equals(name, "OnJumpStart"))
            {
                StartJump();
                return;
            }

            if (string.Equals(name, "OnJumpLock"))
            {
                isHoming = false;
                return;
            }

            if (string.Equals(name, "OnJumpLand"))
            {
                Land();
            }
        }

        private void StartJump()
        {
            jumpStart = Body.position;
            aimPoint = ClampAim(new Vector3(Target.position.x, jumpStart.y, Target.position.z));
            jumpStartTime = Clock.LocalTime;

            isJumping = true;
            isHoming = true;
        }

        private void Land()
        {
            StopJump();

            // 벽에 막혀 덜 갔어도 실제 착지한 위치 기준으로 판정
            Vector3 center = Body.position + GetJumpDirection() * impactOffset;
            TryHit(TelegraphShape.Circle(radius), new Pose(center, Quaternion.identity), damage, reaction);
        }

        private void StopJump()
        {
            if (isJumping) Body.linearVelocity = new Vector3(0f, Body.linearVelocity.y, 0f);

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
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false || isJumping == false) return;

            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = isHoming ? Color.yellow : new Color(1f, 0.5f, 0f);
            TelegraphShape.Circle(radius).DrawHandles(new Pose(aimPoint, Quaternion.identity));
            UnityEditor.Handles.DrawLine(jumpStart, GetLandingPosition());

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
