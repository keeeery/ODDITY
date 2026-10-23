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

        // 기본값(Stats) + 증강칩 등 런타임 보너스. 전투 계산은 이 값을 읽는다
        public RuntimeStats CurrentStats { get; private set; }

        private int hp;

        public bool IsDead => hp <= 0;
        public GameObject GameObject => gameObject;

        protected virtual void Awake()
        {
            CurrentStats = new RuntimeStats(Stats);
            CurrentStats.Changed += ClampHp;
            hp = CurrentStats.MaxHp;
        }

        // 최대 체력이 줄면 현재 체력도 그 안으로 맞춘다
        private void ClampHp()
        {
            hp = Mathf.Min(hp, CurrentStats.MaxHp);
        }

        public virtual int TakeDamage(DamageEvent @event)
        {
            if (IsDead) return 0;
            
            int dmg = Mathf.Max(1, @event.Info.Damage - CurrentStats.Def);
            
            hp = Mathf.Max(0, hp - dmg);
            Debug.Log($"{gameObject.name} takes {dmg} damage!");
            
            if (IsDead) OnDeath();
            
            return dmg;
        }
        
        public virtual int TakeHeal(HealEvent @event)
        {
            if (IsDead) return 0;
            
            hp = Mathf.Min(CurrentStats.MaxHp, hp + @event.HealAmount);
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