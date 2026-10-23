using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;

namespace TeamOdd.Boss
{
    public abstract class BossStateBase : MonoBehaviourState
    {
        protected BossMonster root;
        
        protected Rigidbody Body => root.Body;
        protected Animator Animator => root.Animator;
        
        protected Transform Target => root.Target;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            root = stateMachine.GetComponent<BossMonster>();
        }
    }
}