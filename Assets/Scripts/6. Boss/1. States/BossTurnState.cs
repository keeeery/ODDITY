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
            if (IsApproaching() == false) return false;

            return Mathf.Abs(root.SignedAngleToSteer) <= chaseReadyAngle;
        }

        // 추적으로 이어질 땐 장애물을 돌아갈 방향으로, 그 외엔 대상(사거리 안이면 공격 각도) 쪽으로 돎
        private float GetDeltaAngle()
        {
            if (IsApproaching()) return root.SignedAngleToSteer;

            float angle = root.SignedAngleToTarget;
            return Mathf.DeltaAngle(GetGoalAngle(angle), angle);
        }

        private float GetGoalAngle(float angle)
        {
            BossMeleePattern intent = root.Intent;

            return intent == null ? 0f : intent.ClampAngle(angle, angleMargin);
        }

        // 사거리 밖인 공격을 하러 가거나, 고를 패턴이 없어 가려진 대상을 찾으러 가는 중
        private bool IsApproaching()
        {
            BossMeleePattern intent = root.Intent;

            if (intent == null) return root.HasLineOfSight == false;

            return intent.IsDistanceInRange == false;
        }
    }
}