using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Combat
{
    public class CombatEntity : MonoBehaviour, ICombatable
    {
        [field: Title("Components")]
        [field: SerializeField, Required] public Collider Collider { get; private set; }

        [field: Title("Settings")]
        [field: SerializeField, Required] public CombatStats Stats { get; private set; }

        private int hp;
        
        public bool IsDead => hp <= 0;
        public GameObject GameObject => gameObject;

        protected virtual void Awake()
        {
            hp = Stats.MaxHp;
        }

        public virtual int TakeDamage(DamageEvent @event)
        {
            if (IsDead) return 0;
            
            int dmg = Mathf.Max(1, @event.Info.Damage - Stats.Def);
            
            hp = Mathf.Max(0, hp - dmg);
            Debug.Log($"{gameObject.name} takes {dmg} damage!");
            
            if (IsDead) OnDeath();
            
            return dmg;
        }
        
        public virtual int TakeHeal(HealEvent @event)
        {
            if (IsDead) return 0;
            
            hp = Mathf.Min(Stats.MaxHp, hp + @event.HealAmount);
            Debug.Log($"{gameObject.name} heals {@event.HealAmount} hp!");

            return @event.HealAmount;
        }

        protected void OnDeath()
        {
            // 임시용
            Destroy(gameObject);
            Debug.Log($"{gameObject.name} is dead!");
        }
    }
}