using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Study.Utilities;

namespace TeamOdd.Boss
{
    public class BossChaseState : BossStateBase
    {
        [Title("Settings")]
        [SerializeField] private float chaseSpeed = 5f; // 추격 속도
        [SerializeField] private float chaseEndRange = 10f; // 추격을 종료할 거리
        [SerializeField] private float chaseEndMargin = 0.5f;
        [SerializeField] private float rotateSpeed = 2.5f; // 몹이 방향을 부드럽게 도는 속도? 뭐라 말해야하냐 이거

        private float finalChaseEndRange;
        
        protected override void OnEnter()
        {
            finalChaseEndRange = StateMachine.ReservedState is IRangedPattern pattern ? 
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

            Vector3 velocity = root.transform.forward * chaseSpeed;
            Body.linearVelocity = velocity;
        }
        
        protected override void OnExit()
        {
            root.Animator.SetBool(BossAnimParams.IS_CHASING, false);
        }
    }
}