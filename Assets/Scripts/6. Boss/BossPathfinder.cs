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

        [Title("Settings")]
        [SerializeField] private string agentTypeName = "Boss"; // 보스 반경으로 구운 NavMesh의 Agent Type 이름
        [SerializeField] private float repathInterval = 0.25f;
        [SerializeField] private float repathDistance = 1f; // 대상이 이만큼 움직이면 주기를 기다리지 않고 다시 계산
        [SerializeField] private float cornerReachDistance = 1.5f; // 이 거리 안에 들어온 꺾임점은 지나간 것으로 보고 다음 꺾임점을 향함
        [SerializeField] private float sampleDistance = 6f; // 보스·대상 위치에서 가장 가까운 NavMesh를 찾을 최대 거리

        [Title("States")]
        [ShowInInspector, ReadOnly] public bool IsDirect { get; private set; } = true;
        [ShowInInspector, ReadOnly] private int cornerIdx = 0;
        [ShowInInspector, ReadOnly] private int cornerCount = 0;

        private readonly Vector3[] corners = new Vector3[MAX_CORNER_COUNT];

        private TimeFreezable clock;
        private NavMeshPath path;
        private NavMeshQueryFilter filter;
        private bool hasAgentType = false;

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

        // 지금 향해야 할 지점, 경로를 쓰지 않을 때는 대상 위치 그대로
        // Idle·Turn·Chase가 한 프레임에 여러 번 물어도 계산은 프레임당 한 번만 함
        public Vector3 GetSteerPoint(Vector3 from, Vector3 to)
        {
            if (steerFrame == Time.frameCount) return steerPoint;
            steerFrame = Time.frameCount;

            if (ShouldRepath(to)) Repath(from, to);

            steerPoint = IsDirect ? to : GetCorner(from, to);
            return steerPoint;
        }

        #endregion

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
            if (Application.isPlaying == false || IsDirect) return;

            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = Color.yellow;
            for (int i = cornerIdx; i < cornerCount; i++)
            {
                Vector3 from = i == cornerIdx ? transform.position : corners[i - 1];
                UnityEditor.Handles.DrawLine(from, corners[i]);
                UnityEditor.Handles.DrawWireDisc(corners[i], Vector3.up, cornerReachDistance);
            }

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
