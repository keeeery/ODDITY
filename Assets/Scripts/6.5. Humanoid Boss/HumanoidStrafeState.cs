using Sirenix.OdinInspector;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    public class HumanoidStrafeState : BossStateBase
    {
        [Title("Duration")]
        [SerializeField] private float minDuration = 1.5f;
        [SerializeField] private float maxDuration = 3f;

        // 8방향 걷기 루프 클립의 이동 속도에 맞춘 값, 다르면 발이 미끄러져 보임
        [Title("Movement")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float turnSpeed = 120f;
        [SerializeField] private float directionDampTime = 0.25f;

        // 이 거리 띠 안에서는 옆으로만 돌고, 벗어나면 대각선으로 물러나거나 다가감
        [Title("Distance")]
        [SerializeField] private float keepMinDistance = 6f;
        [SerializeField] private float keepMaxDistance = 12f;
        [SerializeField] private float giveUpDistance = 20f;

        private float endTime;
        private float side;

        protected override void OnEnter()
        {
            endTime = Clock.LocalTime + Random.Range(minDuration, maxDuration);
            side = Random.value < 0.5f ? -1f : 1f;

            Animator.SetFloat(BossAnimParams.VARIANT, Random.value < 0.5f ? 0f : 1f);
            Animator.SetFloat(BossAnimParams.MOVE_X, 0f);
            Animator.SetFloat(BossAnimParams.MOVE_Y, 0f);
            Animator.SetBool(BossAnimParams.IS_STRAFING, true);
        }

        protected override void OnUpdate()
        {
            if (Clock.LocalTime >= endTime || root.DistanceToTarget > giveUpDistance)
            {
                root.ChangeBossState(typeof(BossIdleState));
                return;
            }

            root.RotateYaw(root.SignedAngleToTarget, turnSpeed * Clock.LocalDeltaTime);

            Vector2 goal = GetMoveDirection();
            Animator.SetFloat(BossAnimParams.MOVE_X, goal.x, directionDampTime, Clock.LocalDeltaTime);
            Animator.SetFloat(BossAnimParams.MOVE_Y, goal.y, directionDampTime, Clock.LocalDeltaTime);

            // 애니메이터에 넣은 값(댐핑됨)으로 움직여 방향이 바뀔 때 다리 모션과 이동이 함께 바뀜
            Vector2 move = new Vector2(Animator.GetFloat(BossAnimParams.MOVE_X), Animator.GetFloat(BossAnimParams.MOVE_Y));
            Transform boss = root.transform;
            Vector3 velocity = (boss.right * move.x + boss.forward * move.y) * moveSpeed;
            Movement.Move(velocity * Clock.LocalDeltaTime);
        }

        protected override void OnExit()
        {
            Animator.SetBool(BossAnimParams.IS_STRAFING, false);
        }

        private Vector2 GetMoveDirection()
        {
            float distance = root.DistanceToTarget;
            float forward = 0f;

            if (distance < keepMinDistance) forward = -1f;
            else if (distance > keepMaxDistance) forward = 1f;

            return new Vector2(side, forward).normalized;
        }
    }
}
