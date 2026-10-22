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
        [SerializeField] private BossLeftClawPattern tempPattern;
        [SerializeField] private BossPsychokinesisDropPattern roarDropPattern;

        protected override async void OnEnter()
        {
            await UniTask.WaitForSeconds(0.5f);

            // 내부쿨 패턴은 발동 조건(쿨 포함)을 만족하면 우선 사용
            if (roarDropPattern.CanUse)
            {
                StateMachine.ChangeState(roarDropPattern.GetType());
                return;
            }

            StateMachine.ChangeState(tempPattern.GetType());
        }
        
        protected override void OnExit()
        {
            
        }
    }
}