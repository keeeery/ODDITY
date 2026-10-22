using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Boss
{
    // 테스트용: 1 키로 투하 패턴, 2 키로 투척 패턴 상태로 전환
    // - 패턴이 끝나면 각 패턴이 BossIdleState로 전환
    public sealed class BossPsychokinesisPatternTester : MonoBehaviour
    {
        [SerializeField] private BossMonster boss;

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Digit1)) boss.ChangeState(typeof(BossPsychokinesisDropPattern), true);
            else if (SimpleInput.GetKeyDown(Key.Digit2)) boss.ChangeState(typeof(BossPsychokinesisThrowPattern), true);
        }
    }
}
