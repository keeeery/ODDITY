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

        private bool isManuallyLocked = false;
        
        private void Awake()
        {
            DontDestroyOnLoad(this);
            
            CharacterController = GetComponent<CharacterController>();
            PlayerController = GetComponent<PlayerController>();
            eventListener = GetComponent<AnimStateEventListener>();
            combatEntity = GetComponent<CombatEntity>();
            // 사망 연출은 PlayerController가 맡으므로 죽어도 오브젝트를 지우지 않는다
            combatEntity.DestroyOnDeath = false;
        }

        // CurrentStats는 CombatEntity.Awake에서 만들어지므로 처음 채우는 건 Start에서 한다
        private void Start()
        {
            stamina = MaxStamina;
        }

        private void Update()
        {
            Debug.Log("UI Close: " + UIManager.instance.IsUIClose);
            
            // 칩을 빼서 최대치가 줄면 회복할 때 새 최대치로 맞춰진다
            if (Time.time >= lastConsumeTime + staminaRegenDelay && SimpleInput.GetKey(Key.LeftShift) == false)
                stamina = Mathf.Min(MaxStamina, stamina + staminaRegenRate * Time.deltaTime);
        }

        private void OnEnable()
        {
            PlayerController.OnRollRequested += HandleRollRequested;
            eventListener.OnOccursAnimStateEvent += HandleAnimStateEvent;
            combatEntity.Died += PlayerController.DeathAnimation;
        }

        private void OnDisable()
        {
            PlayerController.OnRollRequested -= HandleRollRequested;
            eventListener.OnOccursAnimStateEvent -= HandleAnimStateEvent;
            combatEntity.Died -= PlayerController.DeathAnimation;
        }
        
        private void HandleRollRequested()
        {
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
                    PlayerController.EndHitAnimation();
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

        // 스태미나를 그 값으로 바로 맞춘다 (시간 역행으로 기록해 둔 스태미나로 되돌릴 때)
        public void SetStamina(float value)
        {
            stamina = Mathf.Clamp(value, 0f, MaxStamina);
        }
    }
}
