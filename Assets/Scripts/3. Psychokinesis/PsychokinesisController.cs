using TeamOdd.Combat;
using Study.Utilities;
using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    [DefaultExecutionOrder(100)]
    public sealed class PsychokinesisController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private CharacterController player;
        [SerializeField] private CombatEntity combatEntity;
        [SerializeField] private Player.Player playerStatus;
        [SerializeField] private LayerMask queryLayers = Physics.DefaultRaycastLayers;

        [Header("Targeting")]
        [SerializeField, Min(1f)] private float pickupDistance = 14f;
        [SerializeField, Min(0.1f)] private float castHalfWidth = 2f;
        [SerializeField, Min(0f)] private float maxMass = 20f;

        [Header("Normal Attack")]
        [SerializeField, Min(0.01f)] private float attackInterval = 0.25f;

        [Header("Swing Activation")]
        [SerializeField, Min(0f)] private float swingMinSpeed = 8f;
        [SerializeField, Min(1f)] private float swingActivationSpeed = 180f;
        [SerializeField, Min(0f)] private float swingActiveDuration = 0.12f;

        [Header("Hold Rotation")]
        [SerializeField, Min(0.01f)] private float holdRotationLag = 0.18f;
        [SerializeField, Min(1f)] private float swingRotationSpeed = 180f;
        [SerializeField, Range(0f, 90f)] private float maxHoldLagAngle = 60f;

        [Header("Hold Physics")]
        [SerializeField, Min(1f)] private float holdDistance = 8f;
        [SerializeField, Min(1f)] private float followStrength = 180f;
        [SerializeField, Min(0f)] private float followDamping = 24f;
        [SerializeField, Min(1f)] private float maxAcceleration = 400f;
        [SerializeField, Min(1f)] private float maxHoldSpeed = 26f;
        [SerializeField, Min(1f)] private float breakDistance = 16f;

        [Header("Stamina")]
        [SerializeField, Min(0f)] private float holdStaminaCostPerSecond = 20f;
        [SerializeField, Min(0f)] private float minGrabStamina = 10f;

        [Header("Throwing")]
        [SerializeField, Min(0.01f)] private float throwPrepareDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float throwPrepareDistance = 2.5f;
        [SerializeField, Min(1f)] private float throwPullMultiplier = 3f;
        [SerializeField, Min(1f)] private float throwSpeed = 25f;

        private const float SmallRotationLag = 0.025f;
        private const float ThrowAimDistance = 100f;

#if UNITY_EDITOR
        private struct TargetingCandidate
        {
            public PsychokinesisObject Target;
            public float CenterDistance;
            public float PlayerDistance;
            public Vector3 Position;
            public bool Visible;
        }

        private readonly TargetingCandidate[] candidates = new TargetingCandidate[128];
        private int candidateCount;
        private float horizontalRotationSpeed;
