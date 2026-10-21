using UnityEngine;

namespace FantasyGarden.FSM
{
    public abstract class MonoBehaviourState : MonoBehaviour
    {
        public MonoBehaviourFSM StateMachine { get; private set; }

        public virtual void Initialize(MonoBehaviourFSM stateMachine)
        {
            StateMachine = stateMachine;
        }
        
        private void OnEnable() => OnEnter();
        private void OnDisable() => OnExit();

        protected abstract void OnEnter();
        protected abstract void OnExit();
        
        public void SetActive(bool active) => gameObject.SetActive(active);
    }
}