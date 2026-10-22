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
        
        public CharacterController characterController;
        public CameraController cameraController;
        
        private PlayerController playerController;
        private AnimStateEventListener eventListener;
        
        private void Awake()
        {
            DontDestroyOnLoad(this);
            
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<PlayerController>();
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

            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            playerController.enabled = cursorLocked;
            cameraController.enabled = cursorLocked;
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
        
        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = locked == false;
        }
    }
}