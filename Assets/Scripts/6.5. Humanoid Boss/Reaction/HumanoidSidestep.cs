using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    public class HumanoidSidestep : HumanoidReactionPattern
    {
        protected override bool UsesMeleeHits => false;

        // 인스펙터의 트리거(dodgeLeft)는 베이스가 먼저 켜므로 지우고 피할 쪽으로 다시 켬
        protected override void OnReactionEnter()
        {
            Animator.ResetTrigger(BossAnimParams.DODGE_LEFT);
            Animator.ResetTrigger(BossAnimParams.DODGE_RIGHT);
            Animator.SetTrigger(ShouldDodgeRight() ? BossAnimParams.DODGE_RIGHT : BossAnimParams.DODGE_LEFT);
        }

        protected override void OnReactionExit()
        {
            Animator.ResetTrigger(BossAnimParams.DODGE_LEFT);
            Animator.ResetTrigger(BossAnimParams.DODGE_RIGHT);
        }

        // 물체 진행선에서 보스 쪽으로 멀어지는 방향, 정면으로 날아오면 진행 방향의 오른쪽
        private bool ShouldDodgeRight()
        {
            if (IsProjectileFlying == false) return Random.value < 0.5f;

            Vector3 velocity = Projectile.Body.linearVelocity;
            velocity.y = 0f;

            Vector3 offset = root.transform.position - Projectile.Body.worldCenterOfMass;
            offset.y = 0f;

            Vector3 away = offset - Vector3.Project(offset, velocity);
            if (away.sqrMagnitude < 0.01f) away = Vector3.Cross(Vector3.up, velocity);

            return Vector3.Dot(away, root.transform.right) >= 0f;
        }
    }
}
