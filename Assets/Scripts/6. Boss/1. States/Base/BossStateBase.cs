using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;
using TeamOdd.TimeControl;

namespace TeamOdd.Boss
{
    public abstract class BossStateBase : MonoBehaviourState
    {
        protected BossMonster root;
        
        protected Rigidbody Body => root.Body;
        protected BossMovement Movement => root.Movement;
        protected Animator Animator => root.Animator;
        
        protected Transform Target => root.Target;

        // 시간 정지 중에는 흐르지 않는 보스의 시계, 타이머와 회전·이동량은 Time 대신 이 값으로 잰다
        protected TimeFreezable Clock => root.Clock;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            root = stateMachine.GetComponent<BossMonster>();
        }

        // 시간 정지 중에는 판단·이동·회전을 모두 멈춤, 상태별 매 프레임 동작은 Update 대신 OnUpdate에 작성
        private void Update()
        {
            if (Clock.IsFrozen) return;
            OnUpdate();
        }

        protected virtual void OnUpdate() { }
    }
}