using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.UI
{
    // 플레이어 체력(CombatEntity)과 스태미나(Player)를 VectorBar로 보여주는 간단한 HUD
    public class PlayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Player.Player player;
        [SerializeField] private CombatEntity combatEntity;

        [Header("Bars")]
        [SerializeField] private VectorBar hpBar;
        [SerializeField] private VectorBar staminaBar;

        [Header("Display")]
        // 값이 바뀔 때 Fill이 따라가는 속도, 0이면 즉시 반영
        [SerializeField, Min(0f)] private float fillLerpSpeed = 12f;

        private void Awake()
        {
            player ??= FindAnyObjectByType<Player.Player>();
            combatEntity ??= player.GetComponent<CombatEntity>();
        }

        private void Start()
        {
            hpBar.FillAmount = GetHpRatio();
            staminaBar.FillAmount = GetStaminaRatio();
        }

        private void Update()
        {
            UpdateFill(hpBar, GetHpRatio());
            UpdateFill(staminaBar, GetStaminaRatio());
        }

        private void UpdateFill(VectorBar bar, float ratio)
        {
            if (bar == null) return;

            bar.FillAmount = fillLerpSpeed > 0f
                ? Mathf.Lerp(bar.FillAmount, ratio, 1f - Mathf.Exp(-fillLerpSpeed * Time.deltaTime))
                : ratio;
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
