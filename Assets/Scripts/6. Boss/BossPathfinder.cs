using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;
using TeamOdd.TimeControl;

namespace TeamOdd.Boss
{
    // 보스가 걸어서 대상을 따라갈 때 장애물을 돌아갈 경로를 계산하고, 지금 향해야 할 지점을 알려줌
    // 경로만 계산하고 이동은 BossMovement가 맡음, NavMeshAgent가 위치를 직접 옮기면 Sweep 충돌·시간 정지·루트 모션 처리와 어긋남
    // 대상까지 NavMesh 위로 막힘 없이 이어지면 경로 대신 대상을 바로 향해 장애물이 없을 때의 추적 느낌을 유지
    public class BossPathfinder : MonoBehaviour
    {
        private const int MAX_CORNER_COUNT = 32;
        private const float OFF_MESH_DISTANCE = 0.1f;
        private const float CLAMP_SAMPLE_DISTANCE = 1f; // 이동 제한 때 발밑 NavMesh를 찾는 거리, 이보다 멀면 NavMesh 밖으로 보고 자르지 않음

        [Title("Settings")]
        [SerializeField] private string agentTypeName = "Boss"; // 보스 반경으로 구운 NavMesh의 Agent Type 이름
        [SerializeField] private float repathInterval = 0.25f;
        [SerializeField] private float repathDistance = 1f; // 대상이 이만큼 움직이면 주기를 기다리지 않고 다시 계산
        [SerializeField] private float cornerReachDistance = 1.5f; // 이 거리 안에 들어온 꺾임점은 지나간 것으로 보고 다음 꺾임점을 향함
        [SerializeField] private float sampleDistance = 6f; // 보스·대상 위치에서 가장 가까운 NavMesh를 찾을 최대 거리

        [Title("Wall Avoidance")]
        [SerializeField] private float wallClearance = 2f; // NavMesh 경계에서 이 거리 안으로 들어오면 벽 반대쪽으로 밀어냄, 0이면 끔
        [SerializeField] private float avoidWeight = 1f; // 경계에 닿았을 때 진행 방향 대비 밀어내는 세기

        [Title("States")]
        [ShowInInspector, ReadOnly] public bool IsDirect { get; private set; } = true;
        [ShowInInspector, ReadOnly] private int cornerIdx;
        [ShowInInspector, ReadOnly] private int cornerCount;

        private readonly Vector3[] corners = new Vector3[MAX_CORNER_COUNT];

        private TimeFreezable clock;
        private NavMeshPath path;
        private NavMeshQueryFilter filter;
        private bool hasAgentType;

        private float nextRepathTime = float.NegativeInfinity;
        private Vector3 lastGoal;
        private Vector3 steerPoint;
        private int steerFrame = -1;

        private void Awake()
        {
            clock = TimeFreezable.GetOrAdd(gameObject);
            path = new();

            hasAgentType = TryFindAgentTypeID(out int agentTypeID);
            filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            if (hasAgentType == false)
                Debug.LogWarning($"{name}: '{agentTypeName}' Agent Type이 없어 장애물을 돌아가지 않고 대상을 바로 향합니다.", this);
        }

        #region PUBLIC_METHODS

        // position에서 maxDistance 안의 가장 가까운 NavMesh 위 자리, 도약 착지처럼 구멍·벽 안에 내려앉았을 때 되돌릴 자리
        // 경계에서 edgePadding보다 가까우면 안쪽으로 그만큼 들임, 경계 바로 위에 서면 벌린 발 한쪽이 구멍·단차 밖으로 떠서
        // 모서리처럼 경계가 둘인 곳을 위해 두 번까지 들이고, 들인 자리까지 NavMesh로 곧장 이어질 때만 옮김
        public bool TryGetNearestOnNavMesh(Vector3 position, float maxDistance, float edgePadding, out Vector3 result)
        {
            result = position;
            if (hasAgentType == false || NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, filter) == false) return false;

