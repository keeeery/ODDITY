using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Study.Utilities;

namespace TeamOdd.Boss
{
    public class BossChaseState : BossStateBase
    {
        private static readonly int RUN_HASH = Animator.StringToHash("Run");
        
        [Title("Settings")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float rotateSpeed = 120f;
        [SerializeField] private float rethinkInterval = 1f;

        [Title("Moving Turn")]
        [SerializeField] private float maxRotateSpeed = 240f;
        [SerializeField] private float slowStartAngle = 30f; // 이 각도부터 감속하며 회전을 빠르게
        [SerializeField] private float slowEndAngle = 90f; // 이 각도에서 최대 감속, 최대 회전 속도
        [SerializeField, Range(0f, 1f)] private float minSpeedRatio = 0.3f;

        private float enterTime;

        protected override void OnEnter()
        {
            enterTime = Clock.LocalTime;
            Animator.SetBool(BossAnimParams.IS_CHASING, true);
        }

        protected override void OnUpdate()
        {
            if (ShouldStopChase())
            {
                StateMachine.ChangeState(typeof(BossIdleState));
                return;
            }
            
            // 멈춰서 도는 대신 각도가 클수록 느리게 걸으면서 빠르게 회전
            // 장애물이 가로막으면 대상 대신 돌아갈 경로의 다음 꺾임점을 향함
            float angle = root.SignedAngleToSteer;
            float turnRatio = Mathf.InverseLerp(slowStartAngle, slowEndAngle, Mathf.Abs(angle));

            root.RotateYaw(angle, Mathf.Lerp(rotateSpeed, maxRotateSpeed, turnRatio) * Clock.LocalDeltaTime);

            float chaseSpeed = GetChaseSpeed(Animator.GetCurrentAnimatorStateInfo(0));
            chaseSpeed *= Mathf.Lerp(1f, minSpeedRatio, turnRatio);

            Vector3 velocity = root.transform.forward * chaseSpeed;
            Movement.Move(velocity * Clock.LocalDeltaTime);
        }

        protected override void OnExit() { }

        // 지금 재생 중인 이동 상태에 맞는 속도, 이동 단계가 다른 보스는 재정의
        protected virtual float GetChaseSpeed(AnimatorStateInfo currentState)
            => currentState.shortNameHash == RUN_HASH ? runSpeed : walkSpeed;

        private bool ShouldStopChase()
        {
            BossMeleePattern intent = root.Intent;

            if (root.CanKeepMovingToTarget == false) return true;
            if (Clock.LocalTime - enterTime >= rethinkInterval) return true;

            // 고를 패턴이 없어 가려진 대상을 찾아가는 중이면 보이는 순간 다시 판단
            if (intent == null) return root.HasLineOfSight;

            return intent.IsDistanceInRange;
        }
    }
}
