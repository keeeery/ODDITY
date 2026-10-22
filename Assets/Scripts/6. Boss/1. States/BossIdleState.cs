using UnityEngine;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace TeamOdd.Boss
{
    // 시야각? 이게 필요하네
    // 만약에 
    public class BossIdleState : BossStateBase
    {
        [Title("Patterns")]
        [SerializeField] private BossStateBase[] attackPatterns;

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
    }
}