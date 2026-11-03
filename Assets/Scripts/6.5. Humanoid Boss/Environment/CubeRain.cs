using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TeamOdd.Arena;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace TeamOdd.Environment
{
    public class CubeRain : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private PsychokinesisObject[] prefabs;
        [SerializeField] private CombatEntity boss;
        [SerializeField] private NavMeshSurface surface;
        [SerializeField] private BossIntroCinematic introCinematic;

        // 활성 개수를 Parent 자식으로 세므로 다른 풀과 Key, Parent를 공유하면 안 됨
        [Title("Pool")]
        [SerializeField] private ObjectPoolManager.ObjectPoolData pool;

        [Title("Settings")]
        [SerializeField] private float minCycleTime = 1f;
        [SerializeField] private float maxCycleTime = 2f;
        [SerializeField] private int maxActiveCount = 30;
        [SerializeField] private LayerMask layerMask;

        [Space]
        [SerializeField] private float spawnHeight = 10f;
        [SerializeField] private float sampleDistance = 1f;
        [SerializeField] private int maxSpawnAttempts = 10;
        [SerializeField] private float maxTiltAngle = 25f;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private bool isRunning = false;
        [ShowInInspector, ReadOnly] private bool isPoolReady = false;

        private ObjectPoolManager PoolManager => ObjectPoolManager.Instance;

        private void Start()
        {
            if (CanCreatePool() == false) return;

            PoolManager.CreatePool(pool);
            isPoolReady = true;
        }

        private void OnEnable()
        {
            introCinematic.OnCinematicEnded += Execute;
        }

        private void OnDisable()
        {
            introCinematic.OnCinematicEnded -= Execute;
        }

        public async void Execute()
        {
            if (isRunning) return;
            if (isPoolReady == false) return;

            isRunning = true;

            while (true)
            {
                if (boss == null) break;
                if (boss.IsDead) break;

                float cycleTime = Random.Range(minCycleTime, maxCycleTime);

                if (GetActiveCount() < maxActiveCount) Spawn();

                await UniTask.WaitForSeconds(cycleTime);
            }

            isRunning = false;
        }

        private bool CanCreatePool()
        {
            bool isPoolDataValid =
                pool != null &&
                string.IsNullOrEmpty(pool.Key) == false &&
                pool.Prefab != null &&
                pool.Parent != null;

            if (isPoolDataValid == false)
            {
                Debug.LogError("[CubeRain] Pool Key, Prefab or Parent is empty");
                return false;
            }

            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogError("[CubeRain] Prefabs is empty");
                return false;
            }

            if (surface == null || surface.navMeshData == null)
            {
                Debug.LogError("[CubeRain] NavMeshSurface is not baked");
                return false;
            }

            if (PoolManager == null)
            {
                Debug.LogError("[CubeRain] ObjectPoolManager is not found");
                return false;
            }

            return true;
        }

        // 풀로 돌아간 큐브는 비활성화되므로 Parent 아래 활성 자식 수가 곧 맵에 남은 큐브 수
        private int GetActiveCount()
        {
            Transform parent = pool.Parent;
            int count = 0;

            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.activeSelf) count++;
            }

            return count;
        }

        // 풀에는 투사체 하나만 두고, 꺼낼 때 프리팹의 형태(메시, 콜라이더, 질량, 크기)를 입힘
        private void Spawn()
        {
            PsychokinesisObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            Quaternion rotation = GetRandomRotation();

            if (TryGetSpawnPos(prefab, rotation, out Vector3 spawnPos) == false) return;
            if (PoolManager.GetObjectOrNull(pool.Key) is not WallFragment cube) return;

            cube.ApplyVariant(prefab);
            cube.transform.SetPositionAndRotation(spawnPos, rotation);
            cube.gameObject.SetActive(true);

            // 벽에 던지면 소모되도록 보스 패턴 오브젝트로 표시 (풀에서 다시 꺼내면 OnEnable에서 해제됨)
            if (cube.TryGetComponent(out PsychokinesisObject psychokinesisObject))
                psychokinesisObject.MarkSpawnedByBoss();
        }

        // 수평 임의 축으로 기울여 무작위로 떨어지는 느낌을 줌 (완전 무작위는 모서리로 서서 떨어지는 경우가 잦음)
        private Quaternion GetRandomRotation()
        {
            Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 axis = new(dir.x, 0f, dir.y);
            Quaternion tilt = Quaternion.AngleAxis(Random.Range(0f, maxTiltAngle), axis);

            return tilt * yaw;
        }

        private bool TryGetSpawnPos(PsychokinesisObject prefab, Quaternion rotation, out Vector3 result)
        {
            result = Vector3.zero;

            BoxCollider box = prefab.GetComponent<BoxCollider>();
            Vector3 scale = prefab.transform.localScale;
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);
            Vector3 centerOffset = rotation * Vector3.Scale(box.center, scale);

            // 기울어진 박스는 세로 반높이가 halfExtents.y보다 커지므로 회전된 축들의 세로 성분으로 다시 계산
            float extentY =
                Mathf.Abs((rotation * Vector3.right).y) * halfExtents.x +
                Mathf.Abs((rotation * Vector3.up).y) * halfExtents.y +
                Mathf.Abs((rotation * Vector3.forward).y) * halfExtents.z;

            NavMeshData data = surface.navMeshData;
            Bounds bounds = data.sourceBounds;
            float sampleRange = bounds.extents.y + sampleDistance;

            NavMeshQueryFilter filter = new()
            {
                agentTypeID = surface.agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            for (int i = 0; i < maxSpawnAttempts; i++)
            {
                // sourceBounds는 Surface 기준(크기 제외) 좌표이므로 그 안에서 뽑은 뒤 월드로 변환
                Vector3 localPos = new
                (
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y,
                    Random.Range(bounds.min.z, bounds.max.z)
                );
                Vector3 randPos = data.position + data.rotation * localPos;

                if (NavMesh.SamplePosition(randPos, out NavMeshHit hit, sampleRange, filter) == false) continue;

                // 바닥이 없는 곳에서 뽑히면 가장 가까운 가장자리로 붙어 생성 위치가 가장자리에 몰림
                Vector3 delta = hit.position - randPos;
                delta.y = 0f;

                if (delta.sqrMagnitude > sampleDistance * sampleDistance) continue;

                // 바닥에서 spawnHeight만큼 띄운 위치에서 바닥에 닿을 때까지 내려가며 검사
                Vector3 spawnPos = hit.position + Vector3.up * (spawnHeight + extentY);
                Vector3 boxCenter = spawnPos + centerOffset;

                // BoxCast는 시작 지점에서 이미 겹친 콜라이더를 잡지 못하므로 따로 검사
                bool isOverlapped = Physics.CheckBox
                (
                    boxCenter,
                    halfExtents,
                    rotation,
                    layerMask,
                    QueryTriggerInteraction.Ignore
                );

                if (isOverlapped) continue;

                bool isBlockedBelow = Physics.BoxCast
                (
                    boxCenter,
                    halfExtents,
                    Vector3.down,
                    rotation,
                    spawnHeight,
                    layerMask,
                    QueryTriggerInteraction.Ignore
                );

                if (isBlockedBelow) continue;

                result = spawnPos;
                return true;
            }

            return false;
        }
    }
}
