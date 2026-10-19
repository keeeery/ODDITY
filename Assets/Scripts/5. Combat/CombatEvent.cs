using UnityEngine;

namespace TeamOdd.Combat
{
    public abstract class CombatEvent
    {
        public enum EventType
        {
            Damage,
            Heal
        }
        
        public ICombatable Sender { get; }
        public ICombatable Receiver { get; }
        
        public abstract EventType Type { get; }

        protected CombatEvent(ICombatable sender, ICombatable receiver)
        {
            Sender = sender;
            Receiver = receiver;
        }
    }

    #region DAMAGE_EVENT
    
    public readonly struct DamageInfo
    {
        public int Damage { get; }
        public Vector3 HitPoint { get; }
        public Collider Collider { get; }
        
        public DamageInfo(int damage, Vector3 hitPoint, Collider collider)
        {
            Damage = damage;
            HitPoint = hitPoint;
            Collider = collider;
        }
    }

    public class DamageEvent : CombatEvent
    {
        public DamageInfo DamageInfo { get; }
        public override EventType Type => EventType.Damage;

        public DamageEvent(ICombatable sender, ICombatable receiver, DamageInfo damageInfo) 
            : base(sender, receiver)
        {
            DamageInfo = damageInfo;
        }
    }
    
    #endregion

    public class HealEvent : CombatEvent
    {
        public int HealAmount { get; }
        public override EventType Type => EventType.Heal;
        
        public HealEvent(ICombatable sender, ICombatable receiver, int healAmount) : base(sender, receiver)
        {
            HealAmount = healAmount;
        }
    }
}