using System.Collections.Generic;
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
        [SerializeField] private Material meshOutline;
        [SerializeField, Min(0f)] private float outlineWidth = 0.05f;

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
        private static readonly int outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private readonly Collider[] swingCandidates = new Collider[64];
        private readonly RaycastHit[] swingObstructions = new RaycastHit[64];

        private Rigidbody body;
        private BoxCollider hitBox;
        private MeshRenderer outline;
        private Mesh outlineMesh;
        private MaterialPropertyBlock outlineProperties;
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

        public Rigidbody Body => body;
        public bool IsHeld { get; private set; }
        public bool IsAvailable(float maxMass) => isActiveAndEnabled && Body.isKinematic == false && Body.mass <= maxMass;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitBox = GetComponent<BoxCollider>();
            objectColliders = GetComponentsInChildren<Collider>();
            CreateOutline();
        }

        private void OnDisable()
        {
            canDealThrowDamage = false;
            damageSender = null;
            Release(Vector3.zero);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (canDealThrowDamage == false) return;
            if (TryGetDamageTarget(collision.collider, out ICombatable target) == false) return;

            canDealThrowDamage = false;
            float impactSpeed = collision.relativeVelocity.magnitude;
            int damage = Mathf.RoundToInt(body.mass * impactSpeed * throwDamageScale);
            Debug.Log($"[투척 피해 요청] {name} → {target.GameObject.name}: "
                + $"RoundToInt(질량 {body.mass:F2} × 충돌 상대속도 {impactSpeed:F2} × 배율 {throwDamageScale:F2}) = {damage}", this);
            SendDamage(target, collision.collider, collision.GetContact(0).point, damage);
            damageSender = null;
        }

        private static bool TryGetDamageTarget(Collider collider, out ICombatable target)
        {
            if (MonsterRegistry.TryGetMonster(collider, out target) == false) return false;
            if (target is CombatEntity entity) return entity.IsDead == false && entity.isActiveAndEnabled;
            return true;
        }

        private void SendDamage(ICombatable target, Collider collider, Vector3 point, int damage)
        {
            DamageInfo info = new DamageInfo(damage, point, collider);
            DamageEvent damageEvent = new DamageEvent(damageSender, target, info);
            CombatManager.Instance.AddCombatEvent(damageEvent);
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

        private void CreateOutline()
        {
            GameObject outlineObject = new GameObject("Focus Outline");
            outlineObject.transform.SetParent(transform, false);
            Mesh source = GetComponent<MeshFilter>().sharedMesh;
            outlineMesh = Instantiate(source);
            outlineMesh.name = source.name + " Outline";
            Vector3[] vertices = outlineMesh.vertices;
            Vector3[] normals = outlineMesh.normals;
            Dictionary<Vector3, Vector3> smoothNormals = new Dictionary<Vector3, Vector3>();
            // UV와 하드 에지 때문에 분리된 동일 위치 정점도 같은 방향으로 확장한다.
            for (int index = 0; index < vertices.Length; index++)
            {
                smoothNormals.TryGetValue(vertices[index], out Vector3 sum);
                smoothNormals[vertices[index]] = sum + normals[index];
            }
            for (int index = 0; index < vertices.Length; index++)
                normals[index] = smoothNormals[vertices[index]].normalized;
            outlineMesh.normals = normals;
            Bounds bounds = outlineMesh.bounds;
            bounds.Expand(outlineWidth * 2f);
            outlineMesh.bounds = bounds;

            outlineObject.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
            outline = outlineObject.AddComponent<MeshRenderer>();
            Material[] materials = new Material[outlineMesh.subMeshCount];
            for (int index = 0; index < materials.Length; index++) materials[index] = meshOutline;
            outline.sharedMaterials = materials;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outlineProperties = new MaterialPropertyBlock();
            outline.enabled = false;
        }

        public void SetFocused(bool isFocused, bool available)
        {
            outline.enabled = isFocused;

            if (isFocused == false) return;

            Material colorMaterial = available ? availableOutline : unavailableOutline;
            outlineProperties.SetColor(outlineColorId, colorMaterial.GetColor(outlineColorId));
            outlineProperties.SetFloat(outlineWidthId, outlineWidth);
            outline.SetPropertyBlock(outlineProperties);
        }

        private void OnDestroy()
        {
            Destroy(outlineMesh);
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
            IsHeld = true;
        }

        public void Release(Vector3 velocity, bool isThrowing = false, ICombatable sender = null)
        {
            if (IsHeld == false) return;

            IsHeld = false;
            canDealThrowDamage = isThrowing;
            damageSender = isThrowing ? sender : null;
            body.useGravity = originalGravity;
            SetPlayerCollisionsIgnored(false);
            readyToSwing = false;
            swingActivated = false;
            body.angularDamping = originalAngularDamping;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            SetFocused(false, true);
        }
    }
}
