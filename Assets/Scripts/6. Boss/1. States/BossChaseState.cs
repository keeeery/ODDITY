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

        [Title("Interrupt")]
        [Tooltip("추격 중 CanUse가 참이 되면 추격을 멈추고 Idle로 돌아가 패턴을 고르게 할 패턴")]
        [SerializeField] private BossCooldownPatternBase[] interruptPatterns;

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

            // 추격 중에도 쿨 패턴을 쓸 수 있게 되면 Idle로 복귀, Idle이 고른 패턴이 예약된 근접 패턴에 가로채이지 않도록 예약을 비움
            foreach (BossCooldownPatternBase pattern in interruptPatterns)
            {
                if (pattern.CanUse == false) continue;

                StateMachine.ClearReservedState();
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
