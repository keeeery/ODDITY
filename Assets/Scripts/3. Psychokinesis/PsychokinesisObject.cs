using System;
using System.Collections.Generic;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.TimeControl;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamOdd.Psychokinesis
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PsychokinesisObject : MonoBehaviour
    {
        [Header("Outline")]
        [SerializeField] private Material availableOutline;
        [SerializeField] private Material unavailableOutline;
        [SerializeField, Range(1f, 8f)] private float outlineWidthPixels = 3f;

        [Header("Swing Damage")]
        [FormerlySerializedAs("swingAssistRange")]
        [SerializeField, Min(0f)] private float swingHitPadding = 0.5f;
        [SerializeField, Min(0f)] private float swingDamageScale = 1f;
        [SerializeField, Min(0.01f)] private float holdArrivalDistance = 0.4f;
        [SerializeField, Min(0f)] private float holdArrivalSpeed = 1f;

        [Header("Throw Damage")]
        [SerializeField, Min(0f)] private float throwDamageScale = 1f;

        [Header("Groggy")]
        [Tooltip("휘두르기·던지기로 준 피해에 곱해 무력화 피해로 쓰는 배율, 무거운 물체를 빠르게 맞힐수록 크게 깎는다.")]
        [SerializeField, Min(0f)] private float groggyScale = 1f;

        [Header("Boss Step")]
        [Tooltip("맵에 배치된 오브젝트가 보스 다리·발에 닿았을 때 좌우로 밀려나는 수평 속도(m/s)")]
        [SerializeField, Min(0f)] private float bossPushSpeed = 4f;

        [Header("Shatter")]
        [Tooltip("비어 있으면 콘크리트 파편으로, 지정하면 이 머티리얼을 입힌 금속 조각으로 부서진다.")]
        [SerializeField] private Material debrisMaterial;

        private const float HitGizmoDuration = 0.3f;
        private static readonly int outlineColorId = Shader.PropertyToID("_BaseColor");
        private readonly RaycastHit[] swingObstructions = new RaycastHit[64];
        private LayerMask heldExcludeLayer;
        private LayerMask originalExcludeLayer;

        private Rigidbody body;
        private TimeFreezable clock;
        private BoxCollider hitBox;
        private BoxCollider hitTrigger;
        private Vector3 throwVelocity;
        private MeshRenderer meshRenderer;
        private int subMeshCount;

        internal static PsychokinesisObject OutlinedObject { get; private set; }
        internal MeshRenderer OutlineRenderer => meshRenderer;
        internal int SubMeshCount => subMeshCount;
        internal Color OutlineColor { get; private set; }
        // 조준 중인 대상을 지금 잡을 수 있는지 (조준점 링 표시용)
        internal bool IsFocusAvailable { get; private set; }
        internal float OutlineWidthPixels => outlineWidthPixels;
        private Collider[] objectColliders;
        private Collider[] playerColliders;
        private bool[] originalIgnoredPairs;
        private bool originalGravity;
        private float originalAngularDamping;
        private bool canDealThrowDamage;
        private bool readyToSwing;
        private bool swingActivated;
        private float swingSpeed;
        private float swingMinSpeed;
        private ICombatable damageSender;

        private Collider swingHitTarget;
        private Vector3 swingHitPoint;
        private float lastSwingHitTime = float.NegativeInfinity;

        private CombatManager pendingDamageManager;
        private DamageEvent pendingDamage;
        private Vector3 impactPosition;
        private Vector3 impactNormal;

        // 보스 돌진 파편처럼 보스 발밑에서 생긴 오브젝트가 바로 밟혀 부서지지 않도록 하는 유예
        // 유예 중에 닿은 보스 부위는 한 번 떨어질 때까지 계속 무시해, 유예가 끝나는 순간 발밑에서 부서지지 않게 함
        private float bossShatterGraceEndTime = float.NegativeInfinity;
        private readonly HashSet<Collider> graceContacts = new();

        // 보스 패턴(돌진, 투하)으로 생긴 오브젝트는 던졌을 때 처음 부딪힌 곳이 벽이면 파편화함
        private int wallLayer;
        private bool shatterOnWallHit;

        // 풀을 거치지 않고 맵에 배치된 오브젝트, 보스에게 밟혀도 파편화되지 않고 좌우로 밀려남
        private bool isPlacedInMap;
        public bool IsPlacedInMap => isPlacedInMap;
        public bool IsSpawnedByBoss { get; private set; }

        // 파편화 위치, 바깥쪽 방향, 질량, 파편 머티리얼(없으면 null)
        public static event Action<Vector3, Vector3, float, Material> Shattered;

        // 미니맵 등에서 순회하기 위한 활성 오브젝트 목록
        private static readonly List<PsychokinesisObject> activeObjects = new();
        public static IReadOnlyList<PsychokinesisObject> ActiveObjects => activeObjects;

        // IK 추가
        [SerializeField] private Transform targetPoint;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float ikWeight;
        public Transform TargetPoint => targetPoint;
        public float IkWeight => ikWeight;

        public Rigidbody Body => body;
        public bool IsHeld { get; private set; }
        // 시간 정지로 kinematic이 된 오브젝트도 원래 잡을 수 있었으면 잡을 수 있음
        public bool IsAvailable(float maxMass) => isActiveAndEnabled && clock.IsKinematicIgnoringFreeze(Body) == false && Body.mass <= maxMass;
        private bool IsSwingActive => IsHeld && readyToSwing && swingActivated && swingSpeed >= swingMinSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            clock = TimeFreezable.GetOrAdd(gameObject);
            hitBox = GetComponent<BoxCollider>();
            CreateHitTrigger();
            objectColliders = GetComponentsInChildren<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();
            subMeshCount = GetComponent<MeshFilter>().sharedMesh.subMeshCount;
            heldExcludeLayer = LayerMask.GetMask("Psychokinesis");
            wallLayer = LayerMask.NameToLayer("Wall");
            isPlacedInMap = TryGetComponent(out IObjectPoolItem _) == false;
        }

        // 피격 판정용 Trigger, 보스 Rigidbody가 Psychokinesis 레이어를 제외하므로 별도 레이어에 두고 몬스터하고만 반응
        private void CreateHitTrigger()
        {
            GameObject triggerObject = new GameObject("HitTrigger") { layer = LayerMask.NameToLayer("PsychokinesisHit") };
            triggerObject.transform.SetParent(transform, false);
            hitTrigger = triggerObject.AddComponent<BoxCollider>();
            hitTrigger.isTrigger = true;
            hitTrigger.excludeLayers = ~LayerMask.GetMask("Monster");
        }

        // 풀에서 꺼낼 때 ApplyVariant로 바뀐 형태에 맞춤
        private void OnEnable()
        {
            ResizeHitTrigger(false);
            activeObjects.Add(this);
            ClearBossShatterGrace();
            IsSpawnedByBoss = false;
            shatterOnWallHit = false;
        }

        // 들고 있는 동안에만 휘두르기 여유 범위를 더함
        private void ResizeHitTrigger(bool withSwingPadding)
        {
            Vector3 scale = transform.lossyScale;
            Vector3 padding = withSwingPadding
                ? new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z)) * (swingHitPadding * 2f)
                : Vector3.zero;
            hitTrigger.center = hitBox.center;
            hitTrigger.size = hitBox.size + padding;
        }

        // 트리거 판정은 물리 스텝 이후에 오므로 스텝 직전 속도로 투척 피해를 계산
        private void FixedUpdate()
        {
            // 시간 정지로 멈춘 동안은 속도가 0이므로 멈추기 전 속도를 유지
            if (canDealThrowDamage && clock.IsFrozen == false) throwVelocity = body.linearVelocity;
        }

        private void OnDisable()
        {
            activeObjects.Remove(this);
            ClearPendingDamage();
            canDealThrowDamage = false;
            damageSender = null;
            SetFocused(false, true);
            Release(Vector3.zero);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (TryThrowHit(other)) return;
            TryShatterByBoss(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsSwingActive && TrySwingHit(other)) return;
            TryShatterByBoss(other);
        }

        private void OnTriggerExit(Collider other)
        {
            graceContacts.Remove(other);
        }

        // 던진 뒤 처음 부딪힌 대상만 보고, 바닥 등 다른 곳에 먼저 닿았으면 그 뒤로는 벽에 닿아도 그대로 둠
        // 보스는 Psychokinesis 레이어와 물리 충돌하지 않으므로 보스를 맞힌 경우는 피격 트리거 쪽에서 처리됨
        private void OnCollisionEnter(Collision collision)
        {
            if (shatterOnWallHit == false) return;

            shatterOnWallHit = false;
            if (collision.collider.gameObject.layer != wallLayer || pendingDamage != null) return;

            SetImpact(collision.GetContact(0).point);
            Shatter();
        }

        // 보스 패턴이 풀에서 꺼낸 직후 호출, 풀로 돌아갔다 다시 꺼내지면 OnEnable에서 해제됨
        public void MarkSpawnedByBoss()
        {
            IsSpawnedByBoss = true;
        }

        // duration(초) 동안 보스 다리·발에 닿아도 파편화하지 않음
        public void GrantBossShatterGrace(float duration)
        {
            bossShatterGraceEndTime = clock.LocalTime + duration;
            graceContacts.Clear();
        }

        private void ClearBossShatterGrace()
        {
            bossShatterGraceEndTime = float.NegativeInfinity;
            graceContacts.Clear();
        }

        private bool IsInBossShatterGrace(Collider other)
        {
            if (clock.LocalTime < bossShatterGraceEndTime)
            {
                graceContacts.Add(other);
                return true;
            }

            return graceContacts.Contains(other);
        }

        private bool TryThrowHit(Collider other)
        {
            if (canDealThrowDamage == false || TryGetDamageTarget(other, out ICombatable target) == false) return false;

            canDealThrowDamage = false;
            Vector3 targetVelocity = other.attachedRigidbody != null ? other.attachedRigidbody.linearVelocity : Vector3.zero;
            float impactSpeed = (throwVelocity - targetVelocity).magnitude;
            int damage = CalculateDamage(impactSpeed, throwDamageScale, out int groggy);
            Debug.Log($"[투척 피해 요청] {name} → {target.GameObject.name}: "
                + $"질량 {body.mass:F2}, 피해 {damage}, 무력화 {groggy}", this);
            SendDamage(target, other, other.ClosestPoint(body.worldCenterOfMass), damage, groggy);
            damageSender = null;
            return true;
        }

        // 보스의 다리·발(BossShatterCollider)에 닿으면 밟힌 것처럼 파편화함, 다른 부위는 부딪히기만 함
        // 맵에 배치된 오브젝트는 파편화하지 않고 보스 진행 방향의 좌우로 밀려남
        // 들고만 있을 때는 통과시키고, 피해 처리를 기다리는 중이면 그쪽 결과를 따름
        private bool TryShatterByBoss(Collider other)
        {
            if ((IsHeld && IsSwingActive == false) ||
                pendingDamage != null)
                return false;
            if (other.TryGetComponent(out BossShatterCollider _) == false)
                return false;
            if (isPlacedInMap)
                return TryPushAsideByBoss(other);
            if (IsInBossShatterGrace(other))
                return false;

            SetImpact(other.ClosestPoint(body.worldCenterOfMass));
            Shatter();
            return true;
        }

        // 닿아 있는 동안 매 물리 스텝 불리므로 속도를 더하지 않고 bossPushSpeed까지만 채움
        // 휘두르는 중이거나 띄워진(kinematic) 상태면 밀지 않음
        private bool TryPushAsideByBoss(Collider other)
        {
            if (IsHeld || body.isKinematic) return false;

            Transform boss = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
            Vector3 right = Vector3.Cross(Vector3.up, TelegraphShape.FlatForward(boss.rotation));
            Vector3 pushDir = Vector3.Dot(body.worldCenterOfMass - boss.position, right) >= 0f ? right : -right;

            float currentSpeed = Vector3.Dot(body.linearVelocity, pushDir);
            if (currentSpeed < bossPushSpeed)
                body.AddForce(pushDir * (bossPushSpeed - currentSpeed), ForceMode.VelocityChange);
            return true;
        }

        private static bool TryGetDamageTarget(Collider collider, out ICombatable target)
        {
            if (MonsterRegistry.TryGetMonster(collider, out target) == false) return false;
            if (target is CombatEntity entity) return entity.IsDead == false && entity.isActiveAndEnabled;
            return true;
        }

        private int CalculateDamage(float speed, float scale, out int groggy)
        {
            float momentum = body.mass * speed;
            if (damageSender != null && damageSender.GameObject != null &&
                damageSender.GameObject.TryGetComponent(out PsychokinesisController controller) &&
                controller.TryCalculateObjectDamage(body.mass, out int overriddenDamage, out groggy))
                return overriddenDamage;

            int damage = Mathf.RoundToInt(momentum * scale);
            groggy = Mathf.RoundToInt(damage * groggyScale);
            return damage;
        }

        private void SendDamage(ICombatable target, Collider collider, Vector3 point, int damage, int groggy)
        {
            if (pendingDamage != null) return;

            SetImpact(point);
            DamageInfo info = new DamageInfo(damage, point, collider, groggy: groggy);
            pendingDamage = new DamageEvent(damageSender, target, info);
            pendingDamageManager = CombatManager.Instance;
            pendingDamageManager.OnDamageEventExecuted += HandleDamageExecuted;
            pendingDamageManager.AddCombatEvent(pendingDamage);
        }

        private void ClearPendingDamage()
        {
            if (pendingDamage == null) return;
            if (pendingDamageManager != null) pendingDamageManager.OnDamageEventExecuted -= HandleDamageExecuted;
            pendingDamage = null;
        }

        private void HandleDamageExecuted(DamageEvent damageEvent, int receivedDamage)
        {
            if (ReferenceEquals(damageEvent, pendingDamage) == false) return;

            ClearPendingDamage();
            if (receivedDamage <= 0) return;

            Shatter();
        }

        private void SetImpact(Vector3 point)
        {
            impactPosition = body.worldCenterOfMass;
            Vector3 direction = impactPosition - point;
            impactNormal = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        }

        // 보스 투척처럼 다른 쪽에서 맞힌 경우, 맞은 지점 기준으로 피격된 것처럼 파편화함
        public void ShatterAt(Vector3 hitPoint)
        {
            if (isActiveAndEnabled == false) return;

            SetImpact(hitPoint);
            Shatter();
        }

        private void Shatter()
        {
            Release(Vector3.zero);
            // 원본 Collider를 먼저 제거해야 생성된 파편이 원본과 겹치지 않는다.
            gameObject.SetActive(false);
            Shattered?.Invoke(impactPosition, impactNormal, body.mass, debrisMaterial);
            Remove();
        }

        // 맵 밖(구멍 아래 등)으로 떨어진 경우처럼 파편 없이 조용히 없앰
        public void Vanish()
        {
            if (isActiveAndEnabled == false) return;

            Release(Vector3.zero);
            gameObject.SetActive(false);
            Remove();
        }

        private void Remove()
        {
            if (TryGetComponent(out IObjectPoolItem poolItem)) poolItem.ReturnToPool();
            else Destroy(gameObject);
        }

        public void UpdateHoldState(Vector3 targetPosition, Vector3 playerVelocity, bool canSwing, bool swingActivated, float minimumSwingSpeed)
        {
            this.swingActivated = canSwing && swingActivated;
            swingMinSpeed = minimumSwingSpeed;

            if (canSwing == false)
            {
                readyToSwing = false;
                swingSpeed = 0f;
                return;
            }

            float arrivalSpeed = (body.linearVelocity - playerVelocity).magnitude;

            // 처음 끌어오는 동안의 이동과 잔여 속도는 휘두르기로 취급하지 않음
            if (readyToSwing == false &&
                Vector3.Distance(body.worldCenterOfMass, targetPosition) <= holdArrivalDistance &&
                arrivalSpeed <= holdArrivalSpeed)
                readyToSwing = true;
        }

        private void GetSwingHitBox(out Vector3 center, out Vector3 halfExtents)
        {
            center = hitBox.transform.TransformPoint(hitBox.center);
            Vector3 scale = hitBox.transform.lossyScale;
            halfExtents = Vector3.Scale(hitBox.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) + Vector3.one * swingHitPadding;
        }

        public void RecordPreCollisionVelocity(Vector3 velocity, Vector3 playerVelocity)
        {
            swingSpeed = (velocity - playerVelocity).magnitude;
        }

        // 휘두르는 중 트리거에 처음 닿은 유효한 대상에게 피해를 준다.
        private bool TrySwingHit(Collider other)
        {
            if (TryGetDamageTarget(other, out ICombatable receiver) == false) return false;

            GetSwingHitBox(out Vector3 center, out _);
            Vector3 point = other.ClosestPoint(center);
            if (HasSwingLineOfSight(point, receiver.GameObject.transform) == false) return false;

            swingHitTarget = other;
            swingHitPoint = point;
            lastSwingHitTime = Time.time;
            int damage = CalculateDamage(swingSpeed, swingDamageScale, out int groggy);
            Debug.Log($"[휘두르기 피해 요청] {name} → {receiver.GameObject.name}: "
                + $"질량 {body.mass:F2}, 피해 {damage}, 무력화 {groggy}", this);
            SendDamage(receiver, other, point, damage, groggy);
            Release(body.linearVelocity);
            return true;
        }

        private bool HasSwingLineOfSight(Vector3 point, Transform target)
        {
            Vector3 offset = point - body.worldCenterOfMass;
            int count = Physics.RaycastNonAlloc(body.worldCenterOfMass, offset.normalized, swingObstructions,
                offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            for (int index = 0; index < count; index++)
            {
                Transform hit = swingObstructions[index].collider.transform;

                if (hit.IsChildOf(transform) || hit.IsChildOf(damageSender.GameObject.transform) || hit.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }

        private void SetPlayerCollisionsIgnored(bool ignored)
        {
            for (int objectIndex = 0; objectIndex < objectColliders.Length; objectIndex++)
            {
                for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
                {
                    Collider objectCollider = objectColliders[objectIndex];
                    Collider playerCollider = playerColliders[playerIndex];

                    if (objectCollider == null || playerCollider == null) continue;

                    int pairIndex = objectIndex * playerColliders.Length + playerIndex;

                    if (ignored) originalIgnoredPairs[pairIndex] =
                            Physics.GetIgnoreCollision(objectCollider, playerCollider);

                    Physics.IgnoreCollision(
                        objectCollider, playerCollider, ignored || originalIgnoredPairs[pairIndex]);
                }
            }
        }

        public void SetFocused(bool isFocused, bool available)
        {
            if (isFocused == false)
            {
                if (OutlinedObject == this) OutlinedObject = null;
                return;
            }
            IsFocusAvailable = available;
            Material colorMaterial = available ? availableOutline : unavailableOutline;
            OutlineColor = colorMaterial.GetColor(outlineColorId);
            OutlinedObject = this;
        }

        // 크기와 질량에 factor를 곱함. 풀에서 다시 꺼낼 때 ApplyVariant가 원래 형태로 되돌림
        // 휘두르기 여유 범위는 크기에 맞춰 다시 계산해야 하므로 트리거도 맞춤
        public void Resize(float factor)
        {
            transform.localScale *= factor;
            body.mass *= factor;
            ResizeHitTrigger(IsHeld);
        }

        // 시간 정지 중에도 플레이어가 든 오브젝트는 움직여야 하므로 먼저 정지에서 빼냄
        public void BeginHold(Collider[] ownerColliders, ICombatable sender)
        {
            clock.SetWorldExempt(true);
            ClearBossShatterGrace(); // 플레이어가 잡은 뒤로는 원래 규칙대로 보스에 닿으면 파편화
            canDealThrowDamage = false;
            shatterOnWallHit = false;
            damageSender = sender;
            readyToSwing = false;
            swingActivated = false;
            swingSpeed = 0f;
            playerColliders = ownerColliders;
            originalIgnoredPairs = new bool[objectColliders.Length * playerColliders.Length];
            SetPlayerCollisionsIgnored(true);
            originalGravity = body.useGravity;
            originalAngularDamping = body.angularDamping;
            body.useGravity = false;
            body.angularDamping = 6f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            originalExcludeLayer = body.excludeLayers;
            body.excludeLayers = originalExcludeLayer | heldExcludeLayer;
            ResizeHitTrigger(true);
            IsHeld = true;
        }

        public void Release(Vector3 velocity, bool isThrowing = false, ICombatable sender = null)
        {
            if (IsHeld == false) return;

            IsHeld = false;
            canDealThrowDamage = isThrowing;
            shatterOnWallHit = isThrowing && IsSpawnedByBoss;
            damageSender = isThrowing ? sender : null;
            body.useGravity = originalGravity;
            body.excludeLayers = originalExcludeLayer;
            ResizeHitTrigger(false);
            SetPlayerCollisionsIgnored(false);
            readyToSwing = false;
            swingActivated = false;
            body.angularDamping = originalAngularDamping;
            body.linearVelocity = velocity;
            throwVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            SetFocused(false, true);

            // 시간 정지 중에 던졌으면 정지가 끝날 때까지 날아가게 둠
            clock.ClearWorldExemptOnResume();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (IsHeld == false && Time.time > lastSwingHitTime + HitGizmoDuration) return;

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Color originalColor = Gizmos.color;
            GetSwingHitBox(out Vector3 center, out Vector3 halfExtents);
            bool active = IsSwingActive;
            Gizmos.matrix = Matrix4x4.TRS(center, hitBox.transform.rotation, Vector3.one);
            Gizmos.color = active ? Color.magenta : Color.gray;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity;
            UnityEditor.Handles.Label(center + Vector3.up * (halfExtents.magnitude + 0.2f),
                $"Swing hit box: padding {swingHitPadding:F2} m [{(active ? "Active" : "Inactive")}]\n"
                + $"Camera: {(swingActivated ? "Active" : "Inactive")}, Previous speed: {swingSpeed:F2} / {swingMinSpeed:F2} m/s, Ready: {readyToSwing}");

            if (swingHitTarget != null && Time.time <= lastSwingHitTime + HitGizmoDuration)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(swingHitTarget.bounds.center, swingHitTarget.bounds.size);
                Gizmos.DrawLine(center, swingHitPoint);
                Gizmos.DrawSphere(swingHitPoint, 0.12f);
                UnityEditor.Handles.Label(swingHitPoint + Vector3.up * 0.2f, "Hit: " + swingHitTarget.name);
            }
            Gizmos.matrix = originalMatrix;
            Gizmos.color = originalColor;
        }
#endif
    }
}
