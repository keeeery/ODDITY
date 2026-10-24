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
        [SerializeField] private float timeout = 2f;
        [SerializeField] private float directionDampTime = 0.15f;

        private float enterTime;

        protected override void OnEnter()
        {
            enterTime = Time.time;
            Animator.SetBool(BossAnimParams.IS_TURNING, true);

            // 진입 첫 프레임에 이전 회전 방향이 섞이지 않도록 댐핑 없이 바로 세팅
            Animator.SetFloat(BossAnimParams.TURN_DIRECTION, Mathf.Sign(GetDeltaAngle()));
        }

        private void Update()
        {
            float delta = GetDeltaAngle();

            if (Mathf.Abs(delta) <= doneAngle || Time.time - enterTime >= timeout)
            {
                StateMachine.ChangeState(typeof(BossIdleState));
                return;
            }

            root.RotateYaw(delta, turnSpeed * Time.deltaTime);
            Animator.SetFloat(BossAnimParams.TURN_DIRECTION, Mathf.Sign(delta), directionDampTime, Time.deltaTime);
        }

        protected override void OnExit()
        {
            Animator.SetBool(BossAnimParams.IS_TURNING, false);
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