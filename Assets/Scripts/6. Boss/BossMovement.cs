using System;
using UnityEngine;
using Sirenix.OdinInspector;
using TeamOdd.TimeControl;

namespace TeamOdd.Boss
{
    // Kinematic 보스의 모든 수평 이동·회전(상태 이동, 루트 모션)을 모아 벽에 막히게 하고, 막힌 순간을 알림
    // 플레이어는 통과하며, 겹친 플레이어 처리는 PlayerController가 맡음
    // 요청은 상태의 Update에서 쌓고, 실제 적용은 FixedUpdate에서 MovePosition/MoveRotation으로 함
    // Interpolate가 켜진 몸체의 transform을 직접 바꾸면 보간값에 덮여 회전이 사라지고, 늦은 보간 위치가 몸체로 되돌아가 이동도 줄어듦
    // Kinematic이라 바닥이 몸을 밀어 올려 주지 않으므로, groundMask가 있으면 발밑 바닥 윗면을 재서 stepHeight 이내 단차는 올라타고 내려감
    // groundMask가 비어 있으면 높이를 바꾸지 않음(평평한 맵의 보스, 높이는 애니메이션이 맡음)
    // 바닥이 벽과 같은 레이어(Wall)인 맵을 위해, 벽 판정에 걸린 면의 윗면이 stepHeight 이내 바닥이면 단차로 보고 막지 않음
    // constrainToNavMesh를 켜면 벽 판정 뒤의 이동을 BossPathfinder의 NavMesh 경계에서 잘라, 벽이 없는 구멍·맵 바깥으로도 나가지 않음
    // (뒷걸음·루트 모션 공격·돌진 모두 해당, 벽 충돌 알림은 자르기 전 벽 판정에서 나가므로 돌진 기절은 그대로)
    // 도약 중(BeginAirborne~EndAirborne)에는 벽 판정과 NavMesh 제한 없이 구멍·벽을 넘어가고, 착지할 때 가장 가까운 NavMesh 위로 옮김(constrainToNavMesh를 켠 보스만)
    public class BossMovement : MonoBehaviour
    {
        private const float SKIN_WIDTH = 0.05f;
        private const int MAX_SLIDE_COUNT = 2;
        private const float BELOW_FEET_MARGIN = 0.1f; // 발 높이보다 이만큼 더 낮은 접촉은 바닥 아래 구조물로 봄
        private const int MAX_WALL_HITS = 16;
        private const float GROUND_RAY_MARGIN = 0.1f;
        private const float MIN_GROUND_NORMAL_Y = 0.5f; // 이보다 가파른 면은 올라탈 바닥이 아님
        private const float LAND_SNAP_DISTANCE = 6f; // 착지 자리가 NavMesh 밖이면 이 거리 안의 가장 가까운 NavMesh 위로 옮김
        private const float STEP_PROBE_INSET = 0.1f; // 단차 윗면을 잴 때 부딪힌 면에서 안쪽으로 들어가는 거리

        [Title("Components")]
        [SerializeField] private Rigidbody body;

        [Title("Settings")]
        [SerializeField] private LayerMask wallMask; // 몸 전체 반경으로 막는 레이어 (벽, 기둥)
        [SerializeField] private float wallRadius = 3f; // 머리·팔·꼬리까지 덮는 몸의 수평 반경, 벽에 몸이 파고들지 않게 함
        [SerializeField] private float castHeight = 0.5f; // 보스 위치에서 판정 구 중심까지의 높이

        [Tooltip("켜면 NavMesh(BossPathfinder의 Agent Type) 밖으로 나가지 않음, 벽 없는 구멍·낭떠러지가 있는 맵에서 켬")]
        [SerializeField] private bool constrainToNavMesh;
        [Tooltip("도약 착지 자리를 NavMesh 경계에서 이만큼 안쪽으로 들임(m), 경계 바로 위에 서면 벌린 발 한쪽이 구멍·단차 밖으로 뜸")]
        [SerializeField, Min(0f)] private float landEdgePadding = 0.6f;

        [Title("Ground")]
        [Tooltip("발밑 높이를 따라갈 바닥 레이어, 비우면 높이를 바꾸지 않음")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("올라탈 수 있는 단차 높이(m), NavMesh Agent의 Step Height와 맞춤")]
        [SerializeField, Min(0f)] private float stepHeight = 1f;
        [Tooltip("내려갈 수 있는 높이(m), 이보다 깊으면(구멍 등) 지금 높이를 유지")]
        [SerializeField, Min(0f)] private float maxDrop = 1.5f;
        [Tooltip("바닥 높이를 따라가는 속도(m/s)")]
        [SerializeField, Min(0.01f)] private float heightSpeed = 6f;

        [Title("States")]
        [ShowInInspector, ReadOnly] private Vector3 pendingDelta;
        [ShowInInspector, ReadOnly] private float pendingYaw;
        [ShowInInspector, ReadOnly] private float groundHeight;
        [ShowInInspector, ReadOnly] private float lift;
        [ShowInInspector, ReadOnly] private bool isAirborne;

