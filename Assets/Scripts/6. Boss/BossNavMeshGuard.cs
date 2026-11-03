using System;
using UnityEngine;
using Sirenix.OdinInspector;
using TeamOdd.TimeControl;

namespace TeamOdd.Boss
{
    // 보스가 NavMesh(BossPathfinder의 Agent Type) 밖에 graceTime 넘게 머물면 가장 가까운 NavMesh 위로 순간이동시킴
    // BossMovement의 NavMesh 제한은 발밑 가까이에 NavMesh가 있을 때만 자르므로, 순간이동·루트 모션 등으로 한 번 크게 벗어나면 맵 밖을 계속 걸어 다님
    // 이동 제한(constrainToNavMesh)과 따로 동작해 제한을 꺼 둔 보스도 되돌림
    // 도약 중(BossMovement.IsAirborne)은 구멍·벽을 넘어가는 게 정상이라 검사하지 않음, 연출 중에는 IsSuppressed로 끔
    // 가까운 NavMesh를 못 찾으면(너무 멀리 나감) 마지막으로 NavMesh 위에 있던 자리로 되돌림
    public class BossNavMeshGuard : MonoBehaviour
    {
        private const float GROUND_SAMPLE_DISTANCE = 3f; // 발밑 NavMesh를 찾을 거리, 보스 위치(바닥 + 들어 올린 높이)와 NavMesh의 높이 차를 덮음

        [Title("Settings")]
        [Tooltip("NavMesh 위에 있는지 검사하는 간격(초)")]
        [SerializeField, Min(0.02f)] private float checkInterval = 0.2f;
        [Tooltip("가장 가까운 NavMesh와의 수평 거리가 이 이하면 NavMesh 위로 봄(m), 경계를 살짝 넘는 정도는 무시")]
        [SerializeField, Min(0f)] private float offMeshTolerance = 0.5f;
        [Tooltip("NavMesh 밖에 이 시간 넘게 머물면 되돌림(초), 루트 모션 등으로 잠깐 나갔다 들어오는 건 넘김")]
        [SerializeField, Min(0f)] private float graceTime = 0.3f;
        [Tooltip("되돌릴 NavMesh를 찾을 최대 거리(m)")]
        [SerializeField, Min(0f)] private float searchDistance = 20f;
        [Tooltip("되돌릴 자리를 NavMesh 경계에서 이만큼 안쪽으로 들임(m)")]
        [SerializeField, Min(0f)] private float edgePadding = 0.6f;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private bool isOffMesh = false;
        [ShowInInspector, ReadOnly] private float offMeshStartTime = 0f;
        [ShowInInspector, ReadOnly] private bool hasSafePos = false;
        [ShowInInspector, ReadOnly] private Vector3 safePos = Vector3.zero;

        private TimeFreezable clock;
        private BossMovement movement;
        private BossPathfinder pathfinder;
        private float nextCheckTime = float.NegativeInfinity;

        // 켜 두면 검사하지 않음, 등장·사망처럼 보스를 일부러 다른 곳에 두는 연출 중에 켬
        public bool IsSuppressed { get; set; } = false;

        // 되돌린 뒤 발생 (벗어나 있던 자리, 되돌린 자리)
        public event Action<Vector3, Vector3> OnRecovered;

        private void Awake()
        {
            clock = TimeFreezable.GetOrAdd(gameObject);
            movement = GetComponent<BossMovement>();
            pathfinder = GetComponent<BossPathfinder>();

            if (movement == null || pathfinder == null)
            {
                Debug.LogError($"[BossNavMeshGuard] {name} needs BossMovement and BossPathfinder on the same object.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (clock.IsFrozen) return;

            // 도약·연출이 끝난 뒤 그 전에 쌓인 이탈 시간으로 바로 되돌리지 않도록 비움
            if (IsSuppressed || movement.IsAirborne)
            {
                isOffMesh = false;
                return;
            }

            if (clock.LocalTime < nextCheckTime) return;
            nextCheckTime = clock.LocalTime + checkInterval;

            if (TryGetGroundOnNavMesh(out Vector3 ground))
            {
                isOffMesh = false;
                safePos = ground;
                hasSafePos = true;
                return;
            }

            if (isOffMesh == false)
            {
                isOffMesh = true;
                offMeshStartTime = clock.LocalTime;
            }

            bool isGraceOver = clock.LocalTime - offMeshStartTime >= graceTime;
            if (isGraceOver) Recover();
        }

        // 발밑 가까이의 NavMesh 위 자리, 수평으로 offMeshTolerance보다 멀면 NavMesh 밖으로 봄
        private bool TryGetGroundOnNavMesh(out Vector3 ground)
        {
            Vector3 pos = movement.Position;
            if (pathfinder.TryGetNearestOnNavMesh(pos, GROUND_SAMPLE_DISTANCE, 0f, out ground) == false) return false;

            Vector2 offset = new(ground.x - pos.x, ground.z - pos.z);
            return offset.sqrMagnitude <= offMeshTolerance * offMeshTolerance;
        }

        private void Recover()
        {
            isOffMesh = false;

            Vector3 from = movement.Position;
            if (pathfinder.TryGetNearestOnNavMesh(from, searchDistance, edgePadding, out Vector3 to) == false)
            {
                if (hasSafePos == false) return;
                to = safePos;
            }

            movement.Teleport(to, movement.Rotation);
            OnRecovered?.Invoke(from, to);
        }
    }
}
