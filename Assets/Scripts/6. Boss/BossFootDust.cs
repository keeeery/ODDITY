using System;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 발밑 흙먼지: 점프·홉 착지 순간(OnJumpLand·OnHopLand) 두 발에서 고리 모양으로 퍼지고, 바닥에 깔린 충격파가 밀려 나가며,
    // 흙 파편이 튀고, 큰 흙먼지가 피어올라 한동안 옅게 남는다.
    // 돌진 중(isCharging)에는 달린 거리마다 발을 번갈아 뒤쪽으로 차올린다. 준비 중 제자리 달리기는 움직이지 않으므로 먼지가 없다.
    // 먼지는 발 아래 땅 표면에서 일어나고, 땅이 멀면(공중) 생략한다. 시간 정지 중에는 먼지도 멈춘다.
    [DisallowMultipleComponent]
    public sealed class BossFootDust : MonoBehaviour
    {
        [Title("Dust")]
        [Tooltip("TeamOdd/Ground Dust 머티리얼, 비워 두면 먼지를 뿜지 않는다.")]
        [SerializeField] private Material dustMaterial;
        [Tooltip("먼지가 일어날 발, 비워 두면 이름이 \"L Foot\"·\"R Foot\"으로 끝나는 뼈를 찾는다.")]
        [SerializeField] private Transform[] feet;
        [Tooltip("땅으로 볼 레이어")]
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [Tooltip("발에서 이 거리(m) 안에 땅이 있어야 먼지를 일으킨다.")]
        [SerializeField, Min(0.1f)] private float groundCheckDistance = 1.5f;

        [Title("Landing")]
        [Tooltip("발 하나에서 고리 모양으로 퍼지는 먼지 수")]
        [SerializeField, Min(0)] private int landingCountPerFoot = 10;
        [Tooltip("두 발 사이에서 천천히 피어오르는 큰 먼지 수")]
        [SerializeField, Min(0)] private int landingCenterCount = 4;
        [SerializeField, Min(0f)] private float landingRadius = 0.6f;
        [Tooltip("퍼지는 속도(m/s), x: 최소, y: 최대. 끌림이 있어 빠르게 감속한다.")]
        [SerializeField] private Vector2 landingSpeed = new(5f, 9f);
        [SerializeField, Min(0f)] private float landingRise = 1.2f;
        [SerializeField] private Vector2 landingSize = new(1.4f, 2.2f);
        [SerializeField] private Vector2 landingLifetime = new(0.9f, 1.4f);
        [SerializeField, Range(0f, 1f)] private float landingOpacity = 0.85f;

        [Title("Landing Shockwave")]
        [Tooltip("바닥에 깔려 밀려 나가는 먼지 고리가 다 퍼졌을 때의 지름(m), 0이면 생략")]
        [SerializeField, Min(0f)] private float shockwaveDiameter = 9f;
        [SerializeField, Min(0.05f)] private float shockwaveLifetime = 0.9f;
        [SerializeField, Range(0f, 1f)] private float shockwaveOpacity = 0.8f;

        [Title("Landing Debris")]
        [Tooltip("두 발 사이에서 튀어 오르는 흙 파편 수")]
        [SerializeField, Min(0)] private int debrisCount = 16;
        [SerializeField] private Vector2 debrisSpeed = new(3.5f, 7.5f);
        [SerializeField] private Vector2 debrisSize = new(0.08f, 0.24f);
        [SerializeField] private Vector2 debrisLifetime = new(1.2f, 2f);

        [Title("Landing Haze")]
        [Tooltip("착지 뒤 천천히 떠올라 한동안 남는 옅고 큰 먼지 수")]
        [SerializeField, Min(0)] private int hazeCount = 3;
        [SerializeField] private Vector2 hazeSize = new(2.2f, 3f);
        [SerializeField] private Vector2 hazeLifetime = new(2.6f, 3.6f);
        [SerializeField, Range(0f, 1f)] private float hazeOpacity = 0.35f;

        [Title("Charge")]
        [Tooltip("이 거리(m)를 달릴 때마다 발을 번갈아 먼지를 차올린다.")]
        [SerializeField, Min(0.1f)] private float chargeStepDistance = 2.2f;
        [SerializeField, Min(1)] private int chargeCountPerStep = 4;
        [SerializeField] private Vector2 chargeSize = new(1f, 1.6f);
        [SerializeField] private Vector2 chargeLifetime = new(0.8f, 1.2f);
        [SerializeField, Range(0f, 1f)] private float chargeOpacity = 0.7f;
        [Tooltip("디딜 때마다 발밑에 깔리는 작은 충격파 지름(m), 0이면 생략")]
        [SerializeField, Min(0f)] private float chargeShockwaveDiameter = 2.6f;
        [Tooltip("디딜 때마다 뒤로 튀는 흙 파편 수")]
        [SerializeField, Min(0)] private int chargeDebrisCount = 3;

        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private BossMonster boss;
        private TimeFreezable clock;
        private GroundDust dust;
        private bool hasChargingParameter;
        private Vector3 previousPosition;
        private float chargeTraveled;
        private int nextFoot;

        private void Awake()
        {
            boss = GetComponent<BossMonster>();
            if (feet == null || feet.Length == 0) feet = FindFeet();
        }

        private void Start()
        {
            clock = boss != null ? boss.Clock : TimeFreezable.GetOrAdd(gameObject);
            clock.FrozenChanged += HandleFrozenChanged;
            if (boss != null && boss.AnimEventListener != null) boss.AnimEventListener.OnOccursAnimStateEvent += HandleAnimEvent;

            // 돌진 패턴이 없는 보스의 Animator에는 파라미터가 없을 수 있음
            Animator animator = boss != null ? boss.Animator : null;
            hasChargingParameter = animator != null && Array.Exists(animator.parameters, parameter => parameter.nameHash == BossAnimParams.IS_CHARGING);
            previousPosition = transform.position;
        }

        private void OnDestroy()
        {
            if (clock != null) clock.FrozenChanged -= HandleFrozenChanged;
            if (boss != null && boss.AnimEventListener != null) boss.AnimEventListener.OnOccursAnimStateEvent -= HandleAnimEvent;
            if (dust != null) Destroy(dust.gameObject);
        }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;
            delta.y = 0f;

            // 보스는 FixedUpdate에서 움직여 프레임마다 이동량이 들쭉날쭉하므로 속도 대신 달린 거리로 셈
            if (IsCharging() == false)
            {
                chargeTraveled = 0f;
                return;
            }

            chargeTraveled += delta.magnitude;
            if (chargeTraveled < chargeStepDistance) return;

            chargeTraveled -= chargeStepDistance;
            if (delta.sqrMagnitude > 0.000001f) KickUp(delta.normalized);
        }

        private bool IsCharging()
        {
            return hasChargingParameter && boss.Animator.GetBool(BossAnimParams.IS_CHARGING);
        }

        // 애니메이션 이벤트 없이 무거운 몸이 땅에 닿는 순간(무력화로 주저앉음, 사망 등)에 쓰는 쪽이 부름
        // amount는 먼지·파편 수 배율, 충격파는 그 절반만큼 넓어짐
        public void PlayLanding(float amount = 1f) => Land(amount);

        private void HandleAnimEvent(string eventName, string parameter)
        {
            if (string.Equals(eventName, "OnJumpLand") || string.Equals(eventName, "OnHopLand")) Land(1f);
        }

        private void HandleFrozenChanged(bool frozen)
        {
            if (dust != null) dust.SetPaused(frozen);
        }

        // 두 발에서 고리 모양으로 퍼지고, 두 발 사이에서 큰 먼지가 피어오름
        private void Land(float amount)
        {
            GroundDust system = GetDust();
            if (system == null) return;

            Vector3 centerSum = Vector3.zero;
            int grounded = 0;

            foreach (Transform foot in feet)
            {
                if (foot == null || TryFindGround(foot.position, out RaycastHit hit) == false) continue;

                system.EmitRing(hit.point + hit.normal * 0.2f, hit.normal, Scale(landingCountPerFoot, amount), landingRadius,
                    landingSpeed, landingRise, landingSize, landingLifetime, landingOpacity);
                centerSum += hit.point;
                grounded++;
            }

            if (grounded == 0) return;

            Vector3 center = centerSum / grounded;
            float shockwaveScale = 1f + (amount - 1f) * 0.5f;
            if (shockwaveDiameter > 0f) system.EmitShockwave(center, shockwaveDiameter * shockwaveScale, shockwaveLifetime * shockwaveScale, shockwaveOpacity);
            system.EmitDebris(center + Vector3.up * 0.1f, Vector3.up, Scale(debrisCount, amount), debrisSpeed, debrisSize, debrisLifetime);

            for (int index = 0; index < Scale(landingCenterCount, amount); index++)
            {
                Vector3 velocity = Vector3.up * UnityEngine.Random.Range(0.8f, 1.8f) + UnityEngine.Random.insideUnitSphere * 1.2f;
                system.Emit(center + Vector3.up * 0.4f, velocity, UnityEngine.Random.Range(landingSize.y, landingSize.y * 1.4f),
                    UnityEngine.Random.Range(landingLifetime.y, landingLifetime.y * 1.3f), landingOpacity * 0.7f);
            }

            // 뒤늦게 천천히 떠올라 착지 자리에 한동안 걸려 있는 먼지
            for (int index = 0; index < Scale(hazeCount, amount); index++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 1.5f;
                Vector3 velocity = Vector3.up * UnityEngine.Random.Range(0.3f, 0.7f) + new Vector3(offset.x, 0f, offset.y) * 0.8f;
                system.Emit(center + new Vector3(offset.x, 0.6f, offset.y), velocity, UnityEngine.Random.Range(hazeSize.x, hazeSize.y),
                    UnityEngine.Random.Range(hazeLifetime.x, hazeLifetime.y), hazeOpacity);
            }
        }

        private static int Scale(int count, float amount) => Mathf.RoundToInt(count * Mathf.Max(0f, amount));

        // 디딘 발 뒤쪽으로 낮게 차올라 돌진 경로에 먼지가 남음
        private void KickUp(Vector3 moveDirection)
        {
            GroundDust system = GetDust();
            if (system == null || feet.Length == 0) return;

            Transform foot = feet[nextFoot % feet.Length];
            nextFoot++;
            if (foot == null || TryFindGround(foot.position, out RaycastHit hit) == false) return;

            Vector3 side = Vector3.Cross(Vector3.up, moveDirection);
            for (int index = 0; index < chargeCountPerStep; index++)
            {
                Vector3 velocity = -moveDirection * UnityEngine.Random.Range(1.5f, 4f)
                                   + side * UnityEngine.Random.Range(-2f, 2f)
                                   + Vector3.up * UnityEngine.Random.Range(0.6f, 1.6f);
                system.Emit(hit.point + Vector3.up * 0.3f, velocity, UnityEngine.Random.Range(chargeSize.x, chargeSize.y),
                    UnityEngine.Random.Range(chargeLifetime.x, chargeLifetime.y), chargeOpacity);
            }

            if (chargeShockwaveDiameter > 0f) system.EmitShockwave(hit.point, chargeShockwaveDiameter, 0.5f, chargeOpacity * 0.8f);

            for (int index = 0; index < chargeDebrisCount; index++)
            {
                Vector3 velocity = -moveDirection * UnityEngine.Random.Range(2f, 4.5f)
                                   + side * UnityEngine.Random.Range(-1.5f, 1.5f)
                                   + Vector3.up * UnityEngine.Random.Range(2f, 4f);
                system.EmitChunk(hit.point + Vector3.up * 0.15f, velocity, UnityEngine.Random.Range(debrisSize.x, debrisSize.y) * 0.8f,
                    UnityEngine.Random.Range(debrisLifetime.x, debrisLifetime.y) * 0.7f);
            }
        }

        // 발 조금 위에서 아래로 쏴서 보스 자신의 콜라이더(발 판정 등)를 건너뛴 가장 가까운 땅
        private bool TryFindGround(Vector3 footPosition, out RaycastHit ground)
        {
            ground = default;
            float bestDistance = float.PositiveInfinity;
            Vector3 origin = footPosition + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, groundCheckDistance + 0.5f, groundMask, QueryTriggerInteraction.Ignore);

            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = groundHits[index];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                ground = hit;
            }

            return bestDistance < float.PositiveInfinity;
        }

        private GroundDust GetDust()
        {
            if (dust == null && dustMaterial != null)
            {
                dust = GroundDust.Create(dustMaterial, $"{name} Foot Dust");
                if (clock != null && clock.IsFrozen) dust.SetPaused(true);
            }
            return dust;
        }

        private Transform[] FindFeet()
        {
            Transform left = null;
            Transform right = null;

            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (left == null && bone.name.EndsWith("L Foot")) left = bone;
                else if (right == null && bone.name.EndsWith("R Foot")) right = bone;
            }

            // 휴머노이드 보스는 뼈 이름 규칙이 달라 아바타의 발 뼈를 씀
            if ((left == null || right == null) && TryGetComponent(out Animator animator) && animator.isHuman)
            {
                left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            if (left == null || right == null)
                Debug.LogWarning($"[{nameof(BossFootDust)}] {name}에서 발 뼈(L Foot·R Foot)를 찾지 못했습니다. Feet를 직접 지정하세요.", this);

            return Array.FindAll(new[] { left, right }, foot => foot != null);
        }
    }
}
