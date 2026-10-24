using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 플레이어 체력(CombatEntity)과 스태미나(Player)를 바 Image Fill로 보여주는 간단한 HUD
    public class PlayerHUD : MonoBehaviour
    {
        private const string FillSpritePath = "WhiteSquare";

        [Header("References")]
        [SerializeField] private Player.Player player;
        [SerializeField] private CombatEntity combatEntity;

        [Header("Bars")]
        [SerializeField] private Image hpFill;
        [SerializeField] private Image staminaFill;

        [Header("Display")]
        // 값이 바뀔 때 Fill이 따라가는 속도, 0이면 즉시 반영
        [SerializeField, Min(0f)] private float fillLerpSpeed = 12f;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<Player.Player>();
            if (combatEntity == null && player != null) combatEntity = player.GetComponent<CombatEntity>();

            Sprite fillSprite = Resources.Load<Sprite>(FillSpritePath);
            SetupFill(hpFill, fillSprite);
            SetupFill(staminaFill, fillSprite);
        }

        private void Start()
        {
            hpFill.fillAmount = GetHpRatio();
            staminaFill.fillAmount = GetStaminaRatio();
        }

        private void Update()
        {
            UpdateFill(hpFill, GetHpRatio());
            UpdateFill(staminaFill, GetStaminaRatio());
        }

        private static void SetupFill(Image fill, Sprite sprite)
        {
            if (fill == null) return;

            if (sprite != null) fill.sprite = sprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private void UpdateFill(Image fill, float ratio)
        {
            if (fill == null) return;

            fill.fillAmount = fillLerpSpeed > 0f
                ? Mathf.Lerp(fill.fillAmount, ratio, 1f - Mathf.Exp(-fillLerpSpeed * Time.deltaTime))
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
