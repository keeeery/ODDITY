using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace FantasyGarden.FSM
{
    public class MonoBehaviourFSM : MonoBehaviour
    {
        [Title("State Machine")]
        [SerializeField] private MonoBehaviourState defaultState;

        private Type defaultStateType;
        
        private MonoBehaviourState currentState;
        private readonly Dictionary<Type, MonoBehaviourState> stateMap = new();
        
        #if UNITY_EDITOR_
        [ShowInInspector, ReadOnly] private string CurrentState => currentState == null ? string.Empty : currentState.GetType().Name;
        #endif

        protected virtual void Awake()
        {
            defaultStateType = defaultState.GetType();
            
            MonoBehaviourState[] states = GetComponentsInChildren<MonoBehaviourState>(true);

            foreach (MonoBehaviourState state in states)
            {
                stateMap.Add(state.GetType(), state);

                state.Initialize(this);
                state.SetActive(false);
            }
        }

        protected void Start()
        {
            ChangeState(defaultStateType);
        }

        public void ChangeState(Type nextState)
        {
            // 만약 미구현된 혹은 정의했는데 깜빡하고 구현 못한게 있다면 로그를 날리고 기본 상태로 돌림
            if (stateMap.TryGetValue(nextState, out MonoBehaviourState state) == false)
            {
                Debug.LogWarning($"[BossStateMachine] State {nextState} not found!");
                state = stateMap[defaultStateType];
            }

            currentState?.SetActive(false);
            
            currentState = state;
            currentState.SetActive(true);
        }
    }
}