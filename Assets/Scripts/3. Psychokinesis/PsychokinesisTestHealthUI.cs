using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    public sealed class PsychokinesisTestHealthUI : MonoBehaviour
    {
        [SerializeField] private CombatEntity[] enemies;

        private int[] currentHealth;
        private int[] maxHealth;
        private string[] enemyNames;
        private string[] labels;
        private GUIStyle labelStyle;

        private void Awake()
        {
            currentHealth = new int[enemies.Length];
            maxHealth = new int[enemies.Length];
            enemyNames = new string[enemies.Length];
            labels = new string[enemies.Length];
            for (int index = 0; index < enemies.Length; index++)
            {
                // 테스트 씬의 적은 최대 체력으로 시작하며, 이후 실제 전투 이벤트 결과를 반영한다.
                maxHealth[index] = enemies[index].Stats.MaxHp;
                currentHealth[index] = maxHealth[index];
                enemyNames[index] = enemies[index].name;
                UpdateLabel(index);
            }
        }

        private void OnEnable()
        {
            CombatManager.Instance.OnDamageEventExecuted += HandleDamage;
            CombatManager.Instance.OnHealEventExecuted += HandleHeal;
        }

        private void OnDisable()
        {
            if (CombatManager.IsInitialized == false) return;
            CombatManager.Instance.OnDamageEventExecuted -= HandleDamage;
            CombatManager.Instance.OnHealEventExecuted -= HandleHeal;
        }

        private void HandleDamage(DamageEvent damageEvent, int damage)
        {
            for (int index = 0; index < enemies.Length; index++)
            {
                if (ReferenceEquals(damageEvent.Receiver, enemies[index]) == false) continue;
                int previousHealth = currentHealth[index];
                currentHealth[index] = Mathf.Max(0, previousHealth - damage);
                UpdateLabel(index);
                Debug.Log($"[피해 처리] {enemyNames[index]}: Max(1, 원시 피해 {damageEvent.Info.Damage}"
                    + $" - 방어력 {enemies[index].Stats.Def}) → 실제 처리 피해 {damage}, "
                    + $"HP Max(0, {previousHealth} - {damage}) = {currentHealth[index]}/{maxHealth[index]}", this);
            }
        }

        private void HandleHeal(HealEvent healEvent, int amount)
        {
            for (int index = 0; index < enemies.Length; index++)
            {
                if (ReferenceEquals(healEvent.Receiver, enemies[index]) == false) continue;

                currentHealth[index] = Mathf.Min(maxHealth[index], currentHealth[index] + amount);
                UpdateLabel(index);
            }
        }

        private void UpdateLabel(int index)
        {
            labels[index] = $"{enemyNames[index]}  HP {currentHealth[index]} / {maxHealth[index]}";
        }

        private void OnGUI()
        {
            labelStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft };

            for (int index = 0; index < labels.Length; index++)
                GUI.Box(new Rect(16f, 16f + index * 44f, 320f, 40f), labels[index], labelStyle);
        }
    }
}
