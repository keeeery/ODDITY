using System;
using System.Collections.Generic;
using TeamOdd.Boss;
using TeamOdd.Combat;
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

        private const float HitGizmoDuration = 0.3f;
        private static readonly int outlineColorId = Shader.PropertyToID("_BaseColor");
        private readonly RaycastHit[] swingObstructions = new RaycastHit[64];
        private LayerMask heldExcludeLayer;
        private LayerMask originalExcludeLayer;

        private Rigidbody body;
        private BoxCollider hitBox;
        private BoxCollider hitTrigger;
        private Vector3 throwVelocity;
        private MeshRenderer meshRenderer;
        private int subMeshCount;

        internal static PsychokinesisObject OutlinedObject { get; private set; }
        internal MeshRenderer OutlineRenderer => meshRenderer;
        internal int SubMeshCount => subMeshCount;
        internal Color OutlineColor { get; private set; }
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

        public static event Action<Vector3, Vector3, float> Shattered;

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
        public bool IsAvailable(float maxMass) => isActiveAndEnabled && Body.isKinematic == false && Body.mass <= maxMass;
        private bool IsSwingActive => IsHeld && readyToSwing && swingActivated && swingSpeed >= swingMinSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitBox = GetComponent<BoxCollider>();
            CreateHitTrigger();
            objectColliders = GetComponentsInChildren<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();
            subMeshCount = GetComponent<MeshFilter>().sharedMesh.subMeshCount;
            heldExcludeLayer = LayerMask.GetMask("Psychokinesis");
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
            if (canDealThrowDamage) throwVelocity = body.linearVelocity;
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

        private bool TryThrowHit(Collider other)
        {
            if (canDealThrowDamage == false || TryGetDamageTarget(other, out ICombatable target) == false) return false;

            canDealThrowDamage = false;
            Vector3 targetVelocity = other.attachedRigidbody != null ? other.attachedRigidbody.linearVelocity : Vector3.zero;
            float impactSpeed = (throwVelocity - targetVelocity).magnitude;
            int damage = Mathf.RoundToInt(body.mass * impactSpeed * throwDamageScale);
            Debug.Log($"[투척 피해 요청] {name} → {target.GameObject.name}: "
                + $"RoundToInt(질량 {body.mass:F2} × 충돌 상대속도 {impactSpeed:F2} × 배율 {throwDamageScale:F2}) = {damage}", this);
            SendDamage(target, other, other.ClosestPoint(body.worldCenterOfMass), damage);
            damageSender = null;
            return true;
        }

        // 보스에 밟히거나 피해를 줄 수 없는 보스에 맞으면 피격된 것처럼 파편화함
        // 들고만 있을 때는 통과시키고, 피해 처리를 기다리는 중이면 그쪽 결과를 따름
        private bool TryShatterByBoss(Collider other)
        {
            if ((IsHeld && IsSwingActive == false) ||
                pendingDamage != null)
                return false;
            if (other.attachedRigidbody == null ||
                other.attachedRigidbody.TryGetComponent(out BossMonster _) == false)
                return false;

            SetImpact(other.ClosestPoint(body.worldCenterOfMass));
            Shatter();
            return true;
        }

        private static bool TryGetDamageTarget(Collider collider, out ICombatable target)
        {
            if (MonsterRegistry.TryGetMonster(collider, out target) == false) return false;
            if (target is CombatEntity entity) return entity.IsDead == false && entity.isActiveAndEnabled;
            return true;
        }

        private void SendDamage(ICombatable target, Collider collider, Vector3 point, int damage)
        {
            if (pendingDamage != null) return;

            SetImpact(point);
            DamageInfo info = new DamageInfo(damage, point, collider);
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

        private void Shatter()
        {
            Release(Vector3.zero);
            // 원본 Collider를 먼저 제거해야 생성된 파편이 원본과 겹치지 않는다.
            gameObject.SetActive(false);
            Shattered?.Invoke(impactPosition, impactNormal, body.mass);

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
            int damage = Mathf.RoundToInt(body.mass * swingSpeed * swingDamageScale);
            Debug.Log($"[휘두르기 피해 요청] {name} → {receiver.GameObject.name}: "
                + $"RoundToInt(질량 {body.mass:F2} × 직전 물리 스텝 속도 {swingSpeed:F2} × 배율 {swingDamageScale:F2}) = {damage}", this);
            SendDamage(receiver, other, point, damage);
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
            Material colorMaterial = available ? availableOutline : unavailableOutline;
            OutlineColor = colorMaterial.GetColor(outlineColorId);
            OutlinedObject = this;
        }

        public void BeginHold(Collider[] ownerColliders, ICombatable sender)
        {
            canDealThrowDamage = false;
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
