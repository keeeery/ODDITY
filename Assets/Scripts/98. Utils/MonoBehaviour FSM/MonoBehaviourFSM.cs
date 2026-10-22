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
        
        [ShowInInspector, ReadOnly] private MonoBehaviourState currentState;
        private readonly Dictionary<Type, MonoBehaviourState> stateMap = new();

        public MonoBehaviourState ReservedState { get; private set; }

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

        public void ChangeState(Type nextState, bool ignoreReservedState = false)
        {
            // 만약 미구현된 혹은 정의했는데 깜빡하고 구현 못한게 있다면 로그를 날리고 기본 상태로 돌림
            if (stateMap.TryGetValue(nextState, out MonoBehaviourState state) == false)
            {
                Debug.LogWarning($"[BossStateMachine] State {nextState} not found!");
                state = stateMap[defaultStateType];
            }
            
            // 예약된 상태가 있고, 이를 무시하는 설정이 아니라면 예약된 상태로 강제
            if (ignoreReservedState == false && ReservedState != null)
            {
                state = ReservedState;
                ReservedState = null;
            }

            currentState?.SetActive(false);
            
            currentState = state;
            currentState.SetActive(true);
        }

        public void ReserveState(Type state) => ReservedState = stateMap[state];

        public void ReserveState(MonoBehaviourState state)
        {
            if (state == null) return;
            if (stateMap.ContainsValue(state) == false) return;
            ReservedState = state;
        }
    }
}