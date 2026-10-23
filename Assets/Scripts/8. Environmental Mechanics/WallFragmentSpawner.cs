using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamOdd.Arena
{
    public enum FragmentSize
    {
        Large,
        Medium,
        Small
    }

    public sealed class WallFragmentSpawner : MonoBehaviour
    {
        private const int MaxSpawnAttempts = 8;
        private const float SpawnClearance = 0.1f;
        private const int MinimumImpactFragmentCount = 3;
        private const float ImpactSpeedMultiplier = 0.3f;
        private readonly Collider[] spawnOverlaps = new Collider[128];

        [SerializeField] private PsychokinesisObject[] largeFragmentPrefabs;
        [FormerlySerializedAs("smallFragmentPrefabs")]
        [SerializeField] private PsychokinesisObject[] mediumFragmentPrefabs;
        [SerializeField] private Rigidbody[] smallVisualFragmentPrefabs;

        // 크기별 공유 풀. Prefab에는 각 목록의 첫 번째 파편을 쓰고, 꺼낼 때 무작위 파편의 형태를 입힌다.
        [Header("Pool")]
        [SerializeField] private ObjectPoolManager.ObjectPoolData largeFragmentPool;
        [SerializeField] private ObjectPoolManager.ObjectPoolData mediumFragmentPool;
        [SerializeField] private ObjectPoolManager.ObjectPoolData smallFragmentPool;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnRadius = 1.8f;
        [SerializeField] private Vector2 launchSpeed = new Vector2(4f, 8f);
        [SerializeField, Min(0f)] private float spinSpeed = 8f;
        [SerializeField, Min(0f)] private float impactSpawnRadius = 0.15f;
        [SerializeField, Min(0.01f)] private float impactFragmentsPerKilogram = 1f;
        [SerializeField, Min(MinimumImpactFragmentCount)] private int maxImpactFragmentCount = 30;

        private void Start()
        {
            CreatePool(largeFragmentPool, nameof(largeFragmentPool));
            CreatePool(mediumFragmentPool, nameof(mediumFragmentPool));
            CreatePool(smallFragmentPool, nameof(smallFragmentPool));
        }

        private void CreatePool(ObjectPoolManager.ObjectPoolData data, string poolField)
        {
            // ObjectPoolManager는 Key나 Prefab이 비어 있으면 예외가 발생하므로 미리 걸러낸다.
            if (data == null || string.IsNullOrEmpty(data.Key) || data.Prefab == null)
            {
                Debug.LogError($"{name}: {poolField}의 Key 또는 Prefab이 비어 있어 풀을 만들지 않았습니다.", this);
                return;
            }

            ObjectPoolManager.Instance.CreatePool(data);
        }

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
            SpawnFragments(smallVisualFragmentPrefabs, smallFragmentPool, count, point, normal, impactSpawnRadius, ImpactSpeedMultiplier);
        }

#if UNITY_EDITOR
        [Header("Prefab Generation")]
        [FormerlySerializedAs("fragmentPrefab")]
        [SerializeField] private PsychokinesisObject fragmentTemplate;
        [FormerlySerializedAs("fragmentScale")]
        [FormerlySerializedAs("mediumFragmentScale")]
        [Tooltip("생성할 파편의 가장 긴 축 길이 범위(m)")]
        [SerializeField] private Vector2 mediumFragmentDiameter = new Vector2(0.594f, 0.99f);
        [FormerlySerializedAs("smallFragmentScale")]
        [Tooltip("생성할 작은 파편의 가장 긴 축 길이 범위(m)")]
        [SerializeField] private Vector2 smallFragmentDiameter = new Vector2(0.3f, 0.5f);
        [FormerlySerializedAs("largeFragmentScale")]
        [Tooltip("생성할 파편의 가장 긴 축 길이 범위(m)")]
        [SerializeField] private Vector2 largeFragmentDiameter = new Vector2(1.024f, 1.1733334f);
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
            largeFragmentPrefabs = WallFragmentPrefabGenerator.Generate<PsychokinesisObject>(fragmentTemplate, 10, largeFragmentDiameter, fragmentDensity, "Large");
            mediumFragmentPrefabs = WallFragmentPrefabGenerator.Generate<PsychokinesisObject>(fragmentTemplate, 20, mediumFragmentDiameter, fragmentDensity, "Medium");
            smallVisualFragmentPrefabs = WallFragmentPrefabGenerator.Generate<Rigidbody>(fragmentTemplate, 5, smallFragmentDiameter, fragmentDensity, "Small", true);
            AssignPoolPrefabs();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("큰 파편 10개, 중간 파편 20개, 연출용 작은 파편 5개의 Prefab을 생성하고 목록에 할당했습니다.", this);
        }

        // ObjectPoolData의 필드가 private이므로 직렬화 경로로 풀 Prefab만 교체한다.
        private void AssignPoolPrefabs()
        {
            UnityEditor.SerializedObject serializedSpawner = new UnityEditor.SerializedObject(this);
            AssignPoolPrefab(serializedSpawner, nameof(largeFragmentPool), largeFragmentPrefabs[0]);
            AssignPoolPrefab(serializedSpawner, nameof(mediumFragmentPool), mediumFragmentPrefabs[0]);
            AssignPoolPrefab(serializedSpawner, nameof(smallFragmentPool), smallVisualFragmentPrefabs[0]);
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignPoolPrefab(UnityEditor.SerializedObject serializedSpawner, string poolField, Component prefab)
        {
            serializedSpawner.FindProperty(poolField).FindPropertyRelative("prefab").objectReferenceValue = prefab.gameObject;
        }
#endif

        // point는 월드 좌표의 피격 위치, normal은 벽 바깥쪽을 향하는 표면 법선
        public void Spawn(Vector3 point, Vector3 normal)
        {
            normal.Normalize();
            SpawnFragments(largeFragmentPrefabs, largeFragmentPool, 1, point, normal, spawnRadius);
            SpawnFragments(mediumFragmentPrefabs, mediumFragmentPool, 5, point, normal, spawnRadius);
            SpawnFragments(smallVisualFragmentPrefabs, smallFragmentPool, 9, point, normal, spawnRadius);
        }

        // 크기별 풀에서 무작위 형태의 파편을 꺼내 배치
        public WallFragment SpawnFragment(FragmentSize size, Vector3 position, Quaternion rotation)
        {
            Component[] prefabs = GetPrefabs(size);
            return SpawnVariant(prefabs[Random.Range(0, prefabs.Length)], GetPool(size), position, rotation);
        }

        private Component[] GetPrefabs(FragmentSize size) => size switch
        {
            FragmentSize.Large => largeFragmentPrefabs,
            FragmentSize.Medium => mediumFragmentPrefabs,
            _ => smallVisualFragmentPrefabs
        };

        private ObjectPoolManager.ObjectPoolData GetPool(FragmentSize size) => size switch
        {
            FragmentSize.Large => largeFragmentPool,
            FragmentSize.Medium => mediumFragmentPool,
            _ => smallFragmentPool
        };

        private static WallFragment SpawnVariant(Component prefab, ObjectPoolManager.ObjectPoolData pool, Vector3 position, Quaternion rotation)
        {
            WallFragment fragment = (WallFragment)ObjectPoolManager.Instance.GetObjectOrNull(pool.Key);

            if (fragment == null) return null;

            fragment.ApplyVariant(prefab);
            fragment.transform.SetPositionAndRotation(position, rotation);
            fragment.gameObject.SetActive(true);
            return fragment;
        }

        private bool IsSpawnBlocked(Vector3 center, Vector3 halfExtents, Quaternion rotation, bool ignoreDebris)
        {
            if (ignoreDebris == false)
                return Physics.CheckBox(
                    center, halfExtents, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, spawnOverlaps, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // 결과가 잘렸다면 누락된 벽이나 다른 오브젝트가 있을 수 있다.
            if (count == spawnOverlaps.Length) return true;

            for (int index = 0; index < count; index++)
            {
                Rigidbody body = spawnOverlaps[index].attachedRigidbody;

                if (body != null && body.TryGetComponent<WallFragmentDebris>(out _)) continue;
                return true;
            }
            return false;
        }

        private void SpawnFragments(Component[] prefabs, ObjectPoolManager.ObjectPoolData pool, int count, Vector3 point, Vector3 normal, float radius, float speedMultiplier = 1f)
        {
            Quaternion surfaceRotation = Quaternion.LookRotation(normal);
            Vector2 speedRange = launchSpeed * speedMultiplier;
            // 연출용 작은 파편끼리는 겹쳐 생성되어도 무방하다.
            bool ignoreDebris = pool == smallFragmentPool;

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

                    if (IsSpawnBlocked(position + rotation * center, halfExtents, rotation, ignoreDebris)) continue;

                    foundSpace = true;
                    break;
                }

                if (foundSpace == false) continue;

                WallFragment fragment = SpawnVariant(prefab, pool, position, rotation);

                if (fragment == null) continue;

                Rigidbody body = fragment.GetComponent<Rigidbody>();
                Vector3 direction = (normal + Random.insideUnitSphere * 0.6f + Vector3.up * 0.35f).normalized;
                body.linearVelocity = direction * Random.Range(speedRange.x, speedRange.y);
                body.angularVelocity = Random.onUnitSphere * spinSpeed;
            }
        }
    }
}
