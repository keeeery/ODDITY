using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;

namespace TeamOdd.Boss
{
    public abstract class BossStateBase : MonoBehaviourState
    {
        protected BossController controller;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            controller = stateMachine.GetComponent<BossController>();
        }
    }
}