#endif
        private readonly Collider[] queryColliders = new Collider[128];
        private readonly RaycastHit[] obstructionHits = new RaycastHit[128];
        private PsychokinesisObject focusedObject;
        private PsychokinesisObject heldObject;
        private Collider[] playerColliders;
        private Vector3 holdDirection;
        private Vector3 previousCameraDirection;
        private Vector3 throwPrepareStartOffset;
        private float throwPrepareStartTime = float.NegativeInfinity;
        private Vector3 holdPosition;
        private Vector3 previousHoldPosition;
        private bool targetInitialized;
        private bool grabRequested;
        private bool releaseRequested;
        private bool throwRequested;
        private bool attackRequested;
        private float nextAttackTime;
        private float swingActiveUntil = float.NegativeInfinity;
        
        public PsychokinesisObject HeldObject => heldObject;
        public bool GrabRequested => grabRequested;
        public bool ReleaseRequested => releaseRequested;

        public Vector3 Viewport => aimCamera.WorldToViewportPoint(new Vector3(0.5f, 0.5f));

        private bool IsPreparingThrow => throwPrepareStartTime >= 0f;

        private void Awake()
        {
            playerColliders = player.GetComponentsInChildren<Collider>();
            if (playerStatus == null) playerStatus = GetComponent<Player.Player>();
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                releaseRequested = true;
                grabRequested = false;
                throwRequested = false;
                attackRequested = false;

                return;
            }

            bool rightHeld = SimpleInput.GetMouseButton(SimpleInput.MouseButton.Right);

            if (rightHeld == false) releaseRequested = true;
            else if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right))
            {
                grabRequested = true;
                releaseRequested = false;
            }

            if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                if (heldObject == null) attackRequested = true;
                else if (rightHeld)
                {
                    swingActiveUntil = float.NegativeInfinity;
                    throwPrepareStartOffset = heldObject.Body.worldCenterOfMass - GetHoldOrigin();
                    throwPrepareStartTime = Time.time;
                    targetInitialized = false;
                }
            }

            if (IsPreparingThrow && rightHeld && SimpleInput.GetMouseButtonUp(SimpleInput.MouseButton.Left))
                throwRequested = true;
        }

        private void LateUpdate()
        {
            PsychokinesisObject target = FindFocus();

            if (heldObject == null)
                SetFocus(Cursor.lockState == CursorLockMode.Locked ? target : null);

            if (grabRequested)
            {
                if (heldObject == null && focusedObject != null && focusedObject.IsAvailable(maxMass) && HasGrabStamina())
                {
                    heldObject = focusedObject;
                    Ray grabRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                    holdDirection = GetCameraAlignedDirection(grabRay, holdDistance);
                    previousCameraDirection = grabRay.direction;
#if UNITY_EDITOR
                    horizontalRotationSpeed = 0f;
#endif
                    swingActiveUntil = float.NegativeInfinity;
                    heldObject.BeginHold(playerColliders, combatEntity);
                    SetFocus(null);
                    heldObject.SetFocused(true, true);
                    targetInitialized = false;
                }

                grabRequested = false;
                Debug.Log(grabRequested);
            }

            if (heldObject != null && throwRequested && releaseRequested == false)
            {
                ReleaseHeld(true);
            }

            if (attackRequested)
            {
                if (heldObject == null) NormalAttack();

                attackRequested = false;
            }

            if (heldObject != null)
            {
                holdPosition = GetHoldPosition();
                ConsumeHoldStamina();
            }
        }

        private bool HasGrabStamina()
        {
            return playerStatus == null || playerStatus.Stamina >= minGrabStamina;
        }

        // 홀딩 중에는 매 프레임 스태미나를 소모하고, 바닥나면 다음 FixedUpdate에서 놓도록 요청한다
        private void ConsumeHoldStamina()
        {
            if (playerStatus == null) return;

            playerStatus.ConsumeStamina(holdStaminaCostPerSecond * Time.deltaTime);

            if (playerStatus.Stamina <= 0f) releaseRequested = true;
        }

        private void FixedUpdate()
        {
            if (heldObject != null && (releaseRequested || heldObject.IsHeld == false || heldObject.IsAvailable(maxMass) == false))
                ReleaseHeld(false);

            releaseRequested = false;

            if (heldObject == null) return;

            Rigidbody body = heldObject.Body;

            if (Vector3.Distance(player.transform.position, body.position) > breakDistance)
            {
                ReleaseHeld(false);

                return;
            }

            heldObject.UpdateHoldState(holdPosition, player.velocity, IsPreparingThrow == false,
                Time.time <= swingActiveUntil, swingMinSpeed);

            if (heldObject.IsHeld == false)
            {
                ReleaseHeld(false);
                return;
            }

            Vector3 targetVelocity = GetTargetVelocity();
            float pullMultiplier = IsPreparingThrow ? Mathf.Lerp(1f, throwPullMultiplier, GetThrowPrepareBlend()) : 1f;
            Vector3 offset = holdPosition - body.worldCenterOfMass;
            Vector3 acceleration = offset * (followStrength * pullMultiplier) - (body.linearVelocity - targetVelocity) * followDamping;
            acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration * pullMultiplier);
            Vector3 nextVelocity = Vector3.ClampMagnitude(body.linearVelocity + acceleration * Time.fixedDeltaTime, maxHoldSpeed * pullMultiplier);
            body.AddForce((nextVelocity - body.linearVelocity) / Time.fixedDeltaTime, ForceMode.Acceleration);
            heldObject.RecordPreCollisionVelocity(nextVelocity, player.velocity);
        }

        private Vector3 GetTargetVelocity()
        {
            Vector3 velocity = Vector3.zero;
            if (targetInitialized)
                velocity = Vector3.ClampMagnitude((holdPosition - previousHoldPosition) / Time.fixedDeltaTime, maxHoldSpeed);

            targetInitialized = true;
            previousHoldPosition = holdPosition;
            return velocity;
        }

        private void OnDisable()
        {
            if (heldObject != null) ReleaseHeld(false);

            SetFocus(null);
            throwPrepareStartTime = float.NegativeInfinity;
            swingActiveUntil = float.NegativeInfinity;
            grabRequested = false;
            releaseRequested = false;
            throwRequested = false;
            attackRequested = false;
        }

        private PsychokinesisObject FindFocus()
        {
            int count = CollectTargetingCandidates(out Ray ray);
            PsychokinesisObject best = null;
            float bestScore = float.PositiveInfinity;
            float nearestDistance = float.PositiveInfinity;
            float aspect = aimCamera.aspect;
#if UNITY_EDITOR
            candidateCount = 0;
#endif

            for (int i = 0; i < count; i++)
            {
                Rigidbody body = queryColliders[i].attachedRigidbody;

                if (body == null ||
                    body.TryGetComponent(out PsychokinesisObject candidate) == false ||
                    candidate.isActiveAndEnabled == false)
                    continue;

                Vector3 position = GetTargetingPosition(queryColliders[i], ray);
                float distance = Vector3.Distance(player.transform.position, position);
                Vector3 viewport = aimCamera.WorldToViewportPoint(position);
                Vector2 delta = new Vector2((viewport.x - 0.5f) * aspect, viewport.y - 0.5f);
                float score = delta.magnitude;
                bool visible = HasLineOfSight(position, candidate.transform);
#if UNITY_EDITOR
                candidates[candidateCount++] = new TargetingCandidate
                {
                    Target = candidate,
                    CenterDistance = score,
                    PlayerDistance = distance,
                    Position = position,
                    Visible = visible
                };
#endif
                if (visible == false) continue;

                if (score < bestScore || (Mathf.Approximately(score, bestScore) && distance < nearestDistance))
                {
                    best = candidate;
                    bestScore = score;
                    nearestDistance = distance;
                }
            }

            return best;
        }

        // 카메라 광선 위에서 플레이어 깊이부터 pickupDistance까지를 타겟팅 구간으로 사용
        // 카메라와 플레이어 사이(플레이어 뒤쪽)는 타겟팅 범위로 사용하지 않음
        private void GetTargetingRange(Ray ray, out float start, out float end)
        {
            start = Mathf.Max(0f, Vector3.Dot(player.transform.position - ray.origin, ray.direction));
            end = start + pickupDistance;
        }

        private float GetTargetingDistance(Ray ray)
        {
            GetTargetingRange(ray, out _, out float end);
            return end;
        }

        private void GetTargetingBox(out Ray ray, out Vector3 center, out Vector3 halfExtents)
        {
            ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            GetTargetingRange(ray, out float start, out float end);
            center = ray.GetPoint((start + end) * 0.5f);
            halfExtents = new Vector3(castHalfWidth, castHalfWidth, pickupDistance * 0.5f);
        }

        private int CollectTargetingCandidates(out Ray ray)
        {
            GetTargetingBox(out ray, out Vector3 center, out Vector3 halfExtents);

            return Physics.OverlapBoxNonAlloc(
                center, halfExtents, queryColliders, aimCamera.transform.rotation,
                queryLayers, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetTargetingPosition(Collider target, Ray ray)
        {
            GetTargetingRange(ray, out float start, out float end);
            Ray rangeRay = new Ray(ray.GetPoint(start), ray.direction);

            if (target.Raycast(rangeRay, out RaycastHit hit, pickupDistance)) return hit.point;

            float depth = Mathf.Clamp(Vector3.Dot(target.bounds.center - ray.origin, ray.direction), start, end);
            return target.ClosestPoint(ray.GetPoint(depth));
        }

        private void NormalAttack()
        {
            if (Time.time < nextAttackTime) return;

            nextAttackTime = Time.time + attackInterval;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = GetTargetingDistance(ray);
            int count = Physics.RaycastNonAlloc(ray, obstructionHits, distance, queryLayers, QueryTriggerInteraction.Ignore);
            Collider hitCollider = null;

            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = obstructionHits[index];

                if (IsOwner(hit.collider) || hit.distance > distance) continue;

                distance = hit.distance;
                hitCollider = hit.collider;
            }

            Vector3 end = ray.GetPoint(distance);

            if (hitCollider != null && MonsterRegistry.TryGetMonster(hitCollider, out ICombatable target))
            {
                Debug.Log($"[기본 공격 요청] {target.GameObject.name}: Atk = {combatEntity.CurrentStats.Atk}", this);
                DamageInfo info = new DamageInfo(combatEntity.CurrentStats.Atk, end, hitCollider);
                DamageEvent damageEvent = new DamageEvent(combatEntity, target, info);
                CombatManager.Instance.AddCombatEvent(damageEvent);
            }

            Debug.DrawLine(ray.origin, end, Color.cyan, 0.15f);
        }

        private bool HasLineOfSight(Vector3 position, Transform target)
        {
            Vector3 origin = aimCamera.transform.position;
            Vector3 offset = position - origin;
            int count = Physics.RaycastNonAlloc(
                origin, offset.normalized, obstructionHits, offset.magnitude,
                queryLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = obstructionHits[i].collider;

                if (IsOwner(collider) == false && collider.transform.IsChildOf(target) == false)
                    return false;
            }

            return true;
        }

        private Vector3 GetHoldPosition()
        {
            Ray cameraRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float rotationSpeed = Vector3.Angle(previousCameraDirection, cameraRay.direction) / Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 previousHorizontal = Vector3.ProjectOnPlane(previousCameraDirection, Vector3.up);
            Vector3 currentHorizontal = Vector3.ProjectOnPlane(cameraRay.direction, Vector3.up);
            float yawSpeed = Vector3.Angle(previousHorizontal, currentHorizontal) / Mathf.Max(Time.deltaTime, 0.0001f);
#if UNITY_EDITOR
            horizontalRotationSpeed = yawSpeed;
#endif
            previousCameraDirection = cameraRay.direction;

            if (IsPreparingThrow == false && releaseRequested == false && yawSpeed >= swingActivationSpeed)
                swingActiveUntil = Time.time + swingActiveDuration;

            if (IsPreparingThrow)
                return GetHoldOrigin() + Vector3.Lerp(throwPrepareStartOffset,
                    GetCameraAlignedDirection(cameraRay, throwPrepareDistance) * throwPrepareDistance, GetThrowPrepareBlend());

            float lagAmount = Mathf.InverseLerp(swingRotationSpeed * 0.25f, swingRotationSpeed, rotationSpeed);
            float lag = Mathf.Lerp(SmallRotationLag, holdRotationLag, lagAmount);
            float blend = 1f - Mathf.Exp(-Time.deltaTime / lag);
            Vector3 aimDirection = GetCameraAlignedDirection(cameraRay, holdDistance);
            holdDirection = Vector3.Slerp(holdDirection, aimDirection, blend).normalized;
            holdDirection = Vector3.RotateTowards(aimDirection, holdDirection, maxHoldLagAngle * Mathf.Deg2Rad, 0f);
            return GetHoldOrigin() + holdDirection * holdDistance;
        }

        // 홀딩 기준점(GetHoldOrigin)을 중심으로 한 반지름 radius 구와 카메라 중앙 광선의 먼 쪽 교점을 향하는 방향
        // 카메라가 어깨 오프셋만큼 플레이어에서 떨어져 있어도 물체가 화면 중앙(Crosshair)에 오고, 플레이어와의 거리는 radius로 유지
        private Vector3 GetCameraAlignedDirection(Ray cameraRay, float radius)
        {
            Vector3 origin = GetHoldOrigin();
            Vector3 toRayOrigin = cameraRay.origin - origin;

            float b = Vector3.Dot(toRayOrigin, cameraRay.direction);
            float c = toRayOrigin.sqrMagnitude - radius * radius;
            float discriminant = b * b - c;

            // 카메라가 구 밖에 있어 광선이 구를 비켜 가는 경우에는 기존처럼 카메라 방향 사용
            if (discriminant < 0f) return cameraRay.direction;

            float distance = -b + Mathf.Sqrt(discriminant);
            if (distance <= 0f) return cameraRay.direction;

            return (cameraRay.GetPoint(distance) - origin).normalized;
        }

        private Vector3 GetHoldOrigin()
        {
            Bounds bounds = player.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y * 0.8f);
        }

        private float GetThrowPrepareBlend()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - throwPrepareStartTime) / throwPrepareDuration));
        }

        private bool IsOwner(Collider collider)
        {
            return collider.transform.IsChildOf(transform);
        }

        private void ReleaseHeld(bool isThrowing)
        {
            Vector3 velocity = Vector3.zero;

            if (isThrowing)
            {
                Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                float distance = ThrowAimDistance;
                int count = Physics.RaycastNonAlloc(ray, obstructionHits, ThrowAimDistance, queryLayers, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = obstructionHits[i];

                    if (IsOwner(hit.collider) == false && hit.rigidbody != heldObject.Body)
                        distance = Mathf.Min(distance, hit.distance);
                }

                Vector3 direction = ray.GetPoint(distance) - heldObject.Body.worldCenterOfMass;

                // 카메라와 물체 사이의 가까운 장애물을 조준해 역방향 발사 방지
                if (Vector3.Dot(direction, ray.direction) <= 0f) direction = ray.direction;

                velocity = direction.normalized * throwSpeed;
            }

            heldObject.Release(velocity, isThrowing, combatEntity);
            heldObject = null;
            swingActiveUntil = float.NegativeInfinity;
            throwRequested = false;
            throwPrepareStartTime = float.NegativeInfinity;
            targetInitialized = false;
        }

        private void SetFocus(PsychokinesisObject target)
        {
            if (focusedObject != target && focusedObject != null) focusedObject.SetFocused(false, true);

            focusedObject = target;

            if (focusedObject != null) focusedObject.SetFocused(true, focusedObject.IsAvailable(maxMass));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            GetTargetingBox(out _, out Vector3 center, out Vector3 halfExtents);
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Color originalColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(center, aimCamera.transform.rotation, Vector3.one);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.8f);
            Vector3 orbitCenter = GetHoldOrigin();
            Gizmos.DrawWireSphere(orbitCenter, holdDistance);
            Vector3 direction = heldObject != null ? holdDirection : aimCamera.transform.forward;
            Vector3 orbitPoint = orbitCenter + direction * holdDistance;
            Gizmos.DrawLine(orbitCenter, orbitPoint);
            Gizmos.DrawSphere(orbitPoint, 0.1f);
            if (heldObject != null)
                UnityEditor.Handles.Label(orbitPoint + Vector3.up * 0.3f,
                    $"Camera yaw: {horizontalRotationSpeed:F0} / {swingActivationSpeed:F0} deg/s\nSwing window: {(Time.time <= swingActiveUntil ? "Active" : "Inactive")}");
            Gizmos.matrix = originalMatrix;
            Gizmos.color = originalColor;
        }

        private GUIStyle debugLabelStyle;

        private void OnGUI()
        {
            if (UnityEditor.Handles.ShouldRenderGizmos() == false || Event.current.type != EventType.Repaint) return;

            debugLabelStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            Color originalColor = GUI.color;

            for (int index = 0; index < candidateCount; index++)
            {
                TargetingCandidate candidate = candidates[index];
                if (candidate.Target == null) continue;
                Vector3 screen = aimCamera.WorldToScreenPoint(candidate.Position);
                if (screen.z <= 0f) continue;
                bool selected = candidate.Target == focusedObject || candidate.Target == heldObject;
                GUI.color = selected ? Color.green : candidate.Visible ? Color.yellow : Color.gray;
                string state = selected ? "Selected" : candidate.Visible ? "Candidate" : "Blocked";
                GUI.Label(new Rect(screen.x + 12f, Screen.height - screen.y + 12f, 250f, 62f),
                    candidate.Target.name + " [" + state + "]\nCenter: " + candidate.CenterDistance.ToString("F3")
                    + " (lower wins)\nRange: " + candidate.PlayerDistance.ToString("F2") + " m", debugLabelStyle);
            }
            GUI.color = originalColor;
        }
#endif

        private void OnApplicationFocus(bool focused)
        {
            if (focused == false) releaseRequested = true;
        }
    }
}
