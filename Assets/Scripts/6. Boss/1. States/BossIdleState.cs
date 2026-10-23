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

        protected override async void OnEnter()
        {
            await UniTask.WaitForSeconds(0.5f);
            StateMachine.ChangeState(tempPattern.GetType());
        }
        
        protected override void OnExit()
        {
            
        }
    }
}