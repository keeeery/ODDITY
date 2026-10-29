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
        [SerializeField] private float sprintDistance = 4f;
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

        [Header("흔들림 (trauma)")]
        [Tooltip("trauma 1일 때 위치 흔들림 최대 세기(m). 실제 세기는 trauma²")]
        [SerializeField] private Vector3 maxShakePosition = new(0.3f, 0.3f, 0f);
        [Tooltip("trauma 1일 때 회전 흔들림 최대 각도(도): pitch, yaw, roll")]
        [SerializeField] private Vector3 maxShakeRotation = new(3f, 1.5f, 5f);
        [Tooltip("Perlin 노이즈 주파수(Hz). 높을수록 잘게 떨린다")]
        [SerializeField, Min(0.1f)] private float shakeFrequency = 25f;
        [Tooltip("기본 trauma 감쇠 속도(초당). 지속 시간을 따로 주면 그 값이 우선")]
        [SerializeField, Min(0f)] private float traumaDecay = 1.7f;

        private float trauma;          // 0~1
        private float currentDecay;
        private float noiseTime;

        private float yaw;   // 좌우 회전 누적값
        private float pitch; // 상하 회전 누적값

        private bool isHeld;

        private void Start()
        {
           target = Player.Player.Instance;
           UIManager.Instance.LockCursor(true);
           target.CameraController = this;
           yaw = target.transform.eulerAngles.y;

           currentOffset = pivotOffset;
           currentDistance = distance;
           
           DOTween.defaultUpdateType = UpdateType.Normal;
        }

        private void Update()
        {
            isHeld = target.PlayerController.HasHoldTarget;

            if (SimpleInput.GetKeyDown(Key.Digit7))
                Shake(new Vector3(0.3f, 0f, 0f), 0.3f);   // 세기 0.3m, 0.3초
        }

        // 충격을 trauma로 쌓음, fadeTime > 0이면 이번 충격이 그 시간 안에 사라지도록 감쇠 속도 결정
        public void AddTrauma(float amount, float fadeTime = 0f)
        {
            trauma = Mathf.Clamp01(trauma + amount);
            currentDecay = fadeTime > 0f ? trauma / fadeTime : traumaDecay;
        }

        // 기존 호출 호환용: strength 크기를 maxShakePosition 대비 trauma로 바꾼다. vibrato/elasticity는 사용하지 않음
        public void Shake(Vector3 strength, float duration, int vibrato = 10, float elasticity = 1f)
        {
            float reference = Mathf.Max(maxShakePosition.magnitude, 0.0001f);
            AddTrauma(strength.magnitude / reference, duration);
        }

        // trauma²로 위치/회전 흔들림을 계산한다.
        private void UpdateTrauma(out Vector3 positionOffset, out Quaternion rotationOffset)
        {
            positionOffset = Vector3.zero;
            rotationOffset = Quaternion.identity;
            if (trauma <= 0f) return;

            float power = trauma * trauma;
            noiseTime += Time.deltaTime * shakeFrequency;

            positionOffset = new Vector3(
                Noise(0, noiseTime) * maxShakePosition.x,
                Noise(1, noiseTime) * maxShakePosition.y,
                Noise(2, noiseTime) * maxShakePosition.z) * power;
            rotationOffset = Quaternion.Euler(
                Noise(3, noiseTime) * maxShakeRotation.x * power,
                Noise(4, noiseTime) * maxShakeRotation.y * power,
                Noise(5, noiseTime) * maxShakeRotation.z * power);

            trauma = Mathf.Max(0f, trauma - currentDecay * Time.deltaTime);
        }

        // 축마다 시드를 달리한 -1~1 Perlin 노이즈
        private static float Noise(int seed, float t) => Mathf.PerlinNoise(seed * 17.31f, t) * 2f - 1f;

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateRotation();
            Vector3 basePosition = UpdatePosition();
            UpdateTrauma(out Vector3 posOffset, out Quaternion rotOffset);

            // 흔들림은 렌더 직전에만 더한다. yaw/pitch 누적값은 건드리지 않음
            Vector3 shake = transform.rotation * posOffset;
            float shakeLength = shake.magnitude;
            if (shakeLength > 0.0001f && Physics.SphereCast(basePosition, collisionRadius, shake / shakeLength,
                    out RaycastHit hit, shakeLength, collisionMask, QueryTriggerInteraction.Ignore))
            {
                shake = shake / shakeLength * hit.distance;
            }

            transform.position = basePosition + shake;
            transform.rotation *= rotOffset;
        }

        private void UpdateRotation()
        {
            const float MOUSE_AXIS_SCALE = 0.1f;
            Vector2 delta = SimpleInput.GetMouseDelta() * MOUSE_AXIS_SCALE;

            yaw += delta.x * mouseSensitivity;
            pitch -= delta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // if (isHeld)
            //     yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
            // 이거 아마 팔 뻗고 부자연스럽게 회전하는 거 막으려는 조치로 해두신 듯?
            // 그 부분은 해결했으니 위에 주석 처리한 거 아예 지워버려도 될 것 같고
            // 오히려 x축 회전 하한선 조정해야 할 듯, 카메라 아래로 쭉 당기면 플레이어 관통돼서 보임

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private Vector3 UpdatePosition()
        {
            var (targetOffset, targetDistance, speed) = GetCameraTarget();

            // 프레임 독립 감쇠: FPS와 관계없이 같은 속도로 수렴합니다.
            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, t);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, t);
            
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
        
        private (Vector3 offset, float distance, float speed) GetCameraTarget()
        {
            if (isHeld)
                return (holdPivotOffset, holdDistance, transitionSpeed);

            if (target.PlayerController.IsSprinting)
                return (pivotOffset, distance + sprintDistance, transitionSpeed);
            
            if (isHeld && target.PlayerController.IsSprinting)
                return (holdPivotOffset, distance + sprintDistance, transitionSpeed);

            return (pivotOffset, distance, transitionSpeed);
        }
    }
}
