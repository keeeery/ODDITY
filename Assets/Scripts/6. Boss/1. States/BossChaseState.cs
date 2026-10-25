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
            enterTime = Time.time;
            Animator.SetBool(BossAnimParams.IS_CHASING, true);
        }

        private void Update()
        {
            if (ShouldStopChase())
            {
                StateMachine.ChangeState(typeof(BossIdleState));
                return;
            }
            
            // 멈춰서 도는 대신 각도가 클수록 느리게 걸으면서 빠르게 회전
            float angle = root.SignedAngleToTarget;
            float turnRatio = Mathf.InverseLerp(slowStartAngle, slowEndAngle, Mathf.Abs(angle));

            root.RotateYaw(angle, Mathf.Lerp(rotateSpeed, maxRotateSpeed, turnRatio) * Time.deltaTime);

            AnimatorStateInfo currentState = Animator.GetCurrentAnimatorStateInfo(0);
            float chaseSpeed = currentState.shortNameHash == RUN_HASH ? runSpeed : walkSpeed;
            chaseSpeed *= Mathf.Lerp(1f, minSpeedRatio, turnRatio);

            Vector3 velocity = root.transform.forward * chaseSpeed;
            Body.linearVelocity = velocity;
        }

        protected override void OnExit()
        {
            Body.linearVelocity = Vector3.zero;
        }

        private bool ShouldStopChase()
        {
            BossMeleePattern intent = root.Intent;

            if (intent == null) return true;
            if (root.CanKeepMovingToTarget == false) return true;
            if (Time.time - enterTime >= rethinkInterval) return true;

            return intent.IsDistanceInRange;
        }
    }
}
