using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    // Idle State에서 할 것?
    // 기본적으로 플레이어를 추격함
    
    public class BossIdleState : BossState
    {
        // 일단 임시용임, 나중에 바꿔야할거같음
        [Title("Temp Settings")]
        [SerializeField] private Transform tempTarget;
        
        private void Update()
        {
            
        }

        protected override void OnEnter()
        {

        }
        
        protected override void OnExit()
        {

        }
    }
}