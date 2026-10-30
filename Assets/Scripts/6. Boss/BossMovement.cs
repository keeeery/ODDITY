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
    public class BossMovement : MonoBehaviour
    {
        private const float SKIN_WIDTH = 0.05f;
        private const int MAX_SLIDE_COUNT = 2;
        private const float BELOW_FEET_MARGIN = 0.1f; // 발 높이보다 이만큼 더 낮은 접촉은 바닥 아래 구조물로 봄
        private const int MAX_WALL_HITS = 16;

        [Title("Components")]
        [SerializeField] private Rigidbody body;

        [Title("Settings")]
        [SerializeField] private LayerMask wallMask; // 몸 전체 반경으로 막는 레이어 (벽, 기둥)
        [SerializeField] private float wallRadius = 3f; // 머리·팔·꼬리까지 덮는 몸의 수평 반경, 벽에 몸이 파고들지 않게 함
        [SerializeField] private float castHeight = 0.5f; // 보스 위치에서 판정 구 중심까지의 높이

        [Title("States")]
        [ShowInInspector, ReadOnly] private Vector3 pendingDelta;
        [ShowInInspector, ReadOnly] private float pendingYaw;

        private TimeFreezable clock;
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

        private void Awake()
        {
            clock = TimeFreezable.GetOrAdd(gameObject);
        }

        private void FixedUpdate()
        {
            if (clock.IsFrozen) return;

            if (pendingYaw != 0f)
            {
                body.MoveRotation(Rotation);
                pendingYaw = 0f;
            }

            if (pendingDelta == Vector3.zero) return;

            Vector3 delta = pendingDelta;
            pendingDelta = Vector3.zero;

            body.MovePosition(body.position + Sweep(delta));
        }

        #region PUBLIC_METHODS

        // 높이는 애니메이션이 맡으므로 수평 이동만 받음
        public void Move(Vector3 delta)
        {
            delta.y = 0f;
            pendingDelta += delta;
        }

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
        }

        #endregion

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
                if (found && candidate.distance >= hit.distance) continue;

                hit = candidate;
                found = true;
            }

            return found;
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
