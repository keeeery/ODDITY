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
        // 피격 반응, Knockdown은 강공격으로 취급한다.
        public HitReaction Reaction { get; }
        // 무력화 피해, 받는 쪽에 무력화 수치가 있으면 그만큼 깎는다(보스 BossGroggy). 방어력은 적용하지 않음
        public int Groggy { get; }
        
        public DamageInfo(int damage, Vector3 hitPoint, Collider collider, HitReaction reaction = HitReaction.None, int groggy = 0)
        {
            Damage = damage;
            HitPoint = hitPoint;
            Collider = collider;
            Reaction = reaction;
            Groggy = groggy;
        }
    }

    public class DamageEvent : CombatEvent
    {
        public DamageInfo Info { get; }
        public override EventType Type => EventType.Damage;

        public DamageEvent(ICombatable sender, ICombatable receiver, DamageInfo info) 
            : base(sender, receiver)
        {
            Info = info;
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