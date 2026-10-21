using System;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

namespace TeamOdd.Combat
{
    public class SampleCombatMonster : CombatEntity
    {
        private void OnEnable()
        {
            MonsterRegistry.RegisterMonster(Collider, this);
        }

        private void OnDisable()
        {
            MonsterRegistry.UnregisterMonster(Collider);
        }

        public override int TakeDamage(DamageEvent @event)
        {
            transform.DOKill(complete: true);
            transform.DOPunchScale(
                punch: Vector3.one * 0.2f,
                duration: 0.2f,
                vibrato: 1,
                elasticity: 0f
            );
            
            return base.TakeDamage(@event);
        }

        [PropertySpace, Button(ButtonSizes.Large)]
        public void AttackPlayer()
        {
            SampleCombatPlayer player = SampleCombatPlayer.Instance;
            
            DamageInfo info = new DamageInfo(Stats.Atk, Vector3.zero, player.Collider);
            DamageEvent evt = new DamageEvent(this, player, info);
            
            CombatManager.Instance.AddCombatEvent(evt);
            
            transform.DOKill(complete: true);
            transform.DOPunchPosition(
                punch: Vector3.left * 0.3f,
                duration: 0.2f,
                vibrato: 1,
                elasticity: 0f
            );
        }
    }
}