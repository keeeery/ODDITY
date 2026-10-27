using Jay;
using Study.Utilities;
using Study3D.FpsGame;
using TeamOdd.CameraMove;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player
{
    public class Player : SingletonBase<Player>
    {
        [SerializeField] private float stamina = 100;
        [SerializeField] private float maxStamina = 100;
        [SerializeField, Min(0f)] private float staminaRegenRate = 15f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 1f; // 스태미나 사용 이후 회복 시작 대기시간

        public float Stamina => stamina;
        public float MaxStamina => maxStamina;

        private float lastConsumeTime = float.NegativeInfinity;

        public CharacterController CharacterController;
        public CameraController CameraController;
        public PlayerController PlayerController;
        
        private AnimStateEventListener eventListener;

        private bool isManuallyLocked = false;

        protected override void OnInitialize()
        {
            CharacterController = GetComponent<CharacterController>();
            PlayerController = GetComponent<PlayerController>();
            eventListener = GetComponent<AnimStateEventListener>();
            
            stamina = maxStamina;
        }

        private void Update()
        {
            if (Time.time >= lastConsumeTime + staminaRegenDelay && SimpleInput.GetKey(Key.LeftShift) == false)
                stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * Time.deltaTime);

            if (isManuallyLocked == false)
            {
                if (SimpleInput.GetKeyDown(Key.Escape))
                    LockCursor(false);
                else if (Cursor.lockState != CursorLockMode.Locked &&
                         (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left) || SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right)))
                    LockCursor(true);
            }
            
            ControllerActive();
        }

        private void OnEnable()
        {
            PlayerController.OnRollRequested += HandleRollRequested;
            eventListener.OnOccursAnimStateEvent += HandleAnimStateEvent;
        }

        private void OnDisable()
        {
            PlayerController.OnRollRequested -= HandleRollRequested;
            eventListener.OnOccursAnimStateEvent -= HandleAnimStateEvent;
        }
        
        private void HandleRollRequested()
        {
            // if (stamina < 100f) return;
            if (stamina < PlayerController.NeedRollStamina) return;

            ConsumeStamina(PlayerController.NeedRollStamina);
            PlayerController.InputRolling();
        }
        
        private void HandleAnimStateEvent(string eventName, string parameter)
        { 
            switch (eventName)
            {
                case "EndRolling":
                    PlayerController.EndRoll();
                    break;
                case "EndHit":
                    PlayerController.isHit = false;
                    break;    
                case "WakeUp":
                    PlayerController.CanWakeUp = true;
                    break;
            }
        }

        public void ConsumeStamina(float amount)
        {
            stamina = Mathf.Max(0, stamina - amount);
            lastConsumeTime = Time.time;
        }

        public void ControllerActive()
        {
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            
            PlayerController.enabled = cursorLocked;
            CameraController.enabled = cursorLocked;
        }
        
        public void LockCursor(bool locked, bool isManuallyLock = false)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = locked == false;

            isManuallyLocked = isManuallyLock;
        }
    }
}
