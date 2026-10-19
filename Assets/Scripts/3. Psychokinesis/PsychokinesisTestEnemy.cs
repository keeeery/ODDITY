using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    public sealed class PsychokinesisTestEnemy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        private float currentHealth;

        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        private void Update()
        {
            if (IsDead) gameObject.SetActive(false);
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Debug.Log($"{name} 피격: 피해 {damage}, 체력 {currentHealth}/{maxHealth}", this);

            if (IsDead)
            {
                Debug.Log($"{name} 사망", this);
            }
        }
    }
}
