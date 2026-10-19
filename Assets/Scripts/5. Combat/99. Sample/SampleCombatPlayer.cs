using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

namespace TeamOdd.Combat
{
    public class SampleCombatPlayer : CombatEntity
    {
        public static SampleCombatPlayer Instance;

        [Title("References")]
        [SerializeField] private Collider hitCollider; // 샘플용임

        protected override void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            base.Awake();
            Instance = this;
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
        public void AttackMonster()
        {
            if (MonsterRegistry.TryGetMonster(hitCollider, out ICombatable target) == false)
                return;

            DamageInfo info = new DamageInfo(Stats.Atk, Vector3.zero, hitCollider);
            DamageEvent evt = new DamageEvent(this, target, info);
            
            CombatManager.Instance.AddCombatEvent(evt);
            
            transform.DOKill(complete: true);
            transform.DOPunchPosition(
                punch: Vector3.right * 0.3f,
                duration: 0.2f,
                vibrato: 1,
                elasticity: 0f
            );
        }
    }
}