using System;
using Study3D.FpsGame;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

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
        
        private PlayerController playerController;
        private AnimStateEventListener eventListener;
        
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<PlayerController>();
            eventListener = GetComponent<AnimStateEventListener>();
            
            hp = maxHp;
            stamina = maxStamina;
        }

        private void Update()
        {
            if(stamina <= maxStamina) stamina += Time.deltaTime;
        }

        private void OnEnable()
        {
            playerController.OnRollRequested += HandleRollRequested;
            eventListener.OnOccursAnimStateEvent += HandleAnimStateEvent;
        }

        private void OnDisable()
        {
            playerController.OnRollRequested -= HandleRollRequested;
            eventListener.OnOccursAnimStateEvent -= HandleAnimStateEvent;
        }
        
        private void HandleRollRequested()
        {
            if (stamina < 100f) return;

            ConsumeStamina(100f);
            playerController.InputRolling();
        }
        
        private void HandleAnimStateEvent(string eventName, string parameter)
        { 
            if (eventName == "EndRolling")
            {
                playerController.EndRoll();
            }
        }

        public void ConsumeStamina(float amount)
        {
            stamina = Mathf.Max(0, stamina - amount);
        }
        
        public int TakeDamage(DamageEvent @event)
        {
            throw new NotImplementedException();
        }

        public int TakeHeal(HealEvent @event)
        {
            throw new NotImplementedException();
        }
    }
}