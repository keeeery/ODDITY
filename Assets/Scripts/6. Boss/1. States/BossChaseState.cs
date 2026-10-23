using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Study.Utilities;

namespace TeamOdd.Boss
{
    // 
    public class BossChaseState : BossStateBase
    {
        [Title("Settings")]
        [SerializeField] private float chaseSpeed = 5f; // 추격 속도
        [SerializeField] private float chaseEndDistance = 10f; // 추격을 종료할 거리
        [SerializeField] private float rotateSpeed = 2.5f; // 몹이 방향을 부드럽게 도는 속도? 뭐라 말해야하냐 이거

        private void Update()
        {
            // TODO: 일단 대충 구현하고 나중에 수정
            if (root.IsTargetInRange(chaseEndDistance))
            {
                StateMachine.ChangeState(typeof(BossIdleState));
                return;
            }

            root.transform.SmoothLookAtFlat(Target, 2f);
            
            Vector3 dir = root.transform.forward;
            root.Body.linearVelocity = dir * chaseSpeed;
        }

        protected override void OnEnter()
        {
            root.Animator.SetBool(BossAnimParams.IS_CHASING, true);
        }
        
        protected override void OnExit()
        {
            root.Animator.SetBool(BossAnimParams.IS_CHASING, false);
        }
    }
}