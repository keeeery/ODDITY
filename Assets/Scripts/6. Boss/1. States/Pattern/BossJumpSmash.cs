using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;

namespace TeamOdd.Boss
{
    public class BossJumpSmash : BossMeleePattern
    {
        [SerializeField, HideLabel] private BossJumpSmashMotion jump = new();

        protected override bool UsesMeleeHits => false;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            jump.Initialize(root);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            jump.Update();
        }

        protected override void OnPatternExit()
        {
            jump.Stop();
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (string.Equals(name, "OnJumpStart"))
            {
                jump.Begin();
                return;
            }

            if (string.Equals(name, "OnJumpLock"))
            {
                jump.Lock();
                return;
            }

            if (string.Equals(name, "OnJumpLand"))
            {
                TryHit(jump.ImpactShape, jump.Land(), jump.Damage, jump.Reaction, jump.Push);
            }
        }

        #region JUMP_VISUALIZATION

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() => jump.DrawHandles();
#endif

        #endregion
    }
}
