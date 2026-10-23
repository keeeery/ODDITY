using DG.Tweening;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.CameraMove
{
    public class CameraController : MonoBehaviour
    {
        
        [SerializeField] private Player.Player target;
        
        [Header("위치")]
        [Tooltip("피벗 기준 카메라 오프셋. x를 주면 어깨 너머 시점이 됩니다")]
        [SerializeField] private Vector3 shoulderOffset = new(0.5f, 0f, 0f);
        [Tooltip("target 위치에서 공전 중심(피벗)까지의 오프셋. 보통 머리 높이")]
        [SerializeField] private Vector3 pivotOffset = new(0f, 1.6f, 0f);
        [SerializeField] private Vector3 holdPivotOffset = new(0f, 1.6f, 0f);
        [SerializeField] private float distance = 4f;
        [SerializeField] private float holdDistance = 4f;
        [SerializeField] private float transitionSpeed = 10f;
        private Vector3 currentOffset;
        private float currentDistance; 

        [Header("회전")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float minYaw = -40f;
        [SerializeField] private float maxYaw = 70f;

        [Header("충돌")]
        [Tooltip("카메라가 막힐 레이어")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.2f;

        [SerializeField] private Vector3 shakeOffset;

        private float yaw;   // 좌우 회전 누적값
        private float pitch; // 상하 회전 누적값

        private bool mouseRightClick;

        private void Start()
        {
           target = Player.Player.Instance;
           target.LockCursor(true);
           target.CameraController = this;
           yaw = target.transform.eulerAngles.y;

           currentOffset = pivotOffset;
           currentDistance = distance;
           
           DOTween.defaultUpdateType = UpdateType.Normal;
        }

        private void Update()
        {
            mouseRightClick = SimpleInput.GetMouseButton(SimpleInput.MouseButton.Right);

            if (SimpleInput.GetKeyDown(Key.Digit7))
            {
                DOTween.Kill(this); // 연타하면 이전 흔들림을 끊고 새로 시작합니다
                shakeOffset = Vector3.zero;
                DOTween.Punch(() => shakeOffset, x => shakeOffset = x,
                        new Vector3(0.3f, 0f, 0f), 0.3f)   // 세기 0.3m, 0.3초
                    .SetTarget(this);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateRotation();
            transform.position = UpdatePosition() + transform.rotation * shakeOffset;

        }

        private void UpdateRotation()
        {
            const float MOUSE_AXIS_SCALE = 0.1f;
            Vector2 delta = SimpleInput.GetMouseDelta() * MOUSE_AXIS_SCALE;

            yaw += delta.x * mouseSensitivity;
            pitch -= delta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (mouseRightClick)
                yaw = Mathf.Clamp(yaw, minYaw, maxYaw);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private Vector3 UpdatePosition()
        {
            Vector3 targetOffset = mouseRightClick ? holdPivotOffset : pivotOffset;
            float targetDistance = mouseRightClick ? holdDistance : distance;
            
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, transitionSpeed * Time.deltaTime);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, transitionSpeed * Time.deltaTime);
            
            Vector3 pivot = target.transform.position + currentOffset;
            // 피벗에서 회전 방향의 뒤쪽(distance)으로 물러난 곳이 카메라가 있고 싶은 위치 입니다.
            Vector3 desiredOffset = transform.rotation * (shoulderOffset + Vector3.back * currentDistance);

            float length = desiredOffset.magnitude;
            
            if (length < 0.0001f) // distance와 오프셋이 모두 0이면 피벗 위치에 둡니다.
            {
                return pivot;
            }
            Vector3 direction = desiredOffset / length;

            // 피벗 -> 카메라 사이에 벽이 있으면 벽 앞까지만 물러납니다.
            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, length,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                length = hit.distance;
            }

            return pivot + direction * length;
        }
    }
}
