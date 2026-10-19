using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Player
{
    public class Player : MonoBehaviour, ICombatable
    {
        private CharacterController characterController;
        
        [SerializeField] private float hp = 100;
        [SerializeField] private float maxHp = 100;
        [SerializeField] private float stamina = 100;
        [SerializeField] private float maxStamina = 100;
        
        public Collider Collider => characterController; 
        public GameObject GameObject => gameObject;
        
        public float Hp => hp;
        public float Stamina => stamina;
        
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            
            hp = maxHp;
            stamina = maxStamina;
        }

        public void ConsumeStamina(float amount)
        {
            stamina = Mathf.Max(0, stamina - amount);
        }
        
        public void TakeDamage(DamageEvent @event)
        {
            hp = Mathf.Max(0, hp - @event.DamageInfo.Damage);

            // 필요한 경우 여기에서 피격 이펙트, 사망 처리 등을 호출합니다.
            // 피격 위치: @event.DamageInfo.HitPoint
        }

        public void TakeHeal(HealEvent @event)
        {
            hp = Mathf.Min(maxHp, hp + @event.HealAmount);
        }
    }
}