using Sirenix.OdinInspector;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    // 휴머노이드 보스의 무력화 상태, 전용 클립으로 쓰러짐 → 누움(루프) → 기상을 이어 재생
    // 쓰러짐에서 누움으로는 애니메이터가 클립 끝에서 넘기고, 기상은 isGroggy를 끄는 순간 시작
    // 진행 시간·흙먼지·흔들림 타이밍은 BossGroggyState가 그대로 맡음 (fallDuration은 쓰러짐 클립 길이, getUpDuration은 기상 클립 길이에 맞춤)
    public class HumanoidGroggyState : BossGroggyState, IHumanoidPhaseListener
    {
        // 페이즈 2에서만 이 확률로 일어나며 반격(StandUp_Revenge)
        [Title("Phase 2")]
        [SerializeField, Range(0f, 1f)] private float revengeStandUpChance = 0.5f;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private bool hasStartedGetUp = false;
        [ShowInInspector, ReadOnly] private bool isPhase2;

        public void EnterPhase2() => isPhase2 = true;

        protected override void OnEnter()
        {
            hasStartedGetUp = false;
            base.OnEnter();
        }

        // 쓰러짐 클립이 스스로 재생되므로 시간을 감지 않음
        protected override void ApplyFallPose(float progress) { }

        protected override void ApplyGetUpPose(float progress)
        {
            if (hasStartedGetUp) return;

            hasStartedGetUp = true;
            Animator.SetBool(BossAnimParams.USE_REVENGE_STAND_UP, isPhase2 && Random.value < revengeStandUpChance);
            Animator.SetBool(BossAnimParams.IS_GROGGY, false);
        }
    }
}
