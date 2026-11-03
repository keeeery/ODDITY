using Jay;
using Study.Utilities;
using Study3D.FpsGame;
using TeamOdd.CameraMove;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player
{
    public enum AttackSoundType
    {
        Normal,
        Pull,
        Hold,
        Throw
    }
    
    // 플레이어가 맞을지는 CombatEntity.TakeDamage 한 곳에서 정해지고, 그 중 플레이어 몫(회피)은 여기서 답한다 (IDamageGuard)
    // 순서: 구르기·기상 직후 무적 → SkillGuard(패시브 칩). 둘 다 지나 맞기로 정해지면 CombatEntity.Hit을 듣고 피격 반응을 건다
    public class Player : SingletonBase<Player>, IDamageGuard
    {
        [SerializeField] private float stamina = 100;
        [SerializeField, Min(0f)] private float staminaRegenRate = 15f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 1f; // 스태미나 사용 이후 회복 시작 대기시간

        public float Stamina => stamina;

        // 최대 스태미나 = 기본값(PlayerStats 에셋) + 증강칩 보너스. 칩을 끼우고 빼면 바로 바뀌므로 쓸 때마다 읽는다
        public float MaxStamina => combatEntity.CurrentStats.Get(StatType.MaxStamina);

        private float lastConsumeTime = float.NegativeInfinity;
        private CombatEntity combatEntity;
        [SerializeField] private SoundData normalSound, pullSound, holdSound, throwSound;

        // 구르기 무적 다음으로 피해 직전·죽기 직전에 물어볼 곳. 증강칩(PassiveSkillRunner)이 자기를 넣는다. 없으면 null
        public IDamageGuard SkillGuard { get; set; }

        public CharacterController CharacterController { get; private set; }
        public CameraController CameraController;
        public PlayerController PlayerController;
        
        private AnimStateEventListener eventListener;

        private bool isManuallyLocked = false;

        protected override void OnInitialize()
        {
            CharacterController = GetComponent<CharacterController>();
            PlayerController = GetComponent<PlayerController>();
            eventListener = GetComponent<AnimStateEventListener>();
            combatEntity = GetComponent<CombatEntity>();
            // 사망 연출은 PlayerController가 맡으므로 죽어도 오브젝트를 지우지 않는다
            combatEntity.DestroyOnDeath = false;
            combatEntity.Guard = this;
        }

        // CurrentStats는 CombatEntity.Awake에서 만들어지므로 처음 채우는 건 Start에서 한다
        private void Start()
        {
            stamina = MaxStamina;
        }

        private void Update()
        {
            //Debug.Log("UI Close: " + UIManager.instance.IsUIClose);
            
            // 칩을 빼서 최대치가 줄면 회복할 때 새 최대치로 맞춰진다
            if (Time.time >= lastConsumeTime + staminaRegenDelay && SimpleInput.GetKey(Key.LeftShift) == false)
                stamina = Mathf.Min(MaxStamina, stamina + staminaRegenRate * Time.deltaTime);
        }

        private void OnEnable()
        {
            PlayerController.OnRollRequested += HandleRollRequested;
            eventListener.OnOccursAnimStateEvent += HandleAnimStateEvent;
            combatEntity.Died += PlayerController.DeathAnimation;
            combatEntity.Hit += HandleHit;
        }

        private void OnDisable()
        {
            PlayerController.OnRollRequested -= HandleRollRequested;
            eventListener.OnOccursAnimStateEvent -= HandleAnimStateEvent;
            combatEntity.Died -= PlayerController.DeathAnimation;
            combatEntity.Hit -= HandleHit;
        }

        // 구르기로 피하면 그 구르기를 누가 시작했는지에 맞는 회피 피드백. 기상 직후 무적은 피한 게 아니라서 띄우지 않는다
        // 확정 피격(Unavoidable)은 구르기 무적도 패시브 칩도 묻지 않고 맞는다
        public bool BlockDamage(DamageEvent damageEvent)
        {
            if (damageEvent.Info.Unavoidable)
                return false;

            if (PlayerController.IsInvincible)
            {
                if (PlayerController.IsRolling) DodgeEffect.Play(transform, PlayerController.RollType);
                return true;
            }

            return SkillGuard != null && SkillGuard.BlockDamage(damageEvent);
        }

        public bool PreventDeath() => SkillGuard != null && SkillGuard.PreventDeath();
        
        public void PlayAttackSound(AttackSoundType type)
        {
            SoundData sound = type switch
            {
                AttackSoundType.Normal => normalSound,
                AttackSoundType.Pull   => pullSound,
                AttackSoundType.Hold   => holdSound,
                AttackSoundType.Throw  => throwSound,
                _ => null
            };
            sound?.Play();
        }

        private void HandleHit(DamageEvent damageEvent)
        {
            DamageInfo info = damageEvent.Info;
            PlayerController.ApplyHitReaction(info.Reaction, info.PushDirection, info.Push, info.AttackerPosition);
        }
        
        private void HandleRollRequested()
        {
            if (stamina < PlayerController.NeedRollStamina) return;

            ConsumeStamina(PlayerController.NeedRollStamina);
            PlayerController.InputRolling(DodgeType.Manual);
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