        private TimeFreezable clock;
        private BossPathfinder pathfinder;
        private readonly RaycastHit[] wallHits = new RaycastHit[MAX_WALL_HITS];

        // 막힌 물리 스텝마다 발생, 벽 충돌 그로기 같은 반응은 구독하는 상태가 판단
        public event Action<BossBlockInfo> OnBlocked;

        // 몸 전체의 수평 반경(벽 판정 반경)
        public float BodyRadius => wallRadius;

        // 아직 물리 스텝에 반영되지 않은 이동량까지 더한 위치
        // 남은 거리로 속도를 정하는 상태(Hop, JumpSmash)가 Body.position 대신 써야 착지점을 지나치지 않음
        public Vector3 Position => body.position + pendingDelta;

        // 아직 물리 스텝에 반영되지 않은 회전까지 더한 방향
        // 대상까지 남은 각도를 이 값으로 재야 이미 요청한 회전을 또 요청해 목표를 지나치지 않음
        public Quaternion Rotation => Quaternion.Euler(0f, pendingYaw, 0f) * body.rotation;

        // 도약 중(BeginAirborne~EndAirborne)이면 구멍·벽을 넘어가는 중이라 NavMesh 밖이어도 정상
        public bool IsAirborne => isAirborne;

        private bool CanSnapToNavMesh => constrainToNavMesh && pathfinder != null;

        // 몸체가 서 있는 바닥 높이, 쓰러진 몸을 들어 올린 높이(lift)는 빠짐
        public float GroundHeight => groundHeight;

        private void Awake()
        {
            clock = TimeFreezable.GetOrAdd(gameObject);
            pathfinder = GetComponent<BossPathfinder>();
            groundHeight = body.position.y;
        }

        // 배치 높이가 바닥과 어긋나 있으면 heightSpeed로 따라가느라 땅에서 솟아오르거나 가라앉으므로 시작할 때 바로 맞춤
        private void Start()
        {
            if (groundMask == 0) return;

            float target = float.NegativeInfinity;
            SampleGround(body.position, ref target);
            if (float.IsNegativeInfinity(target)) return;

            groundHeight = target;
            Vector3 position = body.position;
            position.y = groundHeight + lift;
            body.transform.position = position;
            body.position = position;
        }

        private void FixedUpdate()
        {
            if (clock.IsFrozen) return;

            if (pendingYaw != 0f)
            {
                body.MoveRotation(Rotation);
                pendingYaw = 0f;
            }

            Vector3 delta = pendingDelta;
            pendingDelta = Vector3.zero;

            // 도약 중에는 벽·구멍을 넘어가도록 막지 않음
            bool isFree = isAirborne && CanSnapToNavMesh;
            Vector3 moved = delta == Vector3.zero ? Vector3.zero : isFree ? delta : Sweep(delta);
            if (isFree == false && CanSnapToNavMesh && moved != Vector3.zero) moved = pathfinder.ClampToNavMesh(body.position, moved);

            Vector3 next = body.position + moved;
            UpdateGroundHeight(next, delta);
            next.y = groundHeight + lift;

            if ((next - body.position).sqrMagnitude > 0.0000001f) body.MovePosition(next);
        }

        #region PUBLIC_METHODS

        // 높이는 바닥(groundMask)과 애니메이션이 맡으므로 수평 이동만 받음
        public void Move(Vector3 delta)
        {
            delta.y = 0f;
            pendingDelta += delta;
        }

        // 도약을 시작하면 벽 판정과 NavMesh 제한을 끔
        public void BeginAirborne() => isAirborne = true;

        // 착지(또는 도약이 끊김)하면 제한을 다시 켜고, 구멍·벽 안이나 NavMesh 경계 바로 위에 내려앉았으면 경계에서 안쪽으로 들인 NavMesh 위로 옮김
        public void EndAirborne()
        {
            if (isAirborne == false) return;
            isAirborne = false;

            if (CanSnapToNavMesh == false) return;

            Vector3 position = Position;
            if (pathfinder.TryGetNearestOnNavMesh(position, LAND_SNAP_DISTANCE, landEdgePadding, out Vector3 landing) == false) return;

            Vector2 offset = new(landing.x - position.x, landing.z - position.z);
            if (offset.sqrMagnitude > 0.0025f) Teleport(landing, body.rotation);
        }

        // 바닥 높이 위로 몸체를 더 들어 올림, 쓰러진 몸을 더 높은 단차에 걸칠 때(BossLyingGroundFit) 씀
        public void SetLift(float value) => lift = value;

        // 수직축 회전만 받음, 양수면 오른쪽
        public void Rotate(float deltaYaw) => pendingYaw += deltaYaw;

        // 쌓인 이동량·회전이 순간이동한 자세를 덮어쓰지 않도록 비우고 옮김
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            pendingDelta = Vector3.zero;
            pendingYaw = 0f;

            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            groundHeight = position.y - lift; // 순간이동한 높이(착지점 바닥)에서 다시 따라감
        }

        #endregion

