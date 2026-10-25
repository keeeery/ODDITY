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
        }

        private void Update()
        {
            UpdateFill(hpBar, GetHpRatio());
            UpdateFill(staminaBar, GetStaminaRatio());
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
