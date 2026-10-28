using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Study.Utilities;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private static readonly int Walk = Animator.StringToHash("IsWalk");
        private static readonly int Run = Animator.StringToHash("IsRun");
        private static readonly int Rolling = Animator.StringToHash("Rolling");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int HitMotion = Animator.StringToHash("HitMotion");
        private static readonly int WakeUp = Animator.StringToHash("WakeUp");
        private static readonly int Dead = Animator.StringToHash("Dead");

        [Header("이동")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -9.81f;
        public float NeedRunStamina;

        [Header("방향")]
        [SerializeField] private Transform moveReference;
        [SerializeField] private bool rotateTowardMove = true;
        [SerializeField] private float turnSpeed = 720f;

        [Header("구르기")]
        [SerializeField] private float rollDistance = 4f;
        [SerializeField] private float rollDuration = 0.6f;
        public float NeedRollStamina;

        [SerializeField] private Animator animator;
        private readonly Dictionary<string, AnimationClip> clipDic = new();

        [Header("밀기")]
        [SerializeField, Min(0f)] private float pushForce = 150f;
        [SerializeField, Min(0.01f)] private float maxPushMass = 50f;
        [SerializeField, Range(0f, 1f)] private float minPushSpeedRatio = 0.3f;

        [Header("보스 위 미끄러짐")]
        [Tooltip("위에 올라서면 바깥으로 미끄러져 떨어지는 레이어(보스 몸)")]
        [SerializeField] private LayerMask slideOffLayers;
        [Tooltip("미끄러져 떨어지는 수평 속도(m/s)")]
        [SerializeField, Min(0f)] private float slideOffSpeed = 6f;
        [Tooltip("밟은 면의 법선 y가 이 값보다 크면 위에 올라선 것으로 봄, 옆면에 부딪힌 것은 제외")]
        [SerializeField, Range(0f, 1f)] private float slideOffMinNormalY = 0.3f;

        [Header("보스 몸 겹침")]
        [Tooltip("겹치면 바깥으로 빼내는 레이어(보스 부위, 다리 사이 막기 콜라이더)")]
        [SerializeField] private LayerMask bodyBlockLayers;

        [Header("홀딩")]
        [SerializeField, Range(0f, 90f)]
        private float holdBodyLeadAngle = 50f;

        [SerializeField, Range(0f, 90f)] private float armYawRange = 30f;
        [SerializeField, Min(0.1f)] private float holdTurnSpeed = 15f;
        [SerializeField, Min(0.1f)] private float armReach = 0.6f;
        [SerializeField, Range(0f, 1f)] private float armIkWeight = 0.55f;
        [SerializeField, Range(0f, 1f)] private float holdFingerBendScale = 0.5f;

        [Header("던지기 준비")]
        [SerializeField, Min(0.05f)]
        private float throwPrepareReach = 0.3f;
        [SerializeField, Range(-90f, 90f)] private float throwPrepareWristBend = -40f;
        // 팔꿈치가 굽는 방향, 플레이어 로컬 좌표, 기존 hint 객체를 추가하는 대신 오프셋 값으로 대체
        [SerializeField] private Vector3 elbowHintOffset = new(-0.786f, 1.283f, -0.111f);

        [Header("기본 공격")]
        // 손을 당겨 모으는 시간 → 앞으로 뻗는 시간 → 돌아오는 시간(초)
        [SerializeField, Min(0.01f)] private float attackWindup = 0.06f;
        [SerializeField, Min(0.01f)] private float attackThrust = 0.07f;
        [SerializeField, Min(0.01f)] private float attackRecover = 0.2f;
        // 뻗는 구간 중 염동탄이 손을 떠나는 지점(0: 뻗기 시작, 1: 다 뻗음)
        [SerializeField, Range(0f, 1f)] private float attackReleasePoint = 0.4f;
        // 마지막 공격 뒤 이 시간(초) 동안 조준 자세를 유지해 연타 중 팔이 내려갔다 올라오지 않게 함
        [SerializeField, Min(0f)] private float attackStanceDuration = 0.6f;
        [SerializeField, Min(0.1f)] private float attackReach = 0.75f;
        [SerializeField, Range(0f, 1f)] private float attackIkWeight = 0.85f;
        // 뻗을 때 손바닥이 앞을 보도록 손목을 젖히는 각도, 던지기 준비와 반대 방향
        [SerializeField, Range(-90f, 90f)] private float attackWristBend = 45f;
        [SerializeField, Min(0.1f)] private float attackBlendSpeed = 35f;
        [SerializeField, Min(1f)] private float attackAimDistance = 14f;

        [Header("피격")] 
        private float earlyWakeUpCooldown;
        public float MaxEarlyWakeUpCooldown;
        public float WakeUpTime;
        public float InvincibleTime; 
        // 피격 모션 끝 신호(EndHit)가 상태 전환 등으로 오지 않아도 행동 불가로 묶이지 않도록 기다리는 최대 시간(초)
        [SerializeField, Min(0.1f)] private float maxHitAnimationWait = 3f;
        public bool isHit;
        public bool CanWakeUp;
        private bool hitAnimationEnded;

        private CharacterController controller;
        private PsychokinesisController psychokinesisController;
        private Vector3 velocity;
        private Vector3 rollDir;
        private float moveX, moveZ;

        private float pushSpeedRatio = 1f; // 이번 프레임 Move 중 밀고 있는 물체로 계산
        private float appliedPushSpeedRatio = 1f; // 이번 프레임 이동에 적용 (직전 프레임 계산값)
        private Vector3 slideOffDirection; // Move 중 보스 몸 위에 올라선 것을 찾으면 정해지고, 다음 이동에서 한 번 쓰고 비움
        private readonly Collider[] bodyOverlaps = new Collider[16];

        private bool defaultRotateTowardMove;
        private bool isHolding;
        private bool isRestoringFacing;
        private float bodyTargetYaw;
        private float restoreTargetYaw;
        private float minimapYawOffset;
        private float ikBlend;
        private float throwPrepareBlend; // 0: 팔을 뻗은 홀딩 자세, 1: 던지기 준비 자세
        private Vector3 lastHandTarget;
        private Quaternion lastHandRotation;
        private Transform externalHoldTarget; // 염동 내려찍기처럼 물체가 아닌 대상을 붙잡는 스킬이 지정
        private float attackStartTime = float.NegativeInfinity;
        private float attackPushAtStart;
        private Sequence attackSequence;
        private float attackPull; // 0: 평소, 1: 손을 당겨 모음
        private float attackPush; // 0: 평소, 1: 앞으로 다 뻗음

        // 팔을 뻗을 대상, 스킬이 지정한 대상이 들고 있는 물체보다 우선
        private Transform HoldTarget => externalHoldTarget != null ? externalHoldTarget :
            heldObject != null ? heldObject.transform : null;

        // 기본 공격 직후, 들고 있는 물체가 없어도 조준 방향으로 팔을 뻗는 동안
        private bool IsAttackStance => Time.time < attackStartTime + attackStanceDuration;

        private Transform[] leftFingers;
        private Quaternion[] leftFingerRestRotations;

        public PsychokinesisObject heldObject;

        // 물체를 들고 있거나 스킬이 붙잡을 대상을 지정해 홀딩 자세인지 (카메라 줌인 등에 사용)
        public bool HasHoldTarget => HoldTarget != null;
        public float MinimapFacingYaw => transform.eulerAngles.y - minimapYawOffset;
        public bool IsGrounded => controller.isGrounded;
        public bool IsRolling { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsInvincible { get; private set; }

        // 있으면 구르기 속도를 커브로 제어, 없으면 rollDistance / rollDuration 고정 속도
        public PlayerRollCurve RollCurve { get; set; }

        public event Action OnRollRequested;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            psychokinesisController = GetComponent<PsychokinesisController>();
            animator = GetComponent<Animator>();

            earlyWakeUpCooldown = MaxEarlyWakeUpCooldown;
            
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

            foreach (AnimationClip clip in clips)
                clipDic.Add(clip.name, clip);
        }

        private void Start()
        {
            defaultRotateTowardMove = rotateTowardMove;
            CacheLeftFingerRestPose();
        }

        private void OnDisable()
        {
            StopAttackMotion();
        }

        private void Update()
        {
            earlyWakeUpCooldown = Mathf.Max(0f, earlyWakeUpCooldown - Time.deltaTime);
            
            if (isHit) return;

            UpdatePushSpeedRatio();
            UpdateHoldFacing();

            // 구르는 동안에는 일반 이동 및 입력 처리를 막습니다.
            if (IsRolling)
            {
                ExecuteRollMovement();
                IsInvincible = true;
                return;
            }

            UpdateMovement();
            WalkAnimation();
            CheckRollInput();

            heldObject = psychokinesisController.HeldObject;
            animator.SetLayerWeight(1, HoldTarget == null && IsAttackStance == false ? 0.0f : 1.0f);
        }

        private void UpdateMovement()
        {
            bool canRun = SimpleInput.GetKey(Key.LeftShift) && Player.Instance.Stamina >= NeedRunStamina;
            
            float speed = canRun ? runSpeed : walkSpeed;

            if (IsSprinting)
                Player.Instance.ConsumeStamina(NeedRunStamina * Time.deltaTime);

            Vector2 inputRaw = SimpleInput.GetMoveAxisRaw();
            
            IsSprinting = inputRaw != Vector2.zero && Mathf.Approximately(speed, runSpeed);

            moveX = SmoothAxis(moveX, inputRaw.x);
            moveZ = SmoothAxis(moveZ, inputRaw.y);

            if (moveReference == null) moveReference = Camera.main.transform;

            Transform reference = moveReference;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

            Vector3 moveVector = (right * moveX) + (forward * moveZ);

            // 기본 중력 처리
            if (IsGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }

            Vector3 totalMove = (moveVector * (speed * appliedPushSpeedRatio)) + new Vector3(0, velocity.y, 0);

            // 보스 발·등 위에 올라서 있으면 바깥으로 미끄러뜨려 떨어뜨림, 떨어지는 동안은 평소처럼 중력을 받음
            totalMove += slideOffDirection * slideOffSpeed;
            slideOffDirection = Vector3.zero;

            controller.Move(totalMove * Time.deltaTime);

            bool isMove = inputRaw != Vector2.zero;

            animator.SetBool(Walk, isMove);
            animator.SetBool(Run, IsSprinting);

            if (rotateTowardMove && moveVector.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation =
                    SimpleInput.GetMouseButton(SimpleInput.MouseButton.Right)
                        ? Quaternion.LookRotation(Vector3.forward)
                        : Quaternion.LookRotation(moveVector);
                transform.rotation =
                    Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        #region 애니메이션

        private void WalkAnimation()
        {
            Vector2 inputRaw = SimpleInput.GetMoveAxisRaw();

            bool isMove = inputRaw != Vector2.zero;
            bool isRun = IsSprinting;

            animator.SetBool(Walk, isMove);
            animator.SetBool(Run, isRun);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            UpdateHoldArmIK();
        }

        // 보스 부위가 애니메이션으로 움직인 뒤에 겹침을 검사
        private void LateUpdate()
        {
            PushOutOfBodies();
            ApplyHoldFingerBendScale();
        }

        #endregion

        #region 구르기

        private void CheckRollInput()
        {
            if (SimpleInput.GetKeyDown(Key.G) == false) return;

            OnRollRequested?.Invoke();
        }

        public void InputRolling()
        {
            if (IsRolling) return;

            IsRolling = true;

            // 구르기 방향 결정 (입력 방향 혹은 정면)
            Transform reference = moveReference != null ? moveReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            Vector3 inputDir = (right * moveX) + (forward * moveZ);

            rollDir = inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : transform.forward;

            if (RollCurve != null) RollCurve.BeginRoll();
            animator.SetTrigger(Rolling);
        }

        public void EndRoll()
        {
            IsRolling = false;
            IsInvincible = false;
        }

        private void ExecuteRollMovement()
        {
            // 커브 적용 전 코드
            // float rollSpeed = rollDistance / rollDuration;
            // Vector3 rollMove = (rollDir * rollSpeed) + new Vector3(0, gravity, 0);
            // controller.Move(rollMove * Time.deltaTime);

            var horizontal = RollCurve != null
                ? RollCurve.EvaluateVelocity(rollDir)
                : rollDir * (rollDistance / rollDuration);
            Vector3 rollMove = horizontal + new Vector3(0, gravity, 0);
            controller.Move(rollMove * Time.deltaTime);
        }

        #endregion

        #region 밀기

        private void UpdatePushSpeedRatio()
        {
            appliedPushSpeedRatio = pushSpeedRatio;
            pushSpeedRatio = 1f;
        }

        // CharacterController는 Rigidbody에 힘을 전달하지 못해 직접 물체를 밀어줌
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            TrySlideOff(hit);

            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic || body.mass >= maxPushMass) return;

            // 밟고 있는 물체는 밀지 않음
            if (hit.moveDirection.y < -0.3f) return;

            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;
            if (pushDir == Vector3.zero) return;

            pushSpeedRatio = Mathf.Min(pushSpeedRatio, Mathf.Lerp(1f, minPushSpeedRatio, body.mass / maxPushMass));

            // 물체가 이미 플레이어보다 빠르게 밀려나는 중이면 더 밀지 않음
            Vector3 playerVelocity = controller.velocity;
            float playerSpeed = new Vector3(playerVelocity.x, 0f, playerVelocity.z).magnitude;
            if (Vector3.Dot(body.linearVelocity, pushDir) >= playerSpeed) return;

            // 프레임레이트와 무관하도록 힘 * deltaTime을 충격량으로 전달
            body.AddForceAtPosition(pushDir * (pushForce * Time.deltaTime), hit.point, ForceMode.Impulse);
        }

        // 보스 몸 윗면을 밟았으면 몸 중심에서 바깥쪽을 미끄러질 방향으로 정함
        // 안쪽(다리 사이)으로 떨어뜨리면 보스가 때릴 수 없는 자리에 들어가므로 항상 바깥쪽
        private void TrySlideOff(ControllerColliderHit hit)
        {
            if ((slideOffLayers.value & (1 << hit.gameObject.layer)) == 0) return;
            if (hit.normal.y <= slideOffMinNormalY) return;

            Rigidbody body = hit.collider.attachedRigidbody;
            Vector3 center = body != null ? body.position : hit.collider.bounds.center;

            Vector3 direction = transform.position - center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;

            slideOffDirection = direction.normalized;
        }

        // CharacterController는 스스로 움직일 때만 막히고, 애니메이션·Kinematic 이동으로 다가온 보스 부위에는 밀려나지 않음
        // 한 번 겹치면 그 콜라이더를 무시하고 지나가 버리므로, 겹친 만큼 직접 바깥으로 빼냄
        // 위아래로 빼내면 발 위에 올라서거나 바닥에 묻히므로 수평으로만 밀고, Move로 밀어 벽은 뚫지 않음
        // 피격·구르기 중에도 겹치지 않아야 하므로 Update의 조기 반환과 관계없이 매 프레임 검사
        private void PushOutOfBodies()
        {
            if (bodyBlockLayers.value == 0 || controller.enabled == false) return;

            float radius = controller.radius;
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 offset = Vector3.up * Mathf.Max(controller.height * 0.5f - radius, 0f);
            int count = Physics.OverlapCapsuleNonAlloc(center + offset, center - offset, radius, bodyOverlaps,
                bodyBlockLayers, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;

            for (int index = 0; index < count; index++)
            {
                Collider other = bodyOverlaps[index];
                if (other == controller || other.transform.IsChildOf(transform)) continue;

                Transform otherTransform = other.transform;
                if (Physics.ComputePenetration(controller, transform.position, transform.rotation,
                        other, otherTransform.position, otherTransform.rotation, out Vector3 direction, out float distance) == false)
                    continue;

                push += GetHorizontalPush(other, direction, distance);
            }

            if (push != Vector3.zero) controller.Move(push);
        }

        // 빼낼 방향이 거의 위아래면(내려찍은 손 아래 등) 보스 몸 중심의 바깥쪽으로 밀어냄
        // 비스듬히 겹친 만큼 수평으로는 더 밀어야 빠지므로 수평 성분 비율로 나눔(한 프레임에 덜 빠져도 다음 프레임에 이어서 빠짐)
        private Vector3 GetHorizontalPush(Collider other, Vector3 direction, float distance)
        {
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            float horizontalRatio = horizontal.magnitude;

            if (horizontalRatio < 0.3f)
            {
                Rigidbody body = other.attachedRigidbody;
                horizontal = transform.position - (body != null ? body.position : other.bounds.center);
                horizontal.y = 0f;
                if (horizontal.sqrMagnitude < 0.0001f) horizontal = -transform.forward;

                return horizontal.normalized * distance;
            }

            return horizontal / horizontalRatio * (distance / horizontalRatio);
        }

        #endregion

        #region 홀딩

        // 들고 있는 물체 없이도 target 쪽으로 홀딩 자세와 팔 IK를 취함, 던지기 준비 자세는 쓰지 않음
        public void BeginExternalHold(Transform target) => externalHoldTarget = target;

        // 다른 스킬이 그사이 대상을 바꿨으면 그쪽을 유지
        public void EndExternalHold(Transform target)
        {
            if (externalHoldTarget == target) externalHoldTarget = null;
        }

        // 잡으면 조준이 몸 기준 왼쪽 끝에 오도록 몸을 holdBodyLeadAngle만큼 오른쪽으로 회전,
        // 조준이 거기서 오른쪽 armYawRange 안이면 팔만, 벗어나면 몸 회전, 놓으면 조준 방향으로 복원
        private void UpdateHoldFacing()
        {
            // heldObject는 Update 끝에서 갱신되므로, 같은 프레임의 홀딩 상태와 IK가 어긋나지 않게 먼저 갱신
            heldObject = psychokinesisController.HeldObject;

            // 잡기 입력(우클릭) 중이거나 홀딩 중에는 이동 방향 회전 대신 여기서 몸 방향을 정함
            bool aiming = HoldTarget != null || IsAttackStance || SimpleInput.GetMouseButton(SimpleInput.MouseButton.Right);
            rotateTowardMove = defaultRotateTowardMove && aiming == false;

            float aimYaw = Camera.main.transform.eulerAngles.y;
            float t = 1f - Mathf.Exp(-holdTurnSpeed * Time.deltaTime);

            // 구르면 공격 자세도 끊음
            if (IsRolling) StopAttackMotion();

            if ((HoldTarget == null && IsAttackStance == false) || IsRolling)
            {
                if (isHolding)
                {
                    isHolding = false;
                    isRestoringFacing = IsRolling == false;
                    restoreTargetYaw = aimYaw;
                }

                ikBlend = Mathf.Lerp(ikBlend, 0f, t);
                throwPrepareBlend = 0f;
                RestoreFacing(t);
                minimapYawOffset = Mathf.LerpAngle(minimapYawOffset, 0f, t);
                if (Mathf.Abs(minimapYawOffset) < 0.01f) minimapYawOffset = 0f;
                return;
            }

            if (isHolding == false)
            {
                isHolding = true;
                isRestoringFacing = false;
                bodyTargetYaw = aimYaw + holdBodyLeadAngle;
            }

            float armYaw = Mathf.DeltaAngle(bodyTargetYaw - holdBodyLeadAngle, aimYaw);

            if (armYaw < 0f) bodyTargetYaw = aimYaw + holdBodyLeadAngle;
            else if (armYaw > armYawRange) bodyTargetYaw = aimYaw + holdBodyLeadAngle - armYawRange;

            // 표시 방향은 같은 속도로 조준을 따라가고, 홀딩 자세 때문에 더 돌아간 몸 각도만 제외한다.
            float minimapYaw = Mathf.LerpAngle(MinimapFacingYaw, aimYaw, t);
            float bodyYaw = Mathf.LerpAngle(transform.eulerAngles.y, bodyTargetYaw, t);
            SetBodyYaw(bodyYaw);
            minimapYawOffset = Mathf.DeltaAngle(minimapYaw, bodyYaw);

            // 공격은 한 번 누르자마자 팔이 올라와야 하므로 더 빠르게 블렌드
            if (HoldTarget != null) ikBlend = Mathf.Lerp(ikBlend, armIkWeight, t);
            else ikBlend = Mathf.Lerp(ikBlend, attackIkWeight, 1f - Mathf.Exp(-attackBlendSpeed * Time.deltaTime));

            bool preparingThrow = heldObject != null &&
                externalHoldTarget == null && SimpleInput.GetMouseButton(SimpleInput.MouseButton.Left);
            throwPrepareBlend = Mathf.Lerp(throwPrepareBlend, preparingThrow ? 1f : 0f, t);
        }

        private void RestoreFacing(float t)
        {
            if (isRestoringFacing == false) return;

            // 이동 입력이 들어오면 이동 방향 회전에 맡김
            if (SimpleInput.GetMoveAxisRaw() != Vector2.zero)
            {
                isRestoringFacing = false;
                return;
            }

            float bodyYaw = Mathf.LerpAngle(transform.eulerAngles.y, restoreTargetYaw, t);
            SetBodyYaw(bodyYaw);

            if (Mathf.Abs(Mathf.DeltaAngle(bodyYaw, restoreTargetYaw)) < 0.5f) isRestoringFacing = false;
        }

        private void SetBodyYaw(float yaw) => transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // 홀딩 중 왼팔을 물체 쪽으로 뻗고, 던지기 준비 중에는 굽혀 당김. 놓으면 마지막 자세에서 서서히 해제
        // 기본 공격은 같은 팔 IK로 조준 방향을 향해 당겼다가 뻗음
        private void UpdateHoldArmIK()
        {
            // 같은 프레임 안에 대상이 사라졌으면(보스 제거 등) 놓은 것처럼 서서히 해제
            if (isHolding == false || TryGetArmTarget(out Vector3 armTarget) == false)
            {
                // IK 가중치는 다시 설정할 때까지 유지되므로 다 풀린 뒤에는 0으로 정리
                SetLeftHandIK(lastHandTarget, lastHandRotation, ikBlend > 0.01f ? ikBlend : 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                return;
            }

            Vector3 shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            Vector3 direction = (armTarget - shoulder).normalized;

            // IK 적용 전 애니메이션의 팔 방향에서 새 팔 방향으로 돌린 만큼 손도 돌려 손목 각도 유지
            Vector3 animatedArm = animator.GetIKPosition(AvatarIKGoal.LeftHand) - shoulder;
            Quaternion handRotation = Quaternion.FromToRotation(animatedArm, direction) *
                                      animator.GetIKRotation(AvatarIKGoal.LeftHand);

            // 공격의 당기는 자세는 던지기 준비 자세를 그대로 씀
            float pull = Mathf.Max(throwPrepareBlend, attackPull);
            float reach = Mathf.Lerp(Mathf.Lerp(armReach, throwPrepareReach, pull), attackReach, attackPush);
            float wristBend = throwPrepareWristBend * pull + attackWristBend * attackPush;

            lastHandTarget = shoulder + direction * reach;
            lastHandRotation = BendWrist(handRotation, direction, wristBend);
            SetLeftHandIK(lastHandTarget, lastHandRotation, ikBlend);

            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, transform.TransformPoint(elbowHintOffset));
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, pull * ikBlend);
        }

        // 들고 있는 대상이 우선, 없으면 공격 자세 동안 화면 중앙 조준 방향
        private bool TryGetArmTarget(out Vector3 position)
        {
            if (HoldTarget != null)
            {
                position = HoldTarget.position;
                return true;
            }

            if (IsAttackStance)
            {
                Transform aim = Camera.main.transform;
                position = aim.position + aim.forward * attackAimDistance;
                return true;
            }

            position = default;
            return false;
        }

        // 손목을 현재 꺾인 방향(팔 → 손가락)으로 angle만큼 더 꺾음, 음수면 반대 방향
        private Quaternion BendWrist(Quaternion handRotation, Vector3 armDirection, float angle)
        {
            Vector3 hand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
            Vector3 finger = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position;
            Vector3 bendAxis = Vector3.Cross(armDirection, finger - hand).normalized;

            return Quaternion.AngleAxis(angle, bendAxis) * handRotation;
        }

        private void SetLeftHandIK(Vector3 position, Quaternion rotation, float weight)
        {
            animator.SetIKPosition(AvatarIKGoal.LeftHand, position);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, rotation);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
        }

        // 애니메이션 적용 전(Start 시점) 손가락 회전을 기본 자세로 저장
        private void CacheLeftFingerRestPose()
        {
            int count = HumanBodyBones.LeftLittleDistal - HumanBodyBones.LeftThumbProximal + 1;
            leftFingers = new Transform[count];
            leftFingerRestRotations = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                leftFingers[i] = animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal + i);
                leftFingerRestRotations[i] = leftFingers[i].localRotation;
            }
        }

        // 홀딩 손가락 포즈가 과하게 꺾여서 애니메이션 적용 후 기본 자세 쪽으로 굽힘을 줄임 (손목은 그대로)
        private void ApplyHoldFingerBendScale()
        {
            if (ikBlend <= 0.01f) return;

            // 공격으로 뻗을 때는 손가락을 펴서 손바닥으로 미는 모양
            float keep = Mathf.Lerp(1f, holdFingerBendScale, ikBlend / armIkWeight) * (1f - attackPush);

            for (int i = 0; i < leftFingers.Length; i++)
                leftFingers[i].localRotation =
                    Quaternion.Slerp(leftFingerRestRotations[i], leftFingers[i].localRotation, keep);
        }

        #endregion

        #region 기본 공격

        // 손을 당겼다 앞으로 뻗는 동작을 시작하고, 염동탄이 손을 떠날 때까지의 시간(초)을 돌려줌
        public float PlayAttackMotion()
        {
            attackSequence?.Kill();
            attackPushAtStart = attackPush;
            attackStartTime = Time.time;
            attackPull = 0f;
            attackSequence = DOTween.Sequence()
                .Append(DOTween.To(() => 0f, SetAttackWindup, 1f, attackWindup).SetEase(Ease.Linear))
                .Append(DOTween.To(() => 0f, SetAttackThrust, 1f, attackThrust).SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => 0f, SetAttackRecovery, 1f, attackRecover).SetEase(Ease.Linear))
                .OnKill(() => attackSequence = null);
            return attackWindup + attackThrust * attackReleasePoint;
        }

        // 연타 시 현재 뻗은 정도에서 다시 당기고, 기존 SmoothStep 곡선을 유지한다.
        private void SetAttackWindup(float progress)
        {
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            attackPull = blend;
            attackPush = attackPushAtStart * (1f - blend);
        }

        private void SetAttackThrust(float progress)
        {
            attackPull = 1f - progress;
            attackPush = progress;
        }

        private void SetAttackRecovery(float progress)
        {
            attackPull = 0f;
            attackPush = 1f - Mathf.SmoothStep(0f, 1f, progress);
        }

        private void StopAttackMotion()
        {
            attackSequence?.Kill();
            attackSequence = null;
            attackStartTime = float.NegativeInfinity;
            attackPull = 0f;
            attackPush = 0f;
        }

        #endregion

        #region 피격

        // direction: 밀려날 수평 방향, 거리·높이·시간은 공격마다 정한 push를 따름
        // 피격 반응 중 다시 맞으면 피해만 받고 반응은 새로 시작하지 않음
        public void ApplyHitReaction(HitReaction reaction, Vector3 direction, HitPush push)
        {
            if (reaction == HitReaction.None || isHit) return;

            StopAttackMotion();

            StartCoroutine(PlayHitReaction(reaction, direction, push));
        }

        // 피격 모션의 EndHit 신호, 넉다운은 기상 모션 끝에서 옴
        public void EndHitAnimation() => hitAnimationEnded = true;

        private IEnumerator PlayHitReaction(HitReaction reaction, Vector3 direction, HitPush push)
        {
            isHit = true;
            hitAnimationEnded = false;
            // 지난 넉다운에서 조기 기상을 쓰지 않았어도 이번 넉다운의 기상 가능 시점(WakeUp 신호)부터 다시 허용
            CanWakeUp = false;

            animator.SetInteger(HitMotion, reaction == HitReaction.Knockdown ? 1 : 0);
            animator.SetTrigger(Hit);

            if (push.HasMovement)
            {
                direction.y = 0f;
                direction.Normalize();

                float t = 0f, prevY = 0f;

                while (t < 1f)
                {
                    float step = Mathf.Min(Time.deltaTime / push.duration, 1f - t);
                    t += step;
                    // 가운데(t = 0.5)에서 최고 높이 height에 닿는 포물선
                    float y = 4f * push.height * t * (1f - t);
                    Vector3 move = direction * (push.distance * step);
                    move.y = y - prevY;
                    prevY = y;
                    controller.Move(move); // 벽 충돌은 Move가 처리
                    yield return null;
                }
            }

            if (reaction == HitReaction.Knockdown)
            {
                float elapsed = 0f;
                while (elapsed < WakeUpTime)
                {
                    if (CanWakeUp && earlyWakeUpCooldown <= 0f && SimpleInput.GetKeyDown(Key.Space))
                    {
                        earlyWakeUpCooldown = MaxEarlyWakeUpCooldown;
                        CanWakeUp = false;
                        break;
                    }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            
                animator.SetTrigger(WakeUp);

                // 무적은 기상 시작부터 InvincibleTime 동안, 기상 모션이 먼저 끝나면 무적인 채로 행동 가능
                IsInvincible = true;
                float invincibleEndTime = Time.time + InvincibleTime;

                yield return WaitHitAnimationEnd();
                isHit = false;

                float remaining = invincibleEndTime - Time.time;
                if (remaining > 0f) yield return new WaitForSeconds(remaining);
                IsInvincible = false;
                yield break;
            }

            // 밀려나는 이동과 피격 모션이 모두 끝나야 행동 가능
            yield return WaitHitAnimationEnd();
            isHit = false;
        }

        private IEnumerator WaitHitAnimationEnd()
        {
            float elapsed = 0f;
            while (hitAnimationEnded == false && elapsed < maxHitAnimationWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        #endregion

        private static float SmoothAxis(float current, float target)
        {
            const float AXIS_SPEED = 3f;
            if (target != 0f && current * target < 0f) current = 0f;
            return Mathf.MoveTowards(current, target, AXIS_SPEED * Time.deltaTime);
        }

        // 애니메이션 클립(Run_N 등)에 포함된 애니메이션 이벤트 수신용. 없으면 "has no receiver" 에러가 발생합니다.
        private void OnFootstep(AnimationEvent animationEvent)
        {
            // 블렌드 중 비중이 낮은 클립의 이벤트는 무시
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            // TODO: 발소리 재생
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            // TODO: 착지 사운드 재생
        }
    }
}
