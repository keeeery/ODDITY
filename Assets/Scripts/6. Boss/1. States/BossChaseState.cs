using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Boss
{
    public class BossChaseState : BossStateBase
    {
        // 일단 임시용임, 나중에 바꿔야할거같음
        [Title("Temp Settings")]
        [SerializeField] private Transform tempTarget;

        [Title("Settings")]
        [SerializeField] private float chaseSpeed = 5f; // 추격 속도
        [SerializeField] private float chaseEndDistance = 10f; // 추격을 종료할 거리

        private Vector3 TargetPosition => tempTarget.position;
        
        private void Update()
        {
            // TODO: 일단 대충 구현하고 나중에 수정
            Rigidbody body = controller.Rigidbody;
            
            if (Vector3.Distance(transform.position, TargetPosition) > chaseEndDistance)
            {
                controller.transform.LookAt(tempTarget);
                
                Vector3 dir = controller.transform.forward;
                dir.y = 0;
                body.linearVelocity = dir * chaseSpeed;
            }
        }

        protected override void OnEnter()
        {
            controller.Animator.SetBool(BossAnimParams.IS_WALKING, true);
        }
        
        protected override void OnExit()
        {
            controller.Animator.SetBool(BossAnimParams.IS_WALKING, true);
        }
    }
}