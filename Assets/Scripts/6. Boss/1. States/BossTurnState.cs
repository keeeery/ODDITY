using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    public class BossTurnState : BossStateBase
    {
        [Title("Settings")]
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float angleMargin = 5f;
        [SerializeField] private float doneAngle = 1f;
        [SerializeField] private float chaseReadyAngle = 30f; // 추적으로 이어질 땐 이 각도까지만 돌고 나머지는 걸으면서 보정, moveHalfAngle 이하로 설정
        [SerializeField] private float timeout = 2f;
        [SerializeField] private float directionDampTime = 0.15f;

        private float enterTime;

        protected override void OnEnter()
        {
            enterTime = Clock.LocalTime;
            Animator.SetBool(BossAnimParams.IS_TURNING, true);

            // 진입 첫 프레임에 이전 회전 방향이 섞이지 않도록 댐핑 없이 바로 세팅
            Animator.SetFloat(BossAnimParams.TURN_DIRECTION, Mathf.Sign(GetDeltaAngle()));
        }

        protected override void OnUpdate()
        {
            float delta = GetDeltaAngle();

            if (Mathf.Abs(delta) <= doneAngle || IsChaseReady() || Clock.LocalTime - enterTime >= timeout)
            {
                StateMachine.ChangeState(typeof(BossIdleState));
                return;
            }

            root.RotateYaw(delta, turnSpeed * Clock.LocalDeltaTime);
            Animator.SetFloat(BossAnimParams.TURN_DIRECTION, Mathf.Sign(delta), directionDampTime, Clock.LocalDeltaTime);
        }

        protected override void OnExit()
        {
            Animator.SetBool(BossAnimParams.IS_TURNING, false);
        }

        private bool IsChaseReady()
        {
            BossMeleePattern intent = root.Intent;

            if (intent == null) return false;
            if (intent.IsDistanceInRange) return false;

            return Mathf.Abs(root.SignedAngleToTarget) <= chaseReadyAngle;
        }

        private float GetDeltaAngle()
        {
            float angle = root.SignedAngleToTarget;
            return Mathf.DeltaAngle(GetGoalAngle(angle), angle);
        }

        private float GetGoalAngle(float angle)
        {
            BossMeleePattern intent = root.Intent;

            return intent == null || intent.IsDistanceInRange == false ? 
                0f : intent.ClampAngle(angle, angleMargin);
        }
    }
}