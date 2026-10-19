using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    public sealed class PsychokinesisTestEnemy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private Material focusOutline;

        private float currentHealth;
        private Collider hitCollider;
        private LineRenderer outline;
        private readonly Vector3[] outlinePositions = new Vector3[16];
        private static readonly int[] cornerIndices = { 0, 1, 3, 2, 0, 4, 5, 1, 5, 7, 3, 7, 6, 2, 6, 4 };

        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            currentHealth = maxHealth;
            hitCollider = GetComponent<Collider>();
            GameObject outlineObject = new GameObject("Focus Outline");
            outlineObject.transform.SetParent(transform, false);
            outline = outlineObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = true;
            outline.widthMultiplier = 0.025f;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.sharedMaterial = focusOutline;
            outline.positionCount = outlinePositions.Length;
            outline.enabled = false;
        }

        private void Update()
        {
            if (IsDead) gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            SetFocused(false);
        }

        public void SetFocused(bool focused)
        {
            outline.enabled = focused && IsDead == false;
            if (outline.enabled == false) return;

            Bounds bounds = hitCollider.bounds;
            bounds.Expand(0.03f);
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int index = 0; index < outlinePositions.Length; index++)
            {
                int corner = cornerIndices[index];
                outlinePositions[index] = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
            }
            outline.SetPositions(outlinePositions);
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Debug.Log($"{name} 피격: 피해 {damage}, 체력 {currentHealth}/{maxHealth}", this);

            if (IsDead)
            {
                SetFocused(false);
                Debug.Log($"{name} 사망", this);
            }
        }
    }
}
