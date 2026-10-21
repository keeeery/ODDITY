using TeamOdd.Player;
using Study.Utilities;
using UnityEngine;

namespace TeamOdd.CameraMove
{
    public class CameraController : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private Transform target;
        [Tooltip("target 위치에서 공전 중심(피벗)까지의 오프셋. 보통 머리 높이")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
        [Tooltip("피벗 기준 카메라 오프셋. x를 주면 어깨 너머 시점이 됩니다")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.5f, 0f, 0f);
        [SerializeField] private float distance = 4f;

        [Header("회전")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 70f;

        [Header("충돌")]
        [Tooltip("카메라가 막힐 레이어")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.2f;

        private float yaw;   // 좌우 회전 누적값
        private float pitch; // 상하 회전 누적값

        private void Start()
        {
           // LockCursor();
           target = Player.Player.Instance.transform;
            yaw = target.eulerAngles.y; // 시작할 때 캐릭터 뒤에서 출발합니다.
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateRotation();
            UpdatePosition();
        }

        private void UpdateRotation()
        {
            const float MOUSE_AXIS_SCALE = 0.1f;
            Vector2 delta = SimpleInput.GetMouseDelta() * MOUSE_AXIS_SCALE;

            yaw += delta.x * mouseSensitivity;
            pitch -= delta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdatePosition()
        {
            Vector3 pivot = target.position + pivotOffset;
            // 피벗에서 회전 방향의 뒤쪽(distance)으로 물러난 곳이 카메라가 있고 싶은 위치 입니다.
            Vector3 desiredOffset = transform.rotation * (shoulderOffset + Vector3.back * distance);

            float length = desiredOffset.magnitude;
            if (length < 0.0001f) // distance와 오프셋이 모두 0이면 피벗 위치에 둡니다.
            {
                transform.position = pivot;
                return;
            }
            Vector3 direction = desiredOffset / length;

            // 피벗 -> 카메라 사이에 벽이 있으면 벽 앞까지만 물러납니다.
            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, length,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                length = hit.distance;
            }

            transform.position = pivot + direction * length;
        }

        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