            result = hit.position;
            for (int i = 0; i < 2 && edgePadding > 0f; i++)
            {
                if (NavMesh.FindClosestEdge(result, out NavMeshHit edge, filter) == false || edge.distance >= edgePadding) break;

                // 경계 위에 딱 붙어(구멍·벽에서 끌어온 자리) 방향을 못 구하면 경계의 법선을 씀
                Vector3 inward = new Vector3(result.x - edge.position.x, 0f, result.z - edge.position.z);
                if (inward.sqrMagnitude < 0.0001f) inward = new Vector3(edge.normal.x, 0f, edge.normal.z);
                if (inward.sqrMagnitude < 0.0001f) break;

                Vector3 candidate = edge.position + inward.normalized * edgePadding;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit inside, edgePadding, filter) == false) break;
                if (NavMesh.Raycast(result, inside.position, out _, filter)) break;

                result = inside.position;
            }

            return true;
        }

        // NavMesh 밖(구멍·맵 바깥)으로 나가지 않도록 수평 이동량을 경계에서 자르고, 남은 만큼은 경계를 따라 미끄러지게 함
        // NavMesh는 몸 반경으로 깎여 있어 경계가 곧 몸이 벽·구멍 가장자리에 닿는 자리, 서 있는 곳 근처에 NavMesh가 없으면(공중 등) 자르지 않음
        public Vector3 ClampToNavMesh(Vector3 from, Vector3 delta)
        {
            delta.y = 0f;
            if (hasAgentType == false || delta.sqrMagnitude < 0.00000001f) return delta;
            if (NavMesh.SamplePosition(from, out NavMeshHit start, CLAMP_SAMPLE_DISTANCE, filter) == false) return delta;

            Vector3 origin = start.position;
            if (NavMesh.Raycast(origin, origin + delta, out NavMeshHit edge, filter) == false) return delta;

            Vector3 moved = edge.position - origin;
            moved.y = 0f;

            Vector3 normal = new Vector3(edge.normal.x, 0f, edge.normal.z).normalized;
            Vector3 slide = Vector3.ProjectOnPlane(delta - moved, normal);
            slide.y = 0f;

            if (slide.sqrMagnitude > 0.00000001f && NavMesh.Raycast(edge.position, edge.position + slide, out NavMeshHit second, filter))
            {
                slide = second.position - edge.position;
                slide.y = 0f;
            }

            return moved + slide;
        }

        // 지금 향해야 할 지점, 경로를 쓰지 않을 때는 대상 위치 그대로
        // Idle·Turn·Chase가 한 프레임에 여러 번 물어도 계산은 프레임당 한 번만 함
        public Vector3 GetSteerPoint(Vector3 from, Vector3 to)
        {
            if (steerFrame == Time.frameCount) return steerPoint;
            steerFrame = Time.frameCount;

            if (ShouldRepath(to)) Repath(from, to);

            Vector3 goal = IsDirect ? to : GetCorner(from, to);
            steerPoint = ApplyWallAvoidance(from, goal);
            return steerPoint;
        }

        #endregion

        // 최단 경로는 깎인 경계에 붙어 있어 회전하며 안쪽으로 파고들면 실제 벽에 닿으므로 경계에서 떨어지도록 방향을 틂
        // 좁은 통로에선 양쪽에서 밀어내는 힘이 상쇄돼 가운데로 걸음
        private Vector3 ApplyWallAvoidance(Vector3 from, Vector3 goal)
        {
            if (wallClearance <= 0f) return goal;
            if (TryGetWallPush(from, out Vector3 away, out float push) == false) return goal;

            Vector3 toGoal = goal - from;
            toGoal.y = 0f;

            float distance = toGoal.magnitude;
            if (distance <= 0.0001f) return goal;

            Vector3 dir = toGoal / distance + away * (push * avoidWeight);
            if (dir.sqrMagnitude <= 0.0001f) return goal;

            return from + dir.normalized * distance;
        }

        // 경계에 가까울수록 1에 가까운 세기로 벽 반대쪽 방향을 돌려줌
        // 이미 깎인 경계 밖(실제 벽 쪽)으로 나가 있으면 최대 세기로 NavMesh 안쪽을 향함
        private bool TryGetWallPush(Vector3 from, out Vector3 away, out float push)
        {
            away = Vector3.zero;
            push = 0f;

            if (hasAgentType == false) return false;
            if (TrySample(from, out Vector3 onMesh) == false) return false;

            Vector3 back = onMesh - from;
            back.y = 0f;

            if (back.sqrMagnitude > OFF_MESH_DISTANCE * OFF_MESH_DISTANCE)
            {
                away = back.normalized;
                push = 1f;
                return true;
            }

            if (NavMesh.FindClosestEdge(onMesh, out NavMeshHit edge, filter) == false) return false;
            if (edge.distance >= wallClearance) return false;

            // 경계 위에 딱 붙어 있어 방향을 못 구하면 경계의 법선을 씀
            away = onMesh - edge.position;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.0001f) away = new Vector3(edge.normal.x, 0f, edge.normal.z);
            if (away.sqrMagnitude <= 0.0001f) return false;

            away.Normalize();
            push = 1f - edge.distance / wallClearance;
            return true;
        }

        private bool ShouldRepath(Vector3 to)
        {
            if (hasAgentType == false) return false;
            if (clock.LocalTime >= nextRepathTime) return true;

            return (to - lastGoal).sqrMagnitude >= repathDistance * repathDistance;
        }

        // NavMesh를 못 찾거나 경로가 없으면 바로 향하고, 벽에 막히는 건 BossMovement의 Sweep이 처리
        private void Repath(Vector3 from, Vector3 to)
        {
            nextRepathTime = clock.LocalTime + repathInterval;
            lastGoal = to;

            IsDirect = true;
            cornerIdx = 0;
            cornerCount = 0;

            if (TrySample(from, out Vector3 start) == false) return;
            if (TrySample(to, out Vector3 goal) == false) return;

            // 구울 때 보스 반경이 반영돼 있어 Raycast가 막히지 않으면 몸통째로 지나갈 수 있는 직선
            if (NavMesh.Raycast(start, goal, out _, filter) == false) return;

            if (NavMesh.CalculatePath(start, goal, filter, path) == false) return;
            if (path.status == NavMeshPathStatus.PathInvalid) return;

            // 대상에게 닿지 못하는 부분 경로(PathPartial)도 갈 수 있는 가장 가까운 곳까지는 따라감
            cornerCount = path.GetCornersNonAlloc(corners);
            if (cornerCount < 2) return;

            cornerIdx = 1; // 0번은 출발점
            IsDirect = false;
        }

        private Vector3 GetCorner(Vector3 from, Vector3 to)
        {
            while (cornerIdx < cornerCount && IsReached(from, corners[cornerIdx]))
                cornerIdx++;

            // 마지막 꺾임점까지 지났으면 남은 거리는 대상을 바로 향함
            return cornerIdx < cornerCount ? corners[cornerIdx] : to;
        }

        private bool IsReached(Vector3 from, Vector3 corner)
        {
            Vector3 delta = corner - from;
            delta.y = 0f;

            return delta.sqrMagnitude <= cornerReachDistance * cornerReachDistance;
        }

        private bool TrySample(Vector3 position, out Vector3 result)
        {
            result = position;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleDistance, filter) == false) return false;

            result = hit.position;
            return true;
        }

        private bool TryFindAgentTypeID(out int result)
        {
            result = 0;

            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int agentTypeID = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(agentTypeID) != agentTypeName) continue;

                result = agentTypeID;
                return true;
            }

            return false;
        }

        #region PATH_VISUALIZATION

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false) return;

            Color originalColor = UnityEditor.Handles.color;

            // 벽 반발까지 반영해 실제로 향하는 방향
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawLine(transform.position, steerPoint);

            if (IsDirect == false)
            {
                UnityEditor.Handles.color = Color.yellow;
                for (int i = cornerIdx; i < cornerCount; i++)
                {
                    Vector3 from = i == cornerIdx ? transform.position : corners[i - 1];
                    UnityEditor.Handles.DrawLine(from, corners[i]);
                    UnityEditor.Handles.DrawWireDisc(corners[i], Vector3.up, cornerReachDistance);
                }
            }

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
