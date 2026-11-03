using System;
using TeamOdd.Combat;
using TeamOdd.Mission;
using TeamOdd.UI;
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
        [SerializeField] private PsychokinesisForceWave forceWave;

        [Header("Targeting")]
        [SerializeField, Min(1f)] private float pickupDistance = 14f;
        [SerializeField, Min(0.1f)] private float castHalfWidth = 2f;

        [Header("Normal Attack")]
        [SerializeField, Min(0.01f)] private float attackInterval = 0.25f;
        [Tooltip("기본 공격의 무력화 피해를 현재 공격력에 비례해 계산한다.")]
        [SerializeField] private bool scaleAttackGroggy;
        [SerializeField, Min(0f)] private float attackGroggyScale = 0.27f;

        [Header("Object Damage Override")]
        [Tooltip("이 플레이어가 던지거나 휘두른 물체에 씬별 피해 설정을 적용한다.")]
        [SerializeField] private bool overrideObjectDamage;
        [Tooltip("중형·대형 등급의 최소 질량(kg), 각 기준 미만은 이전 등급으로 처리한다.")]
        [SerializeField] private Vector2 objectMassThresholds = new Vector2(4.5f, 9f);
        [Tooltip("등급별로 현재 공격력에 곱하는 피해 배율")]
        [SerializeField, Min(0f)] private float smallObjectAtkScale = 3f;
        [SerializeField, Min(0f)] private float mediumObjectAtkScale = 5f;
        [SerializeField, Min(0f)] private float largeObjectAtkScale = 7f;
        [Tooltip("등급별 피해에 곱하는 무력화 배율")]
        [SerializeField, Min(0f)] private float objectGroggyScale = 1.2f;

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
        private CombatManager attackEventManager;
        private int attackTargetFrame = -1;
        private bool hasAttackTarget;
        private float swingActiveUntil = float.NegativeInfinity;
        
        public PsychokinesisObject HeldObject => heldObject;
        public LayerMask QueryLayers => queryLayers;
        // 조준 중인(외곽선이 그려진) 오브젝트. 없으면 null
        public PsychokinesisObject FocusedObject => focusedObject;
        public bool GrabRequested => grabRequested;
        public bool ReleaseRequested => releaseRequested;

        public Vector3 Viewport => aimCamera.WorldToViewportPoint(new Vector3(0.5f, 0.5f));

        private bool IsPreparingThrow => throwPrepareStartTime >= 0f;
        private bool IsSuperpowerLocked => playerStatus != null && playerStatus.PlayerController != null && playerStatus.PlayerController.IsSuperpowerLocked;
        private bool IsHit => playerStatus != null && playerStatus.PlayerController != null && playerStatus.PlayerController.IsHit;
        // 정비소에서는 기본 공격과 염동력을 쓰지 않고, 우클릭은 NPC 상호작용(MissionNPC)에 넘긴다
        private bool IsInRepairShop => RepairShopZone.Contains(player.transform.position);

        public bool CanNormalAttack => heldObject == null && IsHit == false && IsSuperpowerLocked == false && IsInRepairShop == false;

        // 피격 순간 입력 예약과 발사 준비를 버리고, 물체는 투척 피해 없이 즉시 내려놓는다.
        public void InterruptForHit()
        {
            grabRequested = false;
            releaseRequested = false;
            throwRequested = false;
            attackRequested = false;
            throwPrepareStartTime = float.NegativeInfinity;
            swingActiveUntil = float.NegativeInfinity;
            targetInitialized = false;
            if (heldObject != null) ReleaseHeld(false);
            SetFocus(null);
            if (forceWave != null) forceWave.CancelPendingLaunches();
        }

        // 들 수 있는 최대 질량. 기본값(PlayerStats 에셋) + 증강칩 보너스라 칩을 끼우고 빼면 바로 바뀌므로 쓸 때마다 읽는다
        private int MaxMass => combatEntity.CurrentStats.Get(StatType.PsychoMaxMass);

        // 기본 공격 사거리. 최대 질량처럼 기본값(PlayerStats 에셋) + 증강칩 보너스라 쓸 때마다 읽는다
        private int AttackRange => combatEntity.CurrentStats.Get(StatType.AttackRange);

        // 플레이어의 공격(기본 공격, 투척, 휘두르기)이 실제로 피해를 줬을 때 (조준점 적중 표시용)
        public event Action AttackHit;

        // 지금 기본 공격을 하면 적에게 맞는지, 여러 곳에서 읽어도 프레임마다 한 번만 검사
        public bool HasAttackTarget
        {
            get
            {
                if (CanNormalAttack == false) return false;
                if (attackTargetFrame == Time.frameCount) return hasAttackTarget;

                attackTargetFrame = Time.frameCount;
                hasAttackTarget = heldObject == null && TryFindAttackTarget(out _, out _, out _, out _);
                return hasAttackTarget;
            }
        }

        public int CalculateAttackGroggy(int damage, int fallback)
            => scaleAttackGroggy ? Mathf.RoundToInt(damage * attackGroggyScale) : fallback;

        // mass는 크기 조절 전 질량(등급 판정용), sizeFactor는 크기 조절 칩으로 누적된 배율
        public bool TryCalculateObjectDamage(float mass, float sizeFactor, out int damage, out int groggy)
        {
            damage = 0;
            groggy = 0;
            if (overrideObjectDamage == false) return false;

            float atkScale;
            if (mass >= objectMassThresholds.y) atkScale = largeObjectAtkScale;
            else if (mass >= objectMassThresholds.x) atkScale = mediumObjectAtkScale;
            else atkScale = smallObjectAtkScale;
            // 평타처럼 공격력 칩 보너스까지 반영하도록 쓸 때마다 읽음
            damage = Mathf.Max(1, Mathf.RoundToInt(combatEntity.CurrentStats.Atk * atkScale * sizeFactor));
            groggy = Mathf.RoundToInt(damage * objectGroggyScale);
            return true;
        }

        private void Awake()
        {
            playerColliders = player.GetComponentsInChildren<Collider>();
            if (playerStatus == null) playerStatus = GetComponent<Player.Player>();
            // 프리팹에 붙여 두지 않았어도 공격은 되도록 붙임 (머티리얼이 없으면 연출만 생략)
            if (forceWave == null && TryGetComponent(out forceWave) == false) forceWave = gameObject.AddComponent<PsychokinesisForceWave>();
        }

        private void Update()
        {
            if (IsHit)
            {
                InterruptForHit();
                return;
            }

            // UI로 입력이 제한되거나 스킬 시전으로 행동이 잠기면 들고 있던 물체를 놓고 입력을 받지 않음
            if (UIManager.Instance.IsUIClose == false || IsSuperpowerLocked)
            {
                releaseRequested = true;
                grabRequested = false;
                throwRequested = false;
                attackRequested = false;

                return;
            }

            if (IsInRepairShop)
            {
                releaseRequested = true;
                grabRequested = false;
                throwRequested = false;
                attackRequested = false;

                if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
                    SystemMessage.Show(RepairShopZone.AttackBlockedMessage, SystemMessage.Tone.Warning);

                return;
            }

            bool rightHeld = SimpleInput.GetMouseButton(SimpleInput.MouseButton.Right) &&
                             Player.Player.Instance.PlayerController.IsHit == false;

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
#if UNITY_EDITOR
            candidateCount = 0;
#endif

            // Update에서 받은 입력도 같은 프레임에 UI가 열리거나 피격·시전이 시작되면 실행하지 않는다.
            // 정비소에 들어서면 들고 있던 물체를 놓고 외곽선도 끈다.
            if (IsHit || IsSuperpowerLocked || UIManager.Instance.IsUIClose == false || IsInRepairShop)
            {
                InterruptForHit();
                return;
            }

            if (heldObject == null)
                SetFocus(FindFocus());

            if (grabRequested)
            {
                if (heldObject == null && focusedObject != null && CanGrab(focusedObject) && HasGrabStamina())
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
            if (IsHit)
            {
                InterruptForHit();
                return;
            }

            if (heldObject != null && (releaseRequested || heldObject.IsHeld == false || heldObject.IsAvailable(MaxMass) == false))
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

        // 투척과 휘두르기 피해도 보낸 사람이 플레이어로 기록되므로, 실행된 피해 중 플레이어가 보낸 것을 모두 적중으로 판정
        private void Start()
        {
            attackEventManager = CombatManager.Instance;
            attackEventManager.OnDamageEventExecuted += HandleDamageExecuted;
        }

        private void OnDestroy()
        {
            if (attackEventManager != null) attackEventManager.OnDamageEventExecuted -= HandleDamageExecuted;
        }

        private PsychokinesisObject FindFocus()
        {
            int count = CollectTargetingCandidates(out Ray ray);
            PsychokinesisObject best = null;
            float bestScore = float.PositiveInfinity;
            float nearestDistance = float.PositiveInfinity;
            float aspect = aimCamera.aspect;
            Vector3 playerPosition = player.transform.position;
#if UNITY_EDITOR
            bool showCandidates = UnityEditor.Handles.ShouldRenderGizmos();
#endif

            for (int i = 0; i < count; i++)
            {
                Rigidbody body = queryColliders[i].attachedRigidbody;

                if (body == null ||
                    body.TryGetComponent(out PsychokinesisObject candidate) == false ||
                    candidate.isActiveAndEnabled == false)
                    continue;

                Vector3 position = GetTargetingPosition(queryColliders[i], ray);
                float distance = Vector3.Distance(playerPosition, position);
                Vector3 viewport = aimCamera.WorldToViewportPoint(position);
                Vector2 delta = new Vector2((viewport.x - 0.5f) * aspect, viewport.y - 0.5f);
                float score = delta.magnitude;
                bool isBetter = score < bestScore || (Mathf.Approximately(score, bestScore) && distance < nearestDistance);
                bool needsVisibilityCheck = isBetter;
#if UNITY_EDITOR
                needsVisibilityCheck = needsVisibilityCheck || showCandidates;
#endif
                if (needsVisibilityCheck == false) continue;

                bool visible = HasLineOfSight(position, candidate.transform);
#if UNITY_EDITOR
                if (showCandidates) candidates[candidateCount++] = new TargetingCandidate
                {
                    Target = candidate,
                    CenterDistance = score,
                    PlayerDistance = distance,
                    Position = position,
                    Visible = visible
                };
#endif
                if (visible && isBetter)
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

        // 기본 공격이 닿는 거리. 타겟팅 구간처럼 플레이어 깊이에서 시작하고, 길이는 물체 잡는 거리 대신 사거리 스탯을 쓴다
        private float GetAttackDistance(Ray ray)
        {
            GetTargetingRange(ray, out float start, out _);
            return start + AttackRange;
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

            // 조준·피해는 염동탄이 손을 떠나고 닿는 순간에 PsychokinesisForceWave가 처리
            forceWave.Fire();
        }

        // 화면 중앙으로 쏜 기본 공격이 가장 먼저 닿는 대상이 살아 있는 적인지 확인
        // 적이 아니어도 hitCollider·end에는 가장 먼저 닿은 곳(없으면 사거리 끝)을 돌려줌
        public bool TryFindAttackTarget(out Ray ray, out ICombatable target, out Collider hitCollider, out Vector3 end)
        {
            ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = GetAttackDistance(ray);
            int count = Physics.RaycastNonAlloc(ray, obstructionHits, distance, queryLayers, QueryTriggerInteraction.Ignore);
            hitCollider = null;
            target = null;

            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = obstructionHits[index];

                if (IsOwner(hit.collider) || hit.distance > distance) continue;

                distance = hit.distance;
                hitCollider = hit.collider;
            }

            end = ray.GetPoint(distance);

            if (hitCollider == null || MonsterRegistry.TryGetMonster(hitCollider, out target) == false) return false;
            return target is not CombatEntity entity || entity.IsDead == false;
        }

        private void HandleDamageExecuted(DamageEvent damageEvent, int receivedDamage)
        {
            if (receivedDamage > 0 && ReferenceEquals(damageEvent.Sender, combatEntity)) AttackHit?.Invoke();
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

            if (focusedObject != null) focusedObject.SetFocused(true, CanGrab(focusedObject));
        }

        // 염동 산탄 칩처럼 스킬이 이미 잡고 있는 오브젝트는 잡을 수 없음 (외곽선도 잡을 수 없는 색으로 표시)
        private bool CanGrab(PsychokinesisObject target)
        {
            return target.IsHeld == false && target.IsAvailable(MaxMass);
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
