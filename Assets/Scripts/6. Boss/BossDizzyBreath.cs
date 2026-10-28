using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 지쳐 무너진 동안 입에서 거칠게 뿜어 나오는 숨, BossDizzy Slump 겹의 가중치를 따라 세지고 잦아든다.
    // 가중치가 처음 오르는 순간 크게 한 번 콧김을 내뿜고, 그 뒤로는 Slump의 숨 들썩임에서 가장 깊이 들이쉰 순간마다 내뿜는다.
    // 입 뼈가 없어 입 안의 혀 뼈를 기준으로 위치를 정하고, 처음 자세에서 그 뼈 공간으로 옮겨 두어 고개를 끄덕이거나 흔들면 숨 방향도 따라간다.
    // 보스 시간(TimeFreezable)을 따라 시간 정지 중에는 숨도 멈춘다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossDizzy))]
    [DefaultExecutionOrder(10)] // BossDizzy가 뼈를 흔든 뒤에 머리 자세를 읽음
    public sealed class BossDizzyBreath : MonoBehaviour
    {
        [Title("Breath")]
        [Tooltip("TeamOdd/Ground Dust 셰이더의 숨 머티리얼(밝은 색), 비워 두면 뿜지 않는다.")]
        [SerializeField] private Material breathMaterial;
        [Tooltip("입 위치의 기준 뼈, 비워 두면 이름에 \"Tongue\"이 든 가운데 혀 뼈 중 정면으로 가장 앞에 있는 뼈를, 없으면 \" Head\"로 끝나는 뼈를 찾는다.")]
        [SerializeField] private Transform mouth;
        [Tooltip("기준 뼈에서 입 앞 가운데까지의 거리(m), 보스 기준 x: 오른쪽, y: 위, z: 앞. Scene 뷰의 기즈모로 맞춘다.")]
        [SerializeField] private Vector3 mouthOffset = new(0f, 0.1f, 0.15f);
        [Tooltip("숨이 두 갈래로 나오는 사이 거리(m), 0이면 한 줄기")]
        [SerializeField, Min(0f)] private float mouthWidth = 0.2f;
        [Tooltip("숨이 보스 정면에서 아래로 기우는 각도(도)")]
        [SerializeField, Range(-30f, 80f)] private float downAngle = 35f;
        [Tooltip("두 갈래가 바깥으로 벌어지는 각도(도)")]
        [SerializeField, Range(0f, 45f)] private float splayAngle = 15f;

        [Title("Rhythm")]
        [Tooltip("켜면 BossDizzy Slump의 숨 들썩임에 맞춰 가장 깊이 들이쉰 순간 내뿜고, 끄면 아래 간격마다 내뿜는다.")]
        [SerializeField] private bool syncWithHeave = true;
        [Tooltip("숨 사이 간격(초), x: 최소, y: 최대. syncWithHeave를 끈 경우에만 쓴다.")]
        [SerializeField] private Vector2 breathInterval = new(1.1f, 1.6f);
        [Tooltip("한 번 내쉬는 동안 콧김이 나오는 시간(초)")]
        [SerializeField, Min(0.05f)] private float exhaleDuration = 0.35f;
        [Tooltip("BossDizzy Slump 가중치가 이 값을 넘는 동안만 숨을 몰아쉰다.")]
        [SerializeField, Range(0f, 1f)] private float weightThreshold = 0.2f;

        [Title("Puff")]
        [Tooltip("한 번 내쉴 때 한 갈래에서 나오는 입자 수")]
        [SerializeField, Min(1)] private int puffsPerNostril = 4;
        [Tooltip("뿜어 나가는 속도(m/s), x: 최소, y: 최대. 끌림이 있어 빠르게 멈춘다.")]
        [SerializeField] private Vector2 puffSpeed = new(2.5f, 4f);
        [Tooltip("콧김이 퍼지는 정도, 속도 대비 옆으로 흩어지는 비율")]
        [SerializeField, Range(0f, 1f)] private float puffSpread = 0.3f;
        [SerializeField] private Vector2 puffSize = new(0.25f, 0.45f);
        [SerializeField] private Vector2 puffLifetime = new(0.8f, 1.3f);
        [SerializeField, Range(0f, 1f)] private float puffOpacity = 0.35f;

        [Title("Snort")]
        [Tooltip("가중치가 처음 오를 때 내뿜는 큰 콧김의 입자 수·속도 배율")]
        [SerializeField, Min(1f)] private float snortCountScale = 2.5f;
        [SerializeField, Min(1f)] private float snortSpeedScale = 1.6f;
        [SerializeField, Min(1f)] private float snortSizeScale = 1.3f;

        private BossDizzy dizzy;
        private TimeFreezable clock;
        private GroundDust breath;

        private Vector3 localOffset;   // 기준 뼈 공간의 입 앞 가운데
        private Vector3 localForward;  // 기준 뼈 공간의 보스 정면
        private Vector3 localUp;
        private bool hasLocalFrame;

        private bool isBreathing;
        private float nextBreathTime;
        private float exhaleStartTime = float.NegativeInfinity;
        private float exhaleScale = 1f;
        private float emitted;
        private float lastHeave;
        private bool wasInhaling;

        private void Awake()
        {
            dizzy = GetComponent<BossDizzy>();
            if (mouth == null) mouth = FindMouth();
            CacheLocalFrame();
        }

        private void Start()
        {
            BossMonster boss = GetComponent<BossMonster>();
            clock = boss != null ? boss.Clock : TimeFreezable.GetOrAdd(gameObject);
            clock.FrozenChanged += HandleFrozenChanged;
        }

        private void OnDestroy()
        {
            if (clock != null) clock.FrozenChanged -= HandleFrozenChanged;
            if (breath != null) Destroy(breath.gameObject);
        }

        private void LateUpdate()
        {
            if (mouth == null || breathMaterial == null) return;

            float now = clock != null ? clock.LocalTime : Time.time;
            bool shouldBreathe = dizzy.Slump.Weight > weightThreshold;

            // 막 무너지기 시작하면 크게 한 번 내뿜음
            if (shouldBreathe && isBreathing == false) StartExhale(now, snortCountScale);
            isBreathing = shouldBreathe;

            if (isBreathing && IsBreathTime(now)) StartExhale(now, 1f);

            UpdateExhale(now);
        }

        // 들썩임이 올라가다 내려가기 시작한 순간(가장 깊이 들이쉰 순간), 앞 콧김과 너무 붙으면 건너뜀
        private bool IsBreathTime(float now)
        {
            if (syncWithHeave == false) return now >= nextBreathTime;

            float heave = dizzy.SlumpBreath;
            bool isInhaling = heave > lastHeave;
            bool isPeak = wasInhaling && isInhaling == false;
            lastHeave = heave;
            wasInhaling = isInhaling;

            return isPeak && now - exhaleStartTime >= exhaleDuration * 2f;
        }

        private void StartExhale(float now, float scale)
        {
            exhaleStartTime = now;
            exhaleScale = scale;
            emitted = 0f;
            nextBreathTime = now + Random.Range(breathInterval.x, breathInterval.y);
        }

        // 내쉬는 시간 동안 나눠서 뿜고, 처음이 가장 세고 끝으로 갈수록 약해짐
        private void UpdateExhale(float now)
        {
            float progress = (now - exhaleStartTime) / exhaleDuration;
            if (progress < 0f || progress > 1f) return;

            int total = Mathf.RoundToInt(puffsPerNostril * exhaleScale);
            float target = total * Mathf.Sqrt(progress);
            if (target - emitted < 1f) return;

            GroundDust system = GetBreath();
            if (system == null) return;

            bool isSnort = exhaleScale > 1f;
            float strength = Mathf.Lerp(1f, 0.5f, progress) * Mathf.Lerp(0.6f, 1f, dizzy.Slump.Weight);

            while (target - emitted >= 1f)
            {
                emitted += 1f;
                EmitPair(system, strength, isSnort);
            }
        }

        private void EmitPair(GroundDust system, float strength, bool isSnort)
        {
            GetMouth(out Vector3 center, out Vector3 forward, out Vector3 up);
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 down = Quaternion.AngleAxis(downAngle, right) * forward;

            float speedScale = isSnort ? snortSpeedScale : 1f;
            float sizeScale = isSnort ? snortSizeScale : 1f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = center + right * (side * mouthWidth * 0.5f);
                Vector3 direction = Quaternion.AngleAxis(side * splayAngle, up) * down;

                float speed = Random.Range(puffSpeed.x, puffSpeed.y) * speedScale * strength;
                Vector3 velocity = direction * speed + Random.insideUnitSphere * (speed * puffSpread);

                system.Emit(position, velocity, Random.Range(puffSize.x, puffSize.y) * sizeScale,
                    Random.Range(puffLifetime.x, puffLifetime.y), puffOpacity * Mathf.Clamp01(strength + 0.2f));
            }
        }

        // 뼈의 축 방향은 모델마다 다르므로, 처음 자세에서 보스 기준 오프셋·정면을 기준 뼈 공간으로 옮겨 둠
        private void CacheLocalFrame()
        {
            if (mouth == null) return;

            Quaternion toMouth = Quaternion.Inverse(mouth.rotation);
            localOffset = toMouth * (transform.rotation * mouthOffset);
            localForward = toMouth * transform.forward;
            localUp = toMouth * transform.up;
            hasLocalFrame = true;
        }

        private void GetMouth(out Vector3 center, out Vector3 forward, out Vector3 up)
        {
            if (hasLocalFrame == false) CacheLocalFrame();

            center = mouth.position + mouth.rotation * localOffset;
            forward = mouth.rotation * localForward;
            up = mouth.rotation * localUp;
        }

        private void HandleFrozenChanged(bool frozen)
        {
            if (breath != null) breath.SetPaused(frozen);
        }

        private GroundDust GetBreath()
        {
            if (breath == null && breathMaterial != null)
            {
                breath = GroundDust.Create(breathMaterial, $"{name} Dizzy Breath");
                if (clock != null && clock.IsFrozen) breath.SetPaused(true);
            }
            return breath;
        }

        // 입 안의 혀 뼈 중 좌우 갈래(Left·Right)를 뺀 가운데 뼈에서 보스 정면으로 가장 앞에 있는 뼈, 혀가 없으면 머리 뼈
        private Transform FindMouth()
        {
            Transform best = null;
            Transform head = null;
            float bestForward = float.NegativeInfinity;

            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (head == null && bone.name.EndsWith(" Head")) head = bone;
                if (bone.name.Contains("Tongue") == false || bone.name.Contains("Left") || bone.name.Contains("Right")) continue;

                float forward = Vector3.Dot(bone.position - transform.position, transform.forward);
                if (forward <= bestForward) continue;

                bestForward = forward;
                best = bone;
            }

            if (best != null) return best;
            if (head != null) return head;

            Debug.LogWarning($"[{nameof(BossDizzyBreath)}] {name}에서 혀 뼈(\"Tongue\")나 머리 뼈(\" Head\")를 찾지 못했습니다. Mouth를 직접 지정하세요.", this);
            return null;
        }

        #region BREATH_VISUALIZATION

#if UNITY_EDITOR
        // 플레이 중이 아니면 지금 자세를 처음 자세로 보고 입 위치·숨 방향을 보여 줌
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false)
            {
                if (mouth == null) mouth = FindMouth();
                hasLocalFrame = false;
            }
            if (mouth == null) return;

            GetMouth(out Vector3 center, out Vector3 forward, out Vector3 up);
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 down = Quaternion.AngleAxis(downAngle, right) * forward;

            Color originalColor = Gizmos.color;
            Gizmos.color = Color.cyan;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = center + right * (side * mouthWidth * 0.5f);
                Vector3 direction = Quaternion.AngleAxis(side * splayAngle, up) * down;
                Gizmos.DrawWireSphere(position, 0.06f);
                Gizmos.DrawLine(position, position + direction * 0.8f);
            }

            Gizmos.color = originalColor;
        }
#endif

        #endregion
    }
}
