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
        [SerializeField, Min(0.1f)] private float castRadius = 1.8f;
        [SerializeField, Range(0.01f, 0.5f)] private float screenFocusRadius = 0.18f;
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
            public bool InRange;
            public bool InsideCylinder;
            public bool Visible;
            public bool InsideFocus;
        }

        private readonly TargetingCandidate[] candidates = new TargetingCandidate[128];
        private int candidateCount;
        private readonly Collider[] queryColliders = new Collider[128];
        private readonly RaycastHit[] obstructionHits = new RaycastHit[128];
        private PsychokinesisObject focusedObject;
        private PsychokinesisObject heldObject;
        private PsychokinesisTestEnemy focusedEnemy;
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
        public float CastRadius => castRadius;
        public float ScreenFocusRadius => screenFocusRadius;
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

            SetEnemyFocus(heldObject == null && Cursor.lockState == CursorLockMode.Locked ? FindAttackTarget() : null);

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
            SetEnemyFocus(null);
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
            int count = CollectTargetingCandidates(out Ray ray, out float length);
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

                bool duplicate = false;

                for (int j = 0; j < candidateCount; j++)
                    if (candidates[j].Target == candidate)
                    {
                        duplicate = true;
                        break;
                    }

                if (duplicate) continue;

                Vector3 position = candidate.FocusPosition;
                float distance = Vector3.Distance(player.transform.position, position);
                Vector3 viewport = aimCamera.WorldToViewportPoint(position);
                Vector2 delta = new Vector2((viewport.x - 0.5f) * aspect, viewport.y - 0.5f);
                float score = delta.magnitude;
                TargetingCandidate diagnostic = new TargetingCandidate
                {
                    Target = candidate,
                    CenterDistance = score,
                    PlayerDistance = distance,
                    InRange = distance <= pickupDistance,
                    InsideCylinder = IsInsideCylinder(position, ray, length),
                    Visible = HasLineOfSight(position, candidate.transform),
                    InsideFocus = viewport.z > 0f && score <= screenFocusRadius
                };
                candidates[candidateCount++] = diagnostic;

                if (diagnostic.InRange == false ||
                    diagnostic.InsideCylinder == false ||
                    diagnostic.Visible == false ||
                    diagnostic.InsideFocus == false)
                    continue;

                if (score < bestScore || (Mathf.Approximately(score, bestScore) && distance < nearestDistance))
                {
                    best = candidate;
                    bestScore = score;
                    nearestDistance = distance;
                }
            }

            return best;
        }

        private int CollectTargetingCandidates(out Ray ray, out float length)
        {
            ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            length = pickupDistance + Vector3.Distance(ray.origin, player.transform.position);
            // 원기둥을 감싸는 박스로 후보를 모은 뒤, 중심점의 축/반경 거리로 최종 판별
            Vector3 center = ray.GetPoint(length * 0.5f);
            Vector3 halfExtents = new Vector3(castRadius, castRadius, length * 0.5f);
            return Physics.OverlapBoxNonAlloc(center, halfExtents, queryColliders,
                aimCamera.transform.rotation, queryLayers, QueryTriggerInteraction.Ignore);
        }

        private bool IsInsideCylinder(Vector3 position, Ray ray, float length)
        {
            Vector3 fromStart = position - ray.origin;
            float axialDistance = Vector3.Dot(fromStart, ray.direction);
            Vector3 radialOffset = fromStart - ray.direction * axialDistance;
            return axialDistance >= 0f && axialDistance <= length
                && radialOffset.sqrMagnitude <= castRadius * castRadius;
        }

        private PsychokinesisTestEnemy FindAttackTarget()
        {
            int count = CollectTargetingCandidates(out Ray ray, out float length);
            PsychokinesisTestEnemy target = null;
            float bestScore = float.PositiveInfinity;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                PsychokinesisTestEnemy enemy = queryColliders[i].GetComponentInParent<PsychokinesisTestEnemy>();
                if (enemy == null || enemy.isActiveAndEnabled == false || enemy.IsDead) continue;

                Vector3 position = enemy.transform.position;
                float distance = Vector3.Distance(player.transform.position, position);
                if (distance > pickupDistance || IsInsideCylinder(position, ray, length) == false) continue;

                Vector3 viewport = aimCamera.WorldToViewportPoint(position);
                Vector2 delta = new Vector2((viewport.x - 0.5f) * aimCamera.aspect, viewport.y - 0.5f);
                float score = delta.magnitude;
                if (viewport.z <= 0f || score > screenFocusRadius || HasLineOfSight(position, enemy.transform) == false) continue;

                if (score < bestScore || (Mathf.Approximately(score, bestScore) && distance < nearestDistance))
                {
                    target = enemy;
                    bestScore = score;
                    nearestDistance = distance;
                }
            }

            return target;
        }

        private void SetEnemyFocus(PsychokinesisTestEnemy target)
        {
            if (focusedEnemy != target && focusedEnemy != null) focusedEnemy.SetFocused(false);

            focusedEnemy = target;

            if (focusedEnemy != null) focusedEnemy.SetFocused(true);
        }

        private void NormalAttack()
        {
            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackInterval;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Vector3 end = ray.GetPoint(pickupDistance + Vector3.Distance(ray.origin, player.transform.position));
            if (focusedEnemy != null)
            {
                focusedEnemy.TakeDamage(attackDamage);
                end = focusedEnemy.transform.position;
                if (focusedEnemy.IsDead) SetEnemyFocus(null);
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
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float length = pickupDistance + Vector3.Distance(ray.origin, player.transform.position);
            Vector3 end = ray.GetPoint(length);
            Vector3 axis = ray.direction.normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(axis, Vector3.right);
            side.Normalize();
            Vector3 up = Vector3.Cross(axis, side).normalized;

            Color originalColor = UnityEditor.Handles.color;
            UnityEditor.Handles.color = new Color(0.2f, 0.75f, 1f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(ray.origin, axis, castRadius);
            UnityEditor.Handles.DrawWireDisc(end, axis, castRadius);
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI / 4f;
                Vector3 radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                UnityEditor.Handles.DrawLine(ray.origin + radial * castRadius, end + radial * castRadius);
            }
            UnityEditor.Handles.color = originalColor;
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
                Vector3 screen = aimCamera.WorldToScreenPoint(candidate.Target.FocusPosition);
                if (screen.z <= 0f) continue;
                bool selected = candidate.Target == focusedObject || candidate.Target == heldObject;
                bool eligible = candidate.InRange && candidate.InsideCylinder && candidate.Visible && candidate.InsideFocus;
                GUI.color = selected ? Color.green : eligible ? Color.yellow : Color.gray;
                string state = selected ? "Selected" : candidate.InRange == false ? "Out of range" : candidate.InsideCylinder == false ? "Outside cylinder" : candidate.Visible == false
                    ? "Blocked" : candidate.InsideFocus == false ? "Outside focus" : "Candidate";
                float score = candidate.CenterDistance / screenFocusRadius;
                GUI.Label(new Rect(screen.x + 12f, Screen.height - screen.y + 12f, 250f, 62f),
                    candidate.Target.name + " [" + state + "]\nCenter: " + score.ToString("F3")
                    + " (lower wins; <= 1 inside)\nRange: " + candidate.PlayerDistance.ToString("F2") + " m", debugLabelStyle);
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
