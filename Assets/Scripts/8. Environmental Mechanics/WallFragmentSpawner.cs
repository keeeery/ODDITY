using System.Collections.Generic;
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
        private const int MaxDebrisSpawnAttempts = 2;
        private const float SpawnClearance = 0.1f;
        private const int MinimumImpactFragmentCount = 3;
        private const float ImpactSpeedMultiplier = 0.3f;
        private readonly Collider[] spawnOverlaps = new Collider[128];
        // 실제로 만들어진 풀과, 비어 있어 이미 경고한 풀 (경고는 풀마다 한 번만)
        private readonly HashSet<ObjectPoolManager.ObjectPoolData> readyPools = new();
        private readonly HashSet<ObjectPoolManager.ObjectPoolData> warnedPools = new();

        [SerializeField] private PsychokinesisObject[] largeFragmentPrefabs;
        [FormerlySerializedAs("smallFragmentPrefabs")]
        [SerializeField] private PsychokinesisObject[] mediumFragmentPrefabs;
        [SerializeField] private Rigidbody[] smallVisualFragmentPrefabs;
        // 맵에 배치된 금속 소품이 부서질 때 쓰는 얇은 금속 조각, 소품마다 다른 머티리얼을 입혀 꺼낸다.
        [SerializeField] private Rigidbody[] metalShardPrefabs;

        // 크기별 공유 풀. Prefab에는 각 목록의 첫 번째 파편을 쓰고, 꺼낼 때 무작위 파편의 형태를 입힌다.
        [Header("Pool")]
        [SerializeField] private ObjectPoolManager.ObjectPoolData largeFragmentPool;
        [SerializeField] private ObjectPoolManager.ObjectPoolData mediumFragmentPool;
        [SerializeField] private ObjectPoolManager.ObjectPoolData smallFragmentPool;
        [SerializeField] private ObjectPoolManager.ObjectPoolData metalShardPool;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnRadius = 1.8f;
        [SerializeField] private Vector2 launchSpeed = new Vector2(4f, 8f);
        [SerializeField, Min(0f)] private float spinSpeed = 8f;
        [SerializeField, Min(0f)] private float impactSpawnRadius = 0.15f;
        [SerializeField, Min(0.01f)] private float impactFragmentsPerKilogram = 1f;
        [SerializeField, Min(MinimumImpactFragmentCount)] private int maxImpactFragmentCount = 30;
        [Tooltip("금속 소품은 시야를 덜 가리도록 콘크리트보다 적게 흩뿌린다.")]
        [SerializeField, Min(0.01f)] private float metalShardsPerKilogram = 0.4f;
        [SerializeField, Min(MinimumImpactFragmentCount)] private int maxMetalShardCount = 8;
        [SerializeField, Min(0f)] private float metalShardSpawnRadius = 0.5f;

        // 보스 충격파처럼 활성화된 파편을 찾아야 하는 쪽에서 사용, 크기별 풀의 부모(비어 있을 수 있음)
        public Transform GetFragmentPoolParent(FragmentSize size) => GetPool(size).Parent;

        private void Start()
        {
            CreatePool(largeFragmentPool, nameof(largeFragmentPool));
            CreatePool(mediumFragmentPool, nameof(mediumFragmentPool));
            CreatePool(smallFragmentPool, nameof(smallFragmentPool));
            CreatePool(metalShardPool, nameof(metalShardPool));
        }

        private void CreatePool(ObjectPoolManager.ObjectPoolData data, string poolField)
        {
            // ObjectPoolManager는 Key나 Prefab이 비어 있으면 예외가 발생하므로 미리 걸러낸다.
            if (data == null || string.IsNullOrEmpty(data.Key) || data.Prefab == null)
            {
                Debug.LogWarning($"{name}: {poolField}의 Key 또는 Prefab이 비어 있어 풀을 만들지 않았습니다.", this);
                return;
            }

            ObjectPoolManager.Instance.CreatePool(data);
            readyPools.Add(data);
        }

        // 파편 목록이 비었거나 풀이 만들어지지 않았으면 생성하지 않음, 씬 설정이 빠진 경우 예외 대신 경고만 남김
        private bool CanSpawn(Component[] prefabs, ObjectPoolManager.ObjectPoolData pool)
        {
            if (prefabs != null && prefabs.Length > 0 && readyPools.Contains(pool)) return true;

            if (warnedPools.Add(pool))
                Debug.LogWarning($"{name}: 파편 목록이 비어 있거나 풀이 만들어지지 않아 파편을 생성하지 않습니다. (풀 키: '{pool?.Key}')", this);
            return false;
        }

        private void OnEnable()
        {
            PsychokinesisObject.Shattered += SpawnImpactDebris;
        }

        private void OnDisable()
        {
            PsychokinesisObject.Shattered -= SpawnImpactDebris;
        }

        // debrisMaterial이 있으면 그 머티리얼의 금속 조각으로, 없으면(벽 파편, 보스 패턴 오브젝트) 콘크리트 파편으로 부서진다.
        public void SpawnImpactDebris(Vector3 point, Vector3 normal, float mass, Material debrisMaterial)
        {
            // 금속 조각이 준비되지 않은 씬에서는 콘크리트 파편으로 대신 부서진다.
            if (debrisMaterial != null && CanSpawn(metalShardPrefabs, metalShardPool))
            {
                int shardCount = Mathf.Clamp(Mathf.RoundToInt(mass * metalShardsPerKilogram),
                    MinimumImpactFragmentCount, maxMetalShardCount);
                SpawnFragments(metalShardPrefabs, metalShardPool, shardCount, point, normal, metalShardSpawnRadius, ImpactSpeedMultiplier, debrisMaterial);
                return;
            }

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

        [Tooltip("생성할 금속 조각의 가장 긴 축 길이 범위(m)")]
        [SerializeField] private Vector2 metalShardDiameter = new Vector2(0.25f, 0.45f);

        [ContextMenu("Generate Metal Shard Prefabs (6)")]
        private void GenerateMetalShardPrefabs()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Prefab 생성은 Play Mode를 종료한 뒤 실행해 주세요.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Assign generated metal shard prefabs");
            metalShardPrefabs = WallFragmentPrefabGenerator.Generate<Rigidbody>(fragmentTemplate, 6, metalShardDiameter, fragmentDensity, "MetalShard", true, true);

            // 금속 조각 풀은 작은 파편 풀과 같은 부모·확장 단위를 쓴다.
            UnityEditor.SerializedObject serializedSpawner = new UnityEditor.SerializedObject(this);
            UnityEditor.SerializedProperty pool = serializedSpawner.FindProperty(nameof(metalShardPool));
            UnityEditor.SerializedProperty smallPool = serializedSpawner.FindProperty(nameof(smallFragmentPool));
            if (string.IsNullOrEmpty(pool.FindPropertyRelative("key").stringValue))
                pool.FindPropertyRelative("key").stringValue = "MetalShardFragment";
            pool.FindPropertyRelative("expandSize").intValue = smallPool.FindPropertyRelative("expandSize").intValue;
            pool.FindPropertyRelative("parent").objectReferenceValue = smallPool.FindPropertyRelative("parent").objectReferenceValue;
            pool.FindPropertyRelative("prefab").objectReferenceValue = metalShardPrefabs[0].gameObject;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("금속 조각 6개의 Prefab을 생성하고 목록과 풀에 할당했습니다.", this);
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

        // 벽 앞에 무언가(돌진한 보스)가 막고 있을 때 사용, 정면 대신 벽을 따라 좌우·위로 튀게 함
        // sideGap: 충돌 지점에서 좌우로 비워 둘 거리(m), shatterGrace: 보스에 닿아도 파편화하지 않는 시간(초)
        // 보스 패턴으로 생긴 오브젝트로 표시되어, 던졌다가 벽에 맞으면 파편화된다.
        public void SpawnAlongWall(Vector3 point, Vector3 normal, float sideGap, float shatterGrace)
        {
            normal.y = 0f;
            normal.Normalize();
            WallSpread spread = new WallSpread(sideGap, shatterGrace);
            SpawnFragments(largeFragmentPrefabs, largeFragmentPool, 1, point, normal, spawnRadius, spread: spread);
            SpawnFragments(mediumFragmentPrefabs, mediumFragmentPool, 5, point, normal, spawnRadius, spread: spread);
            SpawnFragments(smallVisualFragmentPrefabs, smallFragmentPool, 9, point, normal, spawnRadius, spread: spread);
        }

        private readonly struct WallSpread
        {
            public readonly float SideGap;
            public readonly float ShatterGrace;

            public WallSpread(float sideGap, float shatterGrace)
            {
                SideGap = sideGap;
                ShatterGrace = shatterGrace;
            }
        }

        // 크기별 풀에서 무작위 형태의 파편을 꺼내 배치, 목록이 비었거나 풀이 비면 null
        public WallFragment SpawnFragment(FragmentSize size, Vector3 position, Quaternion rotation)
        {
            Component[] prefabs = GetPrefabs(size);
            if (CanSpawn(prefabs, GetPool(size)) == false) return null;

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

        // materialOverride가 있으면 꺼낸 파편에 원래 형태의 머티리얼 대신 입힌다 (다음에 꺼낼 때 ApplyVariant가 되돌림).
        // spread가 있으면 충돌 지점 정면을 비우고 벽을 따라 좌우·위로 생성·발사한다.
        private void SpawnFragments(Component[] prefabs, ObjectPoolManager.ObjectPoolData pool, int count, Vector3 point, Vector3 normal, float radius, float speedMultiplier = 1f, Material materialOverride = null, WallSpread? spread = null)
        {
            if (CanSpawn(prefabs, pool) == false) return;

            Quaternion surfaceRotation = Quaternion.LookRotation(normal);
            Vector3 wallTangent = surfaceRotation * Vector3.right;
            Vector2 speedRange = launchSpeed * speedMultiplier;
            // 연출용 작은 파편끼리는 겹쳐 생성되어도 무방하다.
            bool ignoreDebris = pool == smallFragmentPool || pool == metalShardPool;
            // 연출용 파편은 막힌 위치를 오래 탐색하기보다 생략해 동시 파괴 시 검사량 제한 (성능 고려)
            int maxAttempts = ignoreDebris ? MaxDebrisSpawnAttempts : MaxSpawnAttempts;

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
                float side = Random.value < 0.5f ? -1f : 1f;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Vector2 offset = spread.HasValue
                        ? new Vector2(side * (spread.Value.SideGap + Random.value * radius), Random.value * radius)
                        : Random.insideUnitCircle * radius;
                    position = point + normal * clearance + surfaceRotation * new Vector3(offset.x, offset.y, 0f);

                    if (IsSpawnBlocked(position + rotation * center, halfExtents, rotation, ignoreDebris)) continue;

                    foundSpace = true;
                    break;
                }

                if (foundSpace == false) continue;

                WallFragment fragment = SpawnVariant(prefab, pool, position, rotation);

                if (fragment == null) continue;
                if (materialOverride != null) fragment.GetComponent<MeshRenderer>().sharedMaterial = materialOverride;
                if (spread.HasValue && fragment.TryGetComponent(out PsychokinesisObject psychokinesisObject))
                {
                    psychokinesisObject.GrantBossShatterGrace(spread.Value.ShatterGrace);
                    psychokinesisObject.MarkSpawnedByBoss();
                }

                Rigidbody body = fragment.GetComponent<Rigidbody>();
                Vector3 direction = spread.HasValue
                    ? (wallTangent * side + Vector3.up * 0.7f + normal * 0.2f + Random.insideUnitSphere * 0.3f).normalized
                    : (normal + Random.insideUnitSphere * 0.6f + Vector3.up * 0.35f).normalized;
                body.linearVelocity = direction * Random.Range(speedRange.x, speedRange.y);
                body.angularVelocity = Random.onUnitSphere * spinSpeed;
            }
        }
    }
}
