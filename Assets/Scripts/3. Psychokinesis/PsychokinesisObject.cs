using System;
using Sirenix.OdinInspector;
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

        [Title("Monster Push")]
        [Tooltip("몬스터에 닿아 있는 동안 몬스터 진행 방향의 좌우로 가할 가속도(m/s²), 질량과 무관하게 적용한다. " +
                 "바닥 마찰을 이겨야 움직이므로 마찰이 크면 이 값도 키워야 한다.")]
        [SerializeField, Min(0f)] private float monsterPushAcceleration = 20f;

        private const float HitGizmoDuration = 0.3f;
        private static readonly int outlineColorId = Shader.PropertyToID("_BaseColor");
        private readonly Collider[] swingCandidates = new Collider[64];
        private readonly RaycastHit[] swingObstructions = new RaycastHit[64];
        private LayerMask heldExcludeLayer;
        private LayerMask originalExcludeLayer;
        private int monsterLayer;

        private Rigidbody body;
        private BoxCollider hitBox;
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

        public Rigidbody Body => body;
        public bool IsHeld { get; private set; }
        public bool IsAvailable(float maxMass) => isActiveAndEnabled && Body.isKinematic == false && Body.mass <= maxMass;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitBox = GetComponent<BoxCollider>();
            objectColliders = GetComponentsInChildren<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();
            subMeshCount = GetComponent<MeshFilter>().sharedMesh.subMeshCount;
            heldExcludeLayer = LayerMask.GetMask("Psychokinesis");
            monsterLayer = LayerMask.NameToLayer("Monster");
        }

        private void OnDisable()
        {
            ClearPendingDamage();
            canDealThrowDamage = false;
            damageSender = null;
            SetFocused(false, true);
            Release(Vector3.zero);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (canDealThrowDamage == false)
            {
                PushAsideFromMonster(collision);
                return;
            }
            if (TryGetDamageTarget(collision.collider, out ICombatable target) == false) return;

            canDealThrowDamage = false;
            float impactSpeed = collision.relativeVelocity.magnitude;
            int damage = Mathf.RoundToInt(body.mass * impactSpeed * throwDamageScale);
            Debug.Log($"[투척 피해 요청] {name} → {target.GameObject.name}: "
                + $"RoundToInt(질량 {body.mass:F2} × 충돌 상대속도 {impactSpeed:F2} × 배율 {throwDamageScale:F2}) = {damage}", this);
            SendDamage(target, collision.collider, collision.GetContact(0).point, damage);
            damageSender = null;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (canDealThrowDamage == false) PushAsideFromMonster(collision);
        }

        // 바닥에 놓인 오브젝트가 몬스터의 길목을 막지 않도록 몬스터 진행 방향의 좌우로 밀어낸다.
        private void PushAsideFromMonster(Collision collision)
        {
            if (IsHeld || collision.collider.gameObject.layer != monsterLayer) return;

            // 몬스터의 오른쪽에 있으면 오른쪽, 왼쪽에 있으면 왼쪽으로 밀어냄
            Transform monster = collision.transform;
            Vector3 right = Vector3.ProjectOnPlane(monster.right, Vector3.up).normalized;
            Vector3 side = Vector3.Dot(body.position - monster.position, right) >= 0f ? right : -right;

            // 속도를 덮어쓰지 않고 힘을 더해, 충돌로 밀리는 움직임이 그대로 섞이게 함
            body.AddForce(side * monsterPushAcceleration, ForceMode.Acceleration);
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

            impactPosition = body.worldCenterOfMass;
            Vector3 direction = impactPosition - point;
            impactNormal = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
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

            if (readyToSwing && this.swingActivated && swingSpeed >= swingMinSpeed) CheckSwingHit();
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

        private void CheckSwingHit()
        {
            GetSwingHitBox(out Vector3 center, out Vector3 halfExtents);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents,
                swingCandidates, hitBox.transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            ICombatable receiver = null;
            Vector3 nearestPoint = Vector3.zero;
            float nearestDistance = float.PositiveInfinity;

            for (int index = 0; index < count; index++)
            {
                Collider candidate = swingCandidates[index];
                if (TryGetDamageTarget(candidate, out ICombatable target) == false) continue;

                Vector3 point = candidate.ClosestPoint(center);
                float distance = Vector3.Distance(center, point);
                if (distance >= nearestDistance || HasSwingLineOfSight(point, target.GameObject.transform) == false)
                    continue;

                nearest = candidate;
                receiver = target;
                nearestPoint = point;
                nearestDistance = distance;
            }

            if (nearest == null) return;

            swingHitTarget = nearest;
            swingHitPoint = nearestPoint;
            lastSwingHitTime = Time.time;
            int damage = Mathf.RoundToInt(body.mass * swingSpeed * swingDamageScale);
            Debug.Log($"[휘두르기 피해 요청] {name} → {receiver.GameObject.name}: "
                + $"RoundToInt(질량 {body.mass:F2} × 직전 물리 스텝 속도 {swingSpeed:F2} × 배율 {swingDamageScale:F2}) = {damage}", this);
            SendDamage(receiver, nearest, nearestPoint, damage);
            Release(body.linearVelocity);
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
            SetPlayerCollisionsIgnored(false);
            readyToSwing = false;
            swingActivated = false;
            body.angularDamping = originalAngularDamping;
            body.linearVelocity = velocity;
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
            bool active = IsHeld && readyToSwing && swingActivated && swingSpeed >= swingMinSpeed;
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
