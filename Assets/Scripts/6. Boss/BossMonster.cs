using System;
using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;
using Study.Utilities;
using Study3D.FpsGame;

namespace TeamOdd.Boss
{
    public class BossMonster : MonoBehaviourFSM
    {
        [field: Title("Components")]
        [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public Rigidbody Body { get; private set; }
        [field: SerializeField] public Transform Root { get; private set; }

        [field: Title("References")]
        [field: SerializeField] public Transform Target { get; private set; } // 임시용, 어차피 우리 게임은 플레이어 한 명이니 플레이어 싱글톤 박아서 참조하면 될거같기도 하고?
        [field: SerializeField] public AnimStateEventListener AnimEventListener { get; private set; }
        
        public float DistanceToTarget => transform.FlatDistanceTo(Target);
        
        private void Update()
        {
            Animator.SetFloat(BossAnimParams.DISTANCE_TO_TARGET, DistanceToTarget);
        }

        #region ANIMATION_EVENT

        private void OnEnable()
        {
            AnimEventListener.OnOccursAnimStateEvent += OnAnimEventEnded;
        }

        private void OnDisable()
        {
            AnimEventListener.OnOccursAnimStateEvent -= OnAnimEventEnded;
        }

        private void OnAnimEventEnded(string eventName, string eventParam)
        {
            if (eventName == "OnAttackEnded")
            {
                ChangeState(typeof(BossIdleState));
                return;
            }
        }

        #endregion
        
        #region PUBLIC_METHODS
        
        public bool IsTargetInRange(float range) => DistanceToTarget <= range;
        
        #endregion
    }
}