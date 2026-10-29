using Sirenix.OdinInspector;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    // 휴머노이드 보스의 추적 상태
    // - 걷기·조깅·달리기·전력질주 루프마다 속도를 따로 둠, 어느 단계로 달릴지는 애니메이터가 distanceToTarget으로 정함
    // - 출발처럼 발을 내딛는 전환 클립(RootMove 태그)은 루트 모션으로 움직여 발이 미끄러지지 않게 함
    // - 추적을 시작할 때 같은 동작의 A/B 클립 중 하나를 골라, 한 번의 추적 안에서는 바꾸지 않음
    public class HumanoidChaseState : BossChaseState
    {
        private static readonly int WALK_HASH = Animator.StringToHash("Walk");
        private static readonly int JOG_HASH = Animator.StringToHash("Jog");
        private static readonly int RUN_HASH = Animator.StringToHash("Run");
        private static readonly int FAST_RUN_HASH = Animator.StringToHash("FastRun");
        private static readonly int ROOT_MOVE_TAG = Animator.StringToHash("RootMove");

        // 루프 클립의 루트 이동 속도에 맞춘 값, 다르면 발이 미끄러져 보임
        [Title("Loop Speed")]
        [SerializeField] private float walkLoopSpeed = 4f;
        [SerializeField] private float jogLoopSpeed = 7.3f;
        [SerializeField] private float runLoopSpeed = 9.3f;
        [SerializeField] private float fastRunLoopSpeed = 14f;

        [Title("Variant")]
        [SerializeField, Range(0f, 1f)] private float variantBChance = 0.5f;

        protected override void OnEnter()
        {
            float variant = Random.value < variantBChance ? 1f : 0f;
            Animator.SetFloat(BossAnimParams.VARIANT, variant);

            base.OnEnter();
        }

        protected override void OnUpdate()
        {
            Animator.applyRootMotion = Animator.GetCurrentAnimatorStateInfo(0).tagHash == ROOT_MOVE_TAG;
            base.OnUpdate();
        }

        protected override void OnExit()
        {
            Animator.applyRootMotion = false;
            base.OnExit();
        }

        // 전환 클립은 루트 모션이 이동을 맡으므로 스크립트 이동을 멈춤
        protected override float GetChaseSpeed(AnimatorStateInfo currentState)
        {
            if (currentState.tagHash == ROOT_MOVE_TAG) return 0f;

            int stateHash = currentState.shortNameHash;

            if (stateHash == FAST_RUN_HASH) return fastRunLoopSpeed;
            if (stateHash == RUN_HASH) return runLoopSpeed;
            if (stateHash == JOG_HASH) return jogLoopSpeed;
            if (stateHash == WALK_HASH) return walkLoopSpeed;

            // 정지·가속 전환 중에는 직전 단계와 비슷한 속도로 이어 감
            return walkLoopSpeed;
        }
    }
}
