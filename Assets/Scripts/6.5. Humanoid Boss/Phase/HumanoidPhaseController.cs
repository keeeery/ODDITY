using Sirenix.OdinInspector;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    public class HumanoidPhaseController : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private BossMonster boss;
        [SerializeField] private HumanoidBossCombatEntity combatEntity;
        [SerializeField] private HumanoidPhaseTransition transition;
        [SerializeField] private BossStateBase[] blockingStates; // 이 상태(그로기 등)가 끝날 때까지 전환을 미룸

        [Title("Phase 2")]
        [SerializeField, Range(0f, 1f)] private float phase2HpRatio = 0.5f;
        [SerializeField] private float phase2MotionSpeed = 1f; // 페이즈 2부터 BossMonster의 motionSpeed를 이 값으로

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] public int Phase { get; private set; } = 1;
        [ShowInInspector, ReadOnly] private bool isTransitionPending;

        private void OnEnable() => combatEntity.OnHpChanged += HandleHpChanged;

        private void OnDisable() => combatEntity.OnHpChanged -= HandleHpChanged;

        // 공격 중이면 끊고 바로 전환, 그로기 중이면 회복(Restart) 뒤에 전환
        private void Update()
        {
            if (isTransitionPending == false) return;

            if (combatEntity.IsDead)
            {
                isTransitionPending = false;
                return;
            }

            // 정지·감속 중에 동작 빠르기를 바꾸면 풀릴 때 기억해 둔 속도로 되돌아가므로 정상 시간까지 기다림
            if (boss.Clock.TimeScale < 1f || IsBlocked()) return;

            isTransitionPending = false;
            EnterPhase2();
        }

        private void HandleHpChanged(int hp, int maxHp)
        {
            if (Phase != 1 || isTransitionPending) return;
            if (combatEntity.HpRatio > phase2HpRatio) return;

            isTransitionPending = true;
        }

        private bool IsBlocked()
        {
            foreach (BossStateBase state in blockingStates)
                if (state.isActiveAndEnabled) return true;

            return false;
        }

        // 전환 연출보다 먼저 알려야 연출이 끝난 뒤 Idle이 페이즈 2 패턴으로 고름
        private void EnterPhase2()
        {
            Phase = 2;
            boss.SetMotionSpeed(phase2MotionSpeed);

            foreach (IHumanoidPhaseListener listener in boss.GetComponentsInChildren<IHumanoidPhaseListener>(true))
                listener.EnterPhase2();

            Debug.Log("[HumanoidPhaseController] 페이즈 2 전환", this);
            boss.ChangeBossState(transition.GetType(), true);
        }
    }
}
