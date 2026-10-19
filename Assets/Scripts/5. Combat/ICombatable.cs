using UnityEngine;

namespace TeamOdd.Combat
{
    public interface ICombatable
    {
        Collider Collider { get; }
        GameObject GameObject { get; }

        public int TakeDamage(DamageEvent @event);
        public int TakeHeal(HealEvent @event);
    }
}