        // 지금 자리와, 움직이는 중이면 몸 안쪽 앞의 바닥 중 높은 쪽을 따라감(단차에 발이 먼저 닿으므로)
        // 몸 반경의 절반만 앞을 봐서, 벽 판정에 막힌 난간 윗면은 재지 않음
        private void UpdateGroundHeight(Vector3 position, Vector3 delta)
        {
            if (groundMask == 0) return;

            float target = float.NegativeInfinity;
            SampleGround(position, ref target);

            delta.y = 0f;
            if (delta.sqrMagnitude > 0.000001f) SampleGround(position + delta.normalized * (wallRadius * 0.5f), ref target);

            // 구멍 위처럼 바닥이 없으면 지금 높이 유지
            if (float.IsNegativeInfinity(target)) return;

            groundHeight = Mathf.MoveTowards(groundHeight, target, heightSpeed * clock.LocalFixedDeltaTime);
        }

        // 지금 바닥에서 stepHeight 위부터 maxDrop 아래까지, 위를 향한 면만 바닥으로 봄
        private void SampleGround(Vector3 position, ref float height)
        {
            Vector3 origin = new(position.x, groundHeight + stepHeight + GROUND_RAY_MARGIN, position.z);
            float distance = stepHeight + GROUND_RAY_MARGIN + maxDrop;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= MIN_GROUND_NORMAL_Y)
                height = Mathf.Max(height, hit.point.y);
        }

        // 막히면 벽 앞에서 멈추고, 남은 이동량은 벽면을 따라 미끄러지게 함
        private Vector3 Sweep(Vector3 delta)
        {
            // 한 물리 스텝 동안의 이동량 기준이라 프레임레이트가 물리보다 낮으면 실제보다 크게 잡힐 수 있음
            float speed = delta.magnitude / Time.fixedDeltaTime;

            Vector3 remain = delta;
            Vector3 result = Vector3.zero;

            for (int i = 0; i < MAX_SLIDE_COUNT; i++)
            {
                float distance = remain.magnitude;
                if (distance <= 0.0001f) break;

                Vector3 dir = remain / distance;

                if (CastWall(body.position + result, dir, distance + SKIN_WIDTH, out RaycastHit hit) == false)
                {
                    result += remain;
                    break;
                }

                float movable = Mathf.Max(0f, hit.distance - SKIN_WIDTH);
                result += dir * movable;

                OnBlocked?.Invoke(new BossBlockInfo(hit, dir, speed));

                remain = Vector3.ProjectOnPlane(remain - dir * movable, hit.normal);
                remain.y = 0f;
            }

            return result;
        }

        // 몸 전체 반경의 구로 판정해 머리·팔·꼬리가 벽에 파고들지 않게 함
        // 구라서 회전해도 모양이 같아 회전은 검사하지 않음, 시작할 때 이미 겹친 콜라이더는 감지하지 못함
        // 구가 바닥 아래까지 내려가므로 발보다 낮은 곳에 닿은 구조물(아래층 벽, 바닥 받침)은 막지 않음
        private bool CastWall(Vector3 position, Vector3 dir, float distance, out RaycastHit hit)
        {
            hit = default;
            if (wallMask == 0) return false;

            Vector3 origin = position + Vector3.up * castHeight;
            int count = Physics.SphereCastNonAlloc(origin, wallRadius, dir, wallHits, distance, wallMask, QueryTriggerInteraction.Ignore);
            float floorY = position.y - BELOW_FEET_MARGIN;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = wallHits[i];

                // 시작할 때 이미 겹친 콜라이더는 거리 0, 위치 0으로 오므로 SphereCast처럼 무시
                if (candidate.distance <= 0f && candidate.point == Vector3.zero) continue;
                if (candidate.point.y < floorY) continue;
                if (IsClimbableStep(candidate, position.y)) continue;
                if (found && candidate.distance >= hit.distance) continue;

                hit = candidate;
                found = true;
            }

            return found;
        }

        // 부딪힌 면 바로 안쪽을 위에서 내려다봐, 발보다 높고 stepHeight 이내인 위를 향한 면이 있으면 올라탈 단차
        // 난간·벽처럼 그보다 높으면 레이가 윗면에 닿지 않거나 너무 높아 벽으로 막음
        private bool IsClimbableStep(RaycastHit contact, float feetY)
        {
            if (groundMask == 0) return false;

            Vector3 inside = contact.point - new Vector3(contact.normal.x, 0f, contact.normal.z).normalized * STEP_PROBE_INSET;
            Vector3 origin = new(inside.x, feetY + stepHeight + GROUND_RAY_MARGIN, inside.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit top, stepHeight + GROUND_RAY_MARGIN, groundMask, QueryTriggerInteraction.Ignore) == false)
                return false;

            // 시작점이 이미 더 높은 벽 안이면 그 벽은 맞지 않으므로 거리 0 근처 결과는 믿지 않음
            return top.distance > 0.01f && top.normal.y >= MIN_GROUND_NORMAL_Y;
        }

        #region CAST_VISUALIZATION

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Color originalColor = UnityEditor.Handles.color;

            Vector3 center = transform.position + Vector3.up * castHeight;

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, wallRadius);

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}
