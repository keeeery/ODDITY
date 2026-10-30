using DG.Tweening;
using Study.Utilities;
using TeamOdd.Combat;
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
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float minYaw = -40f;
        [SerializeField] private float maxYaw = 70f;

        [Header("대상 고정 (염동 내려찍기 등)")]
        [Tooltip("대상이 화면 중앙에 오도록 시점을 돌리는 빠르기, 클수록 빨리 맞춰진다. 거리는 홀딩 줌인 값을 그대로 쓴다")]
        [SerializeField, Min(0.1f)] private float focusTurnSpeed = 8f;

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

        private Camera viewCamera;
        private float baseFov;
        private float fovPunch;        // 기본 FOV에서 좁힌 각도(도)
        private Sequence fovPunchSequence;
        private float extraDistance;   // 연출로 더 물러난 거리(m)
        private Sequence pullBackSequence;

        private float trauma;          // 0~1
        private float currentDecay;
        private float noiseTime;

        private float yaw;   // 좌우 회전 누적값
        private float pitch; // 상하 회전 누적값

        private bool isHeld;
        private Transform focusTarget;     // 지정되면 마우스 대신 이 대상이 화면 중앙에 오도록 시점을 돌림
        private Vector3 lastBasePosition;  // 지난 프레임의 흔들림 전 카메라 위치, 대상을 볼 각도 계산에 씀

        public bool IsCinematicPlaying => fovPunchSequence != null || pullBackSequence != null;

        // 대상을 화면 중앙에 두는 고정 시점을 켬
        public void BeginFocus(Transform focus) => focusTarget = focus;

        // 다른 쪽이 그사이 대상을 바꿨으면 그쪽을 유지
        public void EndFocus(Transform focus)
        {
            if (focusTarget == focus) focusTarget = null;
        }

        private void Awake()
        {
            viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera != null) baseFov = viewCamera.fieldOfView;
        }

        private void OnDestroy()
        {
            fovPunchSequence?.Kill();
            pullBackSequence?.Kill();
        }

        private void Start()
        {
           target = Player.Player.Instance;
           UIManager.Instance.LockCursor(true);
           target.CameraController = this;
           yaw = target.transform.eulerAngles.y;

           currentOffset = pivotOffset;
           currentDistance = distance;
           lastBasePosition = transform.position;

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

        // FOV를 amount(도)만큼 zoomIn 동안 좁혀 hold 동안 머문 뒤 release 동안 되돌림, 시간은 실제 초라 히트스탑·슬로모션과 무관
        // 새 요청은 진행 중인 줌을 이어받아 지금 각도에서 다시 시작
        public void PunchFov(float amount, float zoomIn, float hold, float release)
        {
            if (viewCamera == null) return;

            fovPunchSequence?.Kill();
            fovPunchSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Append(DOTween.To(() => fovPunch, value => fovPunch = value, amount, zoomIn).SetEase(Ease.OutCubic))
                .AppendInterval(hold)
                .Append(DOTween.To(() => fovPunch, value => fovPunch = value, 0f, release).SetEase(Ease.InOutSine))
                .OnKill(() => fovPunchSequence = null);
        }

        // 카메라를 amount(m)만큼 pullOut 동안 천천히 더 물렸다가 hold 뒤 release 동안 되돌림(보스 사망 여운 등), 시간은 실제 초
        // 벽 충돌은 평소처럼 적용되고, 새 요청은 지금 거리에서 다시 시작
        public void PullBack(float amount, float pullOut, float hold, float release)
        {
            pullBackSequence?.Kill();
            pullBackSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Append(DOTween.To(() => extraDistance, value => extraDistance = value, amount, pullOut).SetEase(Ease.InOutSine))
                .AppendInterval(hold)
                .Append(DOTween.To(() => extraDistance, value => extraDistance = value, 0f, release).SetEase(Ease.InOutSine))
                .OnKill(() => pullBackSequence = null);
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
            noiseTime += Time.unscaledDeltaTime * shakeFrequency;

            positionOffset = new Vector3(
                Noise(0, noiseTime) * maxShakePosition.x,
                Noise(1, noiseTime) * maxShakePosition.y,
                Noise(2, noiseTime) * maxShakePosition.z) * power;
            rotationOffset = Quaternion.Euler(
                Noise(3, noiseTime) * maxShakeRotation.x * power,
                Noise(4, noiseTime) * maxShakeRotation.y * power,
                Noise(5, noiseTime) * maxShakeRotation.z * power);

            trauma = Mathf.Max(0f, trauma - currentDecay * Time.unscaledDeltaTime);
        }

        // 축마다 시드를 달리한 -1~1 Perlin 노이즈
        private static float Noise(int seed, float t) => Mathf.PerlinNoise(seed * 17.31f, t) * 2f - 1f;

        private void LateUpdate()
        {
            if (target == null || UIManager.Instance.IsUIClose == false) return;

            UpdateRotation();
            Vector3 basePosition = UpdatePosition();
            lastBasePosition = basePosition;
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

            if (viewCamera != null) viewCamera.fieldOfView = baseFov - fovPunch;
        }

        private void UpdateRotation()
        {
            const float MOUSE_AXIS_SCALE = 0.1f;

            // 흔들림이 매 프레임 기준 회전 위에 얹히므로 시점을 고정해도 회전 적용은 계속함
            if (focusTarget != null)
            {
                AimAtFocus();
            }
            else
            {
                // 스킬 시전으로 행동이 잠긴 동안은 마우스로 시점을 돌리지 않음
                // 마우스 이동량은 시간 배율과 무관하므로 슬로모션 중에는 플레이어 배율만큼 직접 느리게 돌림
                Vector2 delta = target.PlayerController.IsActionLocked ? Vector2.zero : SimpleInput.GetMouseDelta() * (MOUSE_AXIS_SCALE * SlowMotion.PlayerScale);

                yaw += delta.x * mouseSensitivity;
                pitch -= delta.y * mouseSensitivity;
            }

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // 지난 프레임 카메라 위치에서 대상을 보는 각도로 서서히 돌림
        // 카메라 위치가 회전을 따라 공전해도 몇 프레임 안에 대상이 화면 중앙에 맞춰지고, yaw/pitch를 같이 바꾸므로 풀었을 때 시점이 튀지 않음
        private void AimAtFocus()
        {
            Vector3 toFocus = focusTarget.position - lastBasePosition;
            if (toFocus.sqrMagnitude < 0.0001f) return;

            Vector3 look = Quaternion.LookRotation(toFocus).eulerAngles;
            float t = 1f - Mathf.Exp(-focusTurnSpeed * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, look.y, t);
            pitch = Mathf.LerpAngle(pitch, Mathf.DeltaAngle(0f, look.x), t);
        }

        private Vector3 UpdatePosition()
        {
            var (targetOffset, targetDistance, speed) = GetCameraTarget();

            // 프레임 독립 감쇠: FPS와 관계없이 같은 속도로 수렴합니다.
            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, t);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance + extraDistance, t);
            
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
