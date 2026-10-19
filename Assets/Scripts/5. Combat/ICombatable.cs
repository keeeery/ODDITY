using UnityEngine;

namespace TeamOdd.Combat
{
    public interface ICombatable
    {
        Collider Collider { get; }
        GameObject GameObject { get; }

        public void TakeDamage(DamageEvent @event);
        public void TakeHeal(HealEvent @event);
    }
}