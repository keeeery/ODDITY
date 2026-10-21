using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamOdd.Arena
{
    public sealed class WallFragmentSpawner : MonoBehaviour
    {
        private const int MaxSpawnAttempts = 8;
        private const float SpawnClearance = 0.1f;
        private const int MinimumImpactFragmentCount = 3;
        private const float ImpactSpeedMultiplier = 0.4f;
#if UNITY_EDITOR
        private const float MinimumLargeScale = 1.28f;
        private const float MinimumMediumScale = 0.66f;
#endif

        [SerializeField] private PsychokinesisObject[] largeFragmentPrefabs;
        [FormerlySerializedAs("smallFragmentPrefabs")]
        [SerializeField] private PsychokinesisObject[] mediumFragmentPrefabs;
        [SerializeField] private Rigidbody[] smallVisualFragmentPrefabs;
        [SerializeField] private Transform fragmentParent;
        [SerializeField, Min(0f)] private float spawnRadius = 1.8f;
        [SerializeField] private Vector2 launchSpeed = new Vector2(4f, 8f);
        [SerializeField, Min(0f)] private float spinSpeed = 8f;
        [SerializeField, Min(0f)] private float impactSpawnRadius = 0.15f;
        [SerializeField, Min(0.01f)] private float impactFragmentsPerKilogram = 1f;
        [SerializeField, Min(MinimumImpactFragmentCount)] private int maxImpactFragmentCount = 30;

        private void OnEnable()
        {
            PsychokinesisObject.Shattered += SpawnImpactDebris;
        }

        private void OnDisable()
        {
            PsychokinesisObject.Shattered -= SpawnImpactDebris;
        }

        public void SpawnImpactDebris(Vector3 point, Vector3 normal, float mass)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(mass * impactFragmentsPerKilogram),
                MinimumImpactFragmentCount, maxImpactFragmentCount);
            SpawnFragments(smallVisualFragmentPrefabs, count, point, normal, impactSpawnRadius, ImpactSpeedMultiplier);
        }

#if UNITY_EDITOR
        [Header("Prefab Generation")]
        [FormerlySerializedAs("fragmentPrefab")]
        [SerializeField] private PsychokinesisObject fragmentTemplate;
        [FormerlySerializedAs("fragmentScale")]
        [SerializeField] private Vector2 mediumFragmentScale = new Vector2(MinimumMediumScale, 1.1f);
        [SerializeField] private Vector2 smallFragmentScale = new Vector2(0.03528f, 0.07938f);
        [SerializeField] private Vector2 largeFragmentScale = new Vector2(MinimumLargeScale, 2.2f * 2f / 3f);
        [SerializeField, Min(0.01f)] private float fragmentDensity = 5f;

        [ContextMenu("Generate Fragment Prefabs (10 Large, 20 Medium, 5 Small)")]
        private void GenerateFragmentPrefabs()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Prefab 생성은 Play Mode를 종료한 뒤 실행해 주세요.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Assign generated fragment prefabs");
            // 기존 Scene에 저장된 크기에도 새 최소값을 적용한다.
            largeFragmentScale.x = Mathf.Max(MinimumLargeScale, largeFragmentScale.x);
            largeFragmentScale.y = Mathf.Max(largeFragmentScale.x, largeFragmentScale.y);
            mediumFragmentScale.x = Mathf.Max(MinimumMediumScale, mediumFragmentScale.x);
            mediumFragmentScale.y = Mathf.Max(mediumFragmentScale.x, mediumFragmentScale.y);
            largeFragmentPrefabs = WallFragmentPrefabGenerator.Generate<PsychokinesisObject>(fragmentTemplate, 10, largeFragmentScale, fragmentDensity, "Large");
            mediumFragmentPrefabs = WallFragmentPrefabGenerator.Generate<PsychokinesisObject>(fragmentTemplate, 20, mediumFragmentScale, fragmentDensity, "Medium");
            smallVisualFragmentPrefabs = WallFragmentPrefabGenerator.Generate<Rigidbody>(fragmentTemplate, 5, smallFragmentScale, fragmentDensity, "Small", true);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("큰 파편 10개, 중간 파편 20개, 연출용 작은 파편 5개의 Prefab을 생성하고 목록에 할당했습니다.", this);
        }
#endif

        // point는 월드 좌표의 피격 위치, normal은 벽 바깥쪽을 향하는 표면 법선
        public void Spawn(Vector3 point, Vector3 normal)
        {
            normal.Normalize();
            SpawnFragments(largeFragmentPrefabs, 1, point, normal, spawnRadius);
            SpawnFragments(mediumFragmentPrefabs, 5, point, normal, spawnRadius);
            SpawnFragments(smallVisualFragmentPrefabs, 9, point, normal, spawnRadius);
        }

        private void SpawnFragments(Component[] prefabs, int count, Vector3 point, Vector3 normal, float radius, float speedMultiplier = 1f)
        {
            Quaternion surfaceRotation = Quaternion.LookRotation(normal);
            Vector2 speedRange = launchSpeed * speedMultiplier;

            for (int index = 0; index < count; index++)
            {
                Component prefab = prefabs[Random.Range(0, prefabs.Length)];
                BoxCollider sourceCollider = prefab.GetComponent<BoxCollider>();
                Vector3 scale = prefab.transform.localScale;
                Vector3 halfExtents = Vector3.Scale(sourceCollider.size * 0.5f, scale);
                Vector3 center = Vector3.Scale(sourceCollider.center, scale);
                Quaternion rotation = Random.rotation;
                float clearance = halfExtents.magnitude + center.magnitude + SpawnClearance;
                Vector3 position = Vector3.zero;
                bool foundSpace = false;

                for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
                {
                    Vector2 offset = Random.insideUnitCircle * radius;
                    position = point + normal * clearance + surfaceRotation * new Vector3(offset.x, offset.y, 0f);

                    if (Physics.CheckBox(position + rotation * center, halfExtents, rotation,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        continue;

                    foundSpace = true;
                    break;
                }

                if (foundSpace == false) continue;

                GameObject fragment = Instantiate(prefab.gameObject, position, rotation, fragmentParent);
                fragment.SetActive(true);
                Rigidbody body = fragment.GetComponent<Rigidbody>();
                Vector3 direction = (normal + Random.insideUnitSphere * 0.6f + Vector3.up * 0.35f).normalized;
                body.linearVelocity = direction * Random.Range(speedRange.x, speedRange.y);
                body.angularVelocity = Random.onUnitSphere * spinSpeed;
            }
        }
    }
}
