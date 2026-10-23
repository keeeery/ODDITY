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
        [SerializeField] private float walkSpeed = 5f; // 추격 속도
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float chaseEndRange = 10f; // 추격을 종료할 거리
        [SerializeField] private float chaseEndMargin = 0.5f;
        [SerializeField] private float rotateSpeed = 2.5f; // 몹이 방향을 부드럽게 도는 속도? 뭐라 말해야하냐 이거

        private float finalChaseEndRange;
        
        protected override void OnEnter()
        {
            finalChaseEndRange = StateMachine.ReservedState is IBossPattern pattern ? 
                    pattern.RequiredRange - chaseEndMargin : 
                    chaseEndRange;
            
            Animator.SetBool(BossAnimParams.IS_CHASING, true);
        }
        
        private void Update()
        {
            // TODO: 일단 대충 구현하고 나중에 수정
            if (root.IsTargetInRange(finalChaseEndRange) && root.IsTargetInSight())
            {
                StateMachine.ChangeState(typeof(BossIdleState));
                return;
            }

            root.transform.SmoothLookAtFlat(Target, 2f);

            AnimatorStateInfo currState = Animator.GetCurrentAnimatorStateInfo(0);
            float speed = currState.shortNameHash == RUN_HASH ? runSpeed : walkSpeed;

            Vector3 velocity = root.transform.forward * speed;
            Body.linearVelocity = velocity;
        }
        
        protected override void OnExit()
        {
            root.Animator.SetBool(BossAnimParams.IS_CHASING, false);
        }
    }
}