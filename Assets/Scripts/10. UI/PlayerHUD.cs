using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.UI
{
    // 플레이어 체력(CombatEntity)과 스태미나(Player)를 VectorBar로 보여주는 간단한 HUD
    // 피격 시 비네팅을 띄우고, 강공격이면 체력바도 반응한다.
    public class PlayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Player.Player player;
        [SerializeField] private CombatEntity combatEntity;

        [Header("Bars")]
        [SerializeField] private VectorBar hpBar;
        [SerializeField] private VectorBar staminaBar;

        [Header("Hit Feedback")]
        [SerializeField] private DamageVignette damageVignette;

        [Header("Camera Shake")]
        // 강공격 시 카메라 trauma(0~1)와 감쇠 시간(초). 세기는 trauma²로 적용된다.
        [SerializeField, Range(0f, 1f)] private float cameraShakeTrauma = 0.6f;
        [SerializeField, Min(0.01f)] private float cameraShakeDuration = 0.35f;

        [Header("Hit Stop")]
        // 피격 시 월드 전체가 멈추는 시간(초). 일반 피격은 거의 느껴지지 않을 정도로 짧게.
        [SerializeField, Min(0f)] private float hitStopNormal = 0.04f;
        [SerializeField, Min(0f)] private float hitStopHeavy = 0.1f;

        private CombatManager combatManager;

        private void Awake()
        {
            player ??= FindAnyObjectByType<Player.Player>();
            combatEntity ??= player.GetComponent<CombatEntity>();
        }

        private void Start()
        {
            // 보스 정보 UI처럼 바를 하나만 쓰는 경우가 있어 비어 있는 바는 건너뜀
            if (hpBar != null) hpBar.SetFillImmediate(GetHpRatio());
            if (staminaBar != null) staminaBar.SetFillImmediate(GetStaminaRatio());

            combatManager = CombatManager.Instance;
            combatManager.OnDamageEventExecuted += HandleDamageExecuted;
        }

        private void OnDestroy()
        {
            if (combatManager != null) combatManager.OnDamageEventExecuted -= HandleDamageExecuted;
        }

        private void Update()
        {
            UpdateFill(hpBar, GetHpRatio());
            UpdateFill(staminaBar, GetStaminaRatio());
            if (damageVignette != null) damageVignette.HealthRatio = GetHpRatio();
        }

        private void HandleDamageExecuted(DamageEvent damageEvent, int receivedDamage)
        {
            if (receivedDamage <= 0 || ReferenceEquals(damageEvent.Receiver, combatEntity) == false) return;

            // 강공격(Knockdown)만 체력바가 반응하고 비네팅도 더 진해진다.
            bool isHeavy = damageEvent.Info.Reaction == HitReaction.Knockdown;
            if (isHeavy && hpBar != null) hpBar.Flash();
            if (isHeavy && player != null && player.CameraController != null)
            {
                player.CameraController.AddTrauma(cameraShakeTrauma, cameraShakeDuration);
            }
            HitStop.Request(isHeavy ? hitStopHeavy : hitStopNormal);
            if (damageVignette != null) damageVignette.Pulse(isHeavy);
        }

        private void UpdateFill(VectorBar bar, float ratio)
        {
            if (bar == null) return;

            bar.FillAmount = ratio;
        }

        private float GetHpRatio()
        {
            if (combatEntity == null) return 0f;

            int maxHp = combatEntity.CurrentStats.MaxHp;
            return maxHp > 0 ? (float)combatEntity.Hp / maxHp : 0f;
        }

        private float GetStaminaRatio()
        {
            if (player == null) return 0f;

            return player.MaxStamina > 0f ? player.Stamina / player.MaxStamina : 0f;
        }
    }
}
