using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Jay;

namespace TeamOdd.Combat
{
    public class CombatManager : SingletonBase<CombatManager>
    {
        public event Action<DamageEvent> OnDamageEventExecuted;
        public event Action<HealEvent> OnHealEventExecuted;

        private readonly Queue<CombatEvent> eventQueue = new();
        
        private void Update()
        {
            while (true)
            {
                if (eventQueue.Count <= 0) break;
                ExecuteEvent();
            }
        }

        private void ExecuteEvent()
        {
            CombatEvent combatEvent = eventQueue.Dequeue();

            // 이벤트의 타겟의 게임 오브젝트가 존재하지 않을 경우, 넘김
            if (combatEvent.Receiver.GameObject == null) return;

            switch (combatEvent.Type)
            {
                case CombatEvent.EventType.Damage:
                    ExecuteDamageEvent();
                    break;
                case CombatEvent.EventType.Heal:
                    ExecuteHealEvent();
                    break;
                default:
                    throw new NotImplementedException($"{combatEvent.Type} event is not implemented.");
            }

            void ExecuteDamageEvent()
            {
                DamageEvent damageEvent = combatEvent as DamageEvent;
                combatEvent.Receiver.TakeDamage(damageEvent);
                OnDamageEventExecuted?.Invoke(damageEvent);
            }

            void ExecuteHealEvent()
            {
                HealEvent healEvent = combatEvent as HealEvent;
                combatEvent.Receiver.TakeHeal(healEvent);
                OnHealEventExecuted?.Invoke(healEvent);
            }
        }

        public void AddCombatEvent(CombatEvent combatEvent) => eventQueue.Enqueue(combatEvent);
    }
}
