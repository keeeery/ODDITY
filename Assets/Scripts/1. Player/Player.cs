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
        
        public float Stamina => stamina;
        
        public CharacterController CharacterController;
        public CameraController CameraController;
        public PlayerController PlayerController;
        
        private AnimStateEventListener eventListener;
        
        private void Awake()
        {
            DontDestroyOnLoad(this);
            
            CharacterController = GetComponent<CharacterController>();
            PlayerController = GetComponent<PlayerController>();
            eventListener = GetComponent<AnimStateEventListener>();
            
            stamina = maxStamina;
        }

        private void Update()
        {
            if(stamina <= maxStamina) stamina += Time.deltaTime;
            
            if (SimpleInput.GetKeyDown(Key.Escape))
                LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked &&
                     (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left) ||
                      SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right)))
                LockCursor(true);

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
            if (stamina < 100f) return;

            ConsumeStamina(100f);
            PlayerController.InputRolling();
        }
        
        private void HandleAnimStateEvent(string eventName, string parameter)
        { 
            if (eventName == "EndRolling")
            {
                PlayerController.EndRoll();
            }
        }

        public void ConsumeStamina(float amount)
        {
            stamina = Mathf.Max(0, stamina - amount);
        }

        public void ControllerActive()
        {
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            
            PlayerController.enabled = cursorLocked;
            CameraController.enabled = cursorLocked;
        }
        
        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = locked == false;
        }
    }
}