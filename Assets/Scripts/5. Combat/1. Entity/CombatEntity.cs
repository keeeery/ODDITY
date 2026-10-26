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
        private int prevMaxHp; // 최대 체력이 얼마나 바뀌었는지 알기 위해 보관

        public int Hp => hp;
        public bool IsDead => hp <= 0;
        public GameObject GameObject => gameObject;

        protected virtual void Awake()
        {
            CurrentStats = Stats.CreateRuntime();
            CurrentStats.Changed += OnStatsChanged;
            hp = CurrentStats.MaxHp;
            prevMaxHp = CurrentStats.MaxHp;
        }

        // 최대 체력이 늘면 늘어난 만큼 현재 체력도 채우고, 줄면 그 안으로 맞춘다
        // (증강칩은 정비소에서만 장착/해제하므로 뺐다 끼워 체력을 채우는 것은 문제 삼지 않는다)
        private void OnStatsChanged()
        {
            int delta = CurrentStats.MaxHp - prevMaxHp;
            prevMaxHp = CurrentStats.MaxHp;

            if (delta > 0 && IsDead == false)
                hp += delta;
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