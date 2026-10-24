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
            
            root.RotateYaw(root.SignedAngleToTarget, rotateSpeed * Time.deltaTime);
            
            AnimatorStateInfo currentState = Animator.GetCurrentAnimatorStateInfo(0);
            float chaseSpeed = currentState.shortNameHash == RUN_HASH ? runSpeed : walkSpeed;
            
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
            if (root.CanMoveToTarget == false) return true;
            if (Time.time - enterTime >= rethinkInterval) return true;

            return intent.IsDistanceInRange;
        }
    }
}
