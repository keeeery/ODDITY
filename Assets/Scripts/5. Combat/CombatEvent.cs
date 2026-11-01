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
        // 밀어내는 거리·높이·시간. 기본값(0)이면 밀지 않고 피격 모션만 재생한다
        public HitPush Push { get; }
        // 밀려날 수평 방향. 공격자·판정 중심을 아는 보내는 쪽이 HitPush.GetDirection으로 구해 넣는다
        public Vector3 PushDirection { get; }
        // 맞는 순간 바라볼 공격자 위치
        public Vector3 AttackerPosition { get; }

        public DamageInfo(int damage, Vector3 hitPoint, Collider collider, HitReaction reaction = HitReaction.None, int groggy = 0,
            HitPush push = default, Vector3 pushDirection = default, Vector3 attackerPosition = default)
        {
            Damage = damage;
            HitPoint = hitPoint;
            Collider = collider;
            Reaction = reaction;
            Groggy = groggy;
            Push = push;
            PushDirection = pushDirection;
            AttackerPosition = attackerPosition;
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