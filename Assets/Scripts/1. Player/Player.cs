using Jay;
using Study.Utilities;
using Study3D.FpsGame;
using TeamOdd.CameraMove;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player
{
    public class Player : SingletonBase<Player>
    {
        [SerializeField] private float stamina = 100;
        [SerializeField, Min(0f)] private float staminaRegenRate = 15f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 1f; // 스태미나 사용 이후 회복 시작 대기시간

        public float Stamina => stamina;

        // 최대 스태미나 = 기본값(PlayerStats 에셋) + 증강칩 보너스. 칩을 끼우고 빼면 바로 바뀌므로 쓸 때마다 읽는다
        public float MaxStamina => combatEntity.CurrentStats.Get(StatType.MaxStamina);

        private float lastConsumeTime = float.NegativeInfinity;
        private CombatEntity combatEntity;

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
            combatEntity = GetComponent<CombatEntity>();
        }

        // CurrentStats는 CombatEntity.Awake에서 만들어지므로 처음 채우는 건 Start에서 한다
        private void Start()
        {
            stamina = MaxStamina;
        }

        private void Update()
        {
            // 칩을 빼서 최대치가 줄면 회복할 때 새 최대치로 맞춰진다
            if (Time.time >= lastConsumeTime + staminaRegenDelay)
                stamina = Mathf.Min(MaxStamina, stamina + staminaRegenRate * Time.deltaTime);
            
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
            lastConsumeTime = Time.time;
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
