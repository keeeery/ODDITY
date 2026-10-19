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
        [SerializeField] private LayerMask queryLayers = Physics.DefaultRaycastLayers;

        [Header("Targeting")]
        [SerializeField, Min(1f)] private float pickupDistance = 14f;
        [SerializeField, Min(0.1f)] private float castHalfWidth = 2f;
        [SerializeField, Min(0f)] private float maxMass = 20f;

        [Header("Normal Attack")]
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.01f)] private float attackInterval = 0.25f;

        [Header("Holding and Throwing")]
        [SerializeField, Min(1f)] private float holdDistance = 8f;
        [SerializeField, Min(0.05f)] private float holdClearance = 0.25f;
        [SerializeField, Min(1f)] private float followStrength = 75f;
        [SerializeField, Min(0f)] private float followDamping = 18f;
        [SerializeField, Min(1f)] private float maxAcceleration = 150f;
        [SerializeField, Min(1f)] private float maxHoldSpeed = 18f;
        [SerializeField, Min(1f)] private float breakDistance = 16f;
        [SerializeField, Min(1f)] private float throwSpeed = 25f;
        [SerializeField, Min(1f)] private float aimDistance = 100f;

        public struct TargetingCandidate
        {
            public PsychokinesisObject Target;
            public float CenterDistance;
            public float PlayerDistance;
            public Vector3 Position;
            public bool Visible;
        }

        private readonly TargetingCandidate[] candidates = new TargetingCandidate[128];
        private int candidateCount;
        private readonly Collider[] queryColliders = new Collider[128];
        private readonly RaycastHit[] obstructionHits = new RaycastHit[128];
        private PsychokinesisObject focusedObject;
        private PsychokinesisObject heldObject;
        private Vector3 holdPosition;
        private Vector3 previousHoldPosition;
        private Vector3 targetVelocity;
        private bool targetInitialized;
        private bool grabRequested;
        private bool releaseRequested;
        private bool throwRequested;
        private bool attackRequested;
        private float nextAttackTime;

        public PsychokinesisObject FocusedObject => focusedObject;
        public PsychokinesisObject HeldObject => heldObject;
        public Camera AimCamera => aimCamera;
        public CharacterController Player => player;
        public float PickupDistance => pickupDistance;
        public float CastHalfWidth => castHalfWidth;
        public int CandidateCount => candidateCount;
        public TargetingCandidate GetCandidate(int index) => candidates[index];

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
                else if (rightHeld) throwRequested = true;
            }
        }

        private void LateUpdate()
        {
            PsychokinesisObject target = FindFocus();

            if (heldObject == null)
                SetFocus(Cursor.lockState == CursorLockMode.Locked ? target : null);

            if (grabRequested)
            {
                if (heldObject == null && focusedObject != null && focusedObject.IsAvailable(maxMass))
                {
                    heldObject = focusedObject;
                    heldObject.BeginHold();
                    SetFocus(null);
                    heldObject.SetFocused(true, true);
                    targetInitialized = false;
                }

                grabRequested = false;
            }

            if (attackRequested)
            {
                if (heldObject == null) NormalAttack();

                attackRequested = false;
            }

            if (heldObject != null) holdPosition = GetHoldPosition();
        }

        private void FixedUpdate()
        {
            if (heldObject != null && (heldObject.IsHeld == false || heldObject.IsAvailable(maxMass) == false))
                ReleaseHeld(false);

            if (heldObject != null && (releaseRequested || throwRequested))
                ReleaseHeld(throwRequested && releaseRequested == false);

            releaseRequested = false;
            throwRequested = false;

            if (heldObject == null) return;

            Rigidbody body = heldObject.Body;

            if (Vector3.Distance(player.transform.position, body.position) > breakDistance)
            {
                ReleaseHeld(false);

                return;
            }

            if (targetInitialized == false)
            {
                targetVelocity = Vector3.zero;
                targetInitialized = true;
            }
            else
            {
                Vector3 measuredVelocity = (holdPosition - previousHoldPosition) / Time.fixedDeltaTime;
                targetVelocity = Vector3.Lerp(targetVelocity, Vector3.ClampMagnitude(measuredVelocity, maxHoldSpeed), 0.35f);
            }

            previousHoldPosition = holdPosition;

            Vector3 offset = holdPosition - body.worldCenterOfMass;
            Vector3 acceleration = offset * followStrength - (body.linearVelocity - targetVelocity) * followDamping;
            acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);
            Vector3 nextVelocity = Vector3.ClampMagnitude(body.linearVelocity + acceleration * Time.fixedDeltaTime, maxHoldSpeed);
            body.AddForce((nextVelocity - body.linearVelocity) / Time.fixedDeltaTime, ForceMode.Acceleration);
        }

        private void OnDisable()
        {
            if (heldObject != null) ReleaseHeld(false);

            SetFocus(null);
            grabRequested = false;
            releaseRequested = false;
            throwRequested = false;
            attackRequested = false;
        }

        public void Configure(Camera camera, CharacterController character)
        {
            aimCamera = camera;
            player = character;
        }

        private PsychokinesisObject FindFocus()
        {
            int count = CollectTargetingCandidates(out Ray ray);
            PsychokinesisObject best = null;
            float bestScore = float.PositiveInfinity;
            float nearestDistance = float.PositiveInfinity;
            float aspect = aimCamera.aspect;
            candidateCount = 0;

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
                TargetingCandidate diagnostic = new TargetingCandidate
                {
                    Target = candidate,
                    CenterDistance = score,
                    PlayerDistance = distance,
                    Position = position,
                    Visible = HasLineOfSight(position, candidate.transform)
                };
                candidates[candidateCount++] = diagnostic;

                if (diagnostic.Visible == false) continue;

                if (score < bestScore || (Mathf.Approximately(score, bestScore) && distance < nearestDistance))
                {
                    best = candidate;
                    bestScore = score;
                    nearestDistance = distance;
                }
            }

            return best;
        }

        private void GetTargetingBox(out Ray ray, out Vector3 center, out Vector3 halfExtents)
        {
            ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float length = pickupDistance + Vector3.Distance(ray.origin, player.transform.position);
            center = ray.GetPoint(length * 0.5f);
            halfExtents = new Vector3(castHalfWidth, castHalfWidth, length * 0.5f);
        }

        private int CollectTargetingCandidates(out Ray ray)
        {
            GetTargetingBox(out ray, out Vector3 center, out Vector3 halfExtents);
            return Physics.OverlapBoxNonAlloc(center, halfExtents, queryColliders,
                aimCamera.transform.rotation, queryLayers, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetTargetingPosition(Collider target, Ray ray)
        {
            GetTargetingBox(out _, out _, out Vector3 halfExtents);
            float length = halfExtents.z * 2f;
            if (target.Raycast(ray, out RaycastHit hit, length)) return hit.point;

            float depth = Mathf.Clamp(Vector3.Dot(target.bounds.center - ray.origin, ray.direction), 0f, length);
            return target.ClosestPoint(ray.GetPoint(depth));
        }

        private void NormalAttack()
        {
            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackInterval;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = pickupDistance + Vector3.Distance(ray.origin, player.transform.position);
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
            if (hitCollider != null)
            {
                PsychokinesisTestEnemy enemy = hitCollider.GetComponentInParent<PsychokinesisTestEnemy>();
                if (enemy != null && enemy.isActiveAndEnabled) enemy.TakeDamage(attackDamage);
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
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Bounds playerBounds = player.bounds;
            float safeRadius = playerBounds.extents.magnitude + heldObject.CollisionRadius + holdClearance;
            float minimumDistance = Vector3.Dot(playerBounds.center - ray.origin, ray.direction) + safeRadius;

            return ray.GetPoint(Mathf.Max(holdDistance, minimumDistance));
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
                float distance = aimDistance;
                int count = Physics.RaycastNonAlloc(ray, obstructionHits, aimDistance, queryLayers, QueryTriggerInteraction.Ignore);
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

            heldObject.Release(velocity);
            heldObject = null;
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
                bool eligible = candidate.Visible;
                GUI.color = selected ? Color.green : eligible ? Color.yellow : Color.gray;
                string state = selected ? "Selected" : candidate.Visible ? "Candidate" : "Blocked";
                float score = candidate.CenterDistance;
                GUI.Label(new Rect(screen.x + 12f, Screen.height - screen.y + 12f, 250f, 62f),
                    candidate.Target.name + " [" + state + "]\nCenter: " + score.ToString("F3")
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
