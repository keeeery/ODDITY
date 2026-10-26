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
        [SerializeField] private BossPsychokinesisDropPattern roarDropPattern;
        [SerializeField] private BossPsychokinesisThrowPattern throwPattern;

        protected override void OnEnter()
        {
            // 내부쿨 패턴은 발동 조건(쿨 포함)을 만족하면 우선 사용
            if (roarDropPattern != null && roarDropPattern.CanUse)
            {
                StateMachine.ChangeState(roarDropPattern.GetType());
                return;
            }

            // TODO: 거리 조건 원인 확인용 임시 로그, 확인 후 제거
     //       Debug.Log($"[Idle] 투척 판정 거리 {root.DistanceToTarget:F2}m, 투척 CanUse {throwPattern.CanUse}, 남은 쿨 {throwPattern.RemainingCooldown:F1}s", this);

            if (throwPattern != null && throwPattern.CanUse)
            {
                StateMachine.ChangeState(throwPattern.GetType());
                return;
            }

            // TODO: 일단 간단하게 구현, 점점 복잡해지겠지...? 야발
            // 근접 공격 패턴이 없으면 공격하지 않고 추격
            if (root.IsTargetInSight() && attackPatterns.Length > 0)
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