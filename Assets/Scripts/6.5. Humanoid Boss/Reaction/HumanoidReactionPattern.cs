using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd
{
    // Idle의 패턴 목록에는 넣지 않고 HumanoidProjectileWatcher가 직접 끼워 넣음
    public abstract class HumanoidReactionPattern : BossMeleePattern, IHumanoidPhaseListener
    {
        [Title("Reaction")]
        [SerializeField] private float centerHeight = 2.5f; // 날아오는 물체와의 거리를 잴 보스 몸통 높이

        // 페이즈 1은 회피 위주, 페이즈 2는 튕겨내기·베기 위주로 가중치를 바꿈
        [Title("Phase 2")]
        [SerializeField] private float phase2Weight = 1f;

        private HumanoidBossCombatEntity combatEntity;
        private bool isPhase2;

        public float ReactionWeight => isPhase2 ? phase2Weight : Weight;

        public void EnterPhase2() => isPhase2 = true;

        protected PsychokinesisObject Projectile { get; private set; }

        // 부서졌거나, 플레이어가 다시 잡았거나, 멈춘 물체엔 더 반응하지 않음
        protected bool IsProjectileFlying => Projectile != null && Projectile.isActiveAndEnabled
                                             && Projectile.IsHeld == false && Projectile.Body.isKinematic == false;

        protected Vector3 BodyCenter => root.transform.position + Vector3.up * centerHeight;

        protected float ProjectileDistance => Vector3.Distance(Projectile.Body.worldCenterOfMass, BodyCenter);

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            combatEntity = root.GetComponent<HumanoidBossCombatEntity>();
        }

        public void React(PsychokinesisObject projectile)
        {
            Projectile = projectile;
            StateMachine.ChangeState(GetType());
        }

        // 추적·스트레이프를 끊고 들어오므로 이동 플래그를 꺼야 반응이 끝난 뒤 Idle에서 다시 출발하지 않음
        protected override void OnPatternEnter()
        {
            Animator.SetBool(BossAnimParams.IS_CHASING, false);
            Animator.SetBool(BossAnimParams.IS_STRAFING, false);
            Animator.SetBool(BossAnimParams.IS_TURNING, false);

            OnReactionEnter();
        }

        protected override void OnPatternExit()
        {
            SetGuarding(false);
            OnReactionExit();
            Projectile = null;
        }

        protected virtual void OnReactionEnter() { }
        protected virtual void OnReactionExit() { }

        protected void SetGuarding(bool isGuarding)
        {
            if (combatEntity != null) combatEntity.IsGuarding = isGuarding;
        }
    }
}
