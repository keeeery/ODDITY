using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Boss
{
    public class BossIdleState : BossStateBase
    {
        [Title("Settings")]
        [SerializeField] private float reserveTimeout = 4f;
        [SerializeField] private float turnAngleMargin = 5f;
        
        [Title("Patterns")]
        [SerializeField] private BossStateBase[] attackPatterns;

        private float reservedTime;
        
        protected override void OnEnter()
        {
            // TODO: 일단 간단하게 구현, 점점 복잡해지겠지...? 야발
            if (root.IsTargetInSight())
            {
                BossStateBase attack = attackPatterns[Random.Range(0, attackPatterns.Length)];
                StateMachine.ChangeState(attack.GetType());
                return;
            }

            StateMachine.ChangeState(typeof(BossChaseState));
        }
        
        protected override void OnExit()
        {
            
        }

        private bool TryGetPattern(out IBossPattern result)
        {
            // 예약 시간이 지났다면, 예약된 요소 제거
            if (Time.time - reservedTime >= reserveTimeout)
                StateMachine.CancleReservedState();

            if (StateMachine.ReservedState != null &&
                StateMachine.ReservedState is IBossPattern reserved && reserved.CanUse)
            {
                result = reserved;
                return true;
            }

            result = new BossLeftClawAttack(); // 임시용
            return false;
        }
        
        
    }
}