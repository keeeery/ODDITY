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
        private static readonly int HitDir = Animator.StringToHash("HitDir");
        private static readonly int WakeUp = Animator.StringToHash("WakeUp");
        private static readonly int Dead = Animator.StringToHash("Dead");
        private static readonly int KnockdownState = Animator.StringToHash("Knockdown Blend Tree");
        private static readonly int WakeupState = Animator.StringToHash("Wakeup Blend Tree");
        private const float UpperLayerFadeDuration = 0.25f; // 대기 자세로 돌아갈 때 들기·조준 상체 레이어를 내리는 시간
        private const string HoldLayerName = "Psycokinesis"; // 들기·조준 상체 레이어

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
        // 커브 Y = 1일 때 속도가 rollDistance / (Roll 길이 * moveEndTime), 실제 거리는 rollDistance * 커브 넓이
        [SerializeField] private float rollDistance = 4f;
        [SerializeField, Range(0.05f, 1f)] private float moveEndTime = 0.7f; // 이 진행도 이후로는 이동하지 않음
        // X: Roll 진행도 0 ~ moveEndTime을 0~1로 늘린 값, Y: 속도 배율
        [SerializeField] private AnimationCurve speedCurve = new();
        [SerializeField] private string rollStateName = "Roll";
        [SerializeField] private string rollClipName = "Unarmed-DiveRoll-Forward1"; // 첫 구르기 전 길이 계산용
        public float NeedRollStamina;
        private int rollStateHash;
        private int holdLayer; // 들기·조준 상체 레이어 번호 (이름으로 찾음)
        private bool hasEnteredRollState; // 이번 구르기에서 Roll 상태를 한 번이라도 거쳤는지

        [SerializeField] private Animator animator;

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
        [Tooltip("겹친 몸에서 빠져나가는 속도(m/s), 달리기보다 빨라야 함")]
        [SerializeField, Min(0.1f)] private float bodyEscapeSpeed = 8f;
        [Tooltip("겹친 보스 부위가 이 속도(m/s)보다 빠르면 지나가는 중으로 보고 빼내지 않음")]
        [SerializeField, Min(0f)] private float bodyPassingSpeed = 1f;
        [Tooltip("겹친 보스 부위가 이 시간(초) 동안 멈춰 있어야 멈춘 것으로 보고 빼냄")]
        [SerializeField, Min(0f)] private float bodyStopTime = 0.2f;
        [Tooltip("보스 부위와 이 시간(초)보다 오래 겹치면 옆으로 비켜섬")]
        [SerializeField, Min(0f)] private float bodySidestepDelay = 0.3f;
        [Tooltip("끼였을 때 몸 밖으로 빠져나갈 자리를 찾는 최대 거리(m)")]
        [SerializeField, Min(0.5f)] private float bodyEscapeSearchDistance = 4f;
        [Tooltip("빠져나가는 길에 있으면 막힌 방향으로 보는 레이어(벽 등)")]
        [SerializeField] private LayerMask bodyEscapeBlockLayers = 1 << 7;

        [Header("홀딩")]
        [SerializeField, Range(0f, 90f)]
        private float holdBodyLeadAngle = 50f;

        [Tooltip("제자리에서 물체를 들 때 조준보다 오른쪽으로 더 돌리는 몸 각도")]
        [SerializeField, Range(0f, 90f)] private float standingHoldLeadAngle = 15f;
        [SerializeField, Range(0f, 90f)] private float armYawRange = 30f;
        [SerializeField, Min(0.1f)] private float holdTurnSpeed = 15f;
        // 몸을 되돌리지 않고 홀딩·공격을 끝낼 때 미니맵 방향이 몸 방향으로 돌아가는 빠르기
        [SerializeField, Min(0.1f)] private float minimapReturnSpeed = 6f;
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

        [Header("기본 공격 방향")]
        // 몸 정면 기준 왼팔이 닿는 조준 범위, 벗어난 만큼만 몸을 돌림
        [SerializeField, Range(0f, 180f)] private float attackArmLeftRange = 40f;
        [SerializeField, Range(0f, 180f)] private float attackArmRightRange = 20f;
        [SerializeField, Min(0.1f)] private float attackTurnSpeed = 25f; // 팔이 뻗기 전에 몸이 따라오도록 빠르게
        // 범위 안에서는 몸 대신 척추·머리를 조준 쪽으로 비틂
        [SerializeField, Range(0f, 1f)] private float attackLookBodyWeight = 0.3f;
        [SerializeField, Range(0f, 1f)] private float attackLookHeadWeight = 0.6f;
        [SerializeField, Min(0.1f)] private float attackLookBlendSpeed = 12f;

        [Header("피격")]
        private float earlyWakeUpCooldown;
        public float MaxEarlyWakeUpCooldown;
        public float WakeUpTime;
        public float InvincibleTime; 
        // 피격 모션 끝 신호(EndHit)가 상태 전환 등으로 오지 않아도 행동 불가로 묶이지 않도록 기다리는 최대 시간(초)
        [SerializeField, Min(0.1f)] private float maxHitAnimationWait = 3f;
        // 넉백·넉다운 등 피격 반응 중이거나 죽은 뒤, 이동·공격·염동력·스킬 쪽이 이 값을 보고 행동을 막는다.
        public bool IsHit
        {
            get => isHit || isDead;
            set => isHit = value;
        }
        public bool CanWakeUp;
        private bool isHit;
        private bool hitAnimationEnded;

        private bool isDead = false;

        private CharacterController controller;
        private PsychokinesisController psychokinesisController;
        private Vector3 velocity;
        private Coroutine hitRoutine;
        private float savedHitStepOffset;
        private bool hasHitStepOffset;
        private Vector3 rollDir;
        private float moveX, moveZ;

        private float pushSpeedRatio = 1f; // 이번 프레임 Move 중 밀고 있는 물체로 계산
        private float appliedPushSpeedRatio = 1f; // 이번 프레임 이동에 적용 (직전 프레임 계산값)
        private Vector3 slideOffDirection; // Move 중 보스 몸 위에 올라선 것을 찾으면 정해지고, 다음 이동에서 한 번 쓰고 비움
        private readonly Collider[] bodyOverlaps = new Collider[16];
        private Vector3 escapeDirection;        // 끼임에서 빠져나가는 중인 방향, 몸에서 다 빠져나오면 비움
        private Vector3 blockedEscapeDirection; // 벽 등에 막혀 나아가지 못한 방향, 다음 탐색에서 뺌
        private static readonly Vector3 NoBodyHistory = Vector3.positiveInfinity; // 처음 겹쳐 직전 위치가 없는 부위
        private readonly Dictionary<Collider, Vector3> overlappedBodies = new(); // 충돌을 꺼 둔 겹친 보스 부위와 직전 위치
        private readonly HashSet<Collider> touchedBodies = new(); // 이번 검사에서 겹친 부위
        private readonly List<Collider> separatedBodies = new();
        private Dictionary<Collider, Vector3> nearBodies = new(); // skinWidth 안에 들어온(충돌은 켜 둔) 보스 부위와 직전 위치
        private readonly List<Transform> passThroughBodies = new(); // 막지도 밀어내지도 않고 통과하는 몸(분신 등)의 루트
        private Dictionary<Collider, Vector3> nextNearBodies = new();
        private bool isPushingOut; // 겹친 몸에서 빠져나가는 중
        private float bodyStillTime; // 겹친 보스 부위가 모두 멈춰 있던 시간(초)
        private float bodyOverlapTime; // 보스 부위와 계속 겹친 시간(초)
        private Vector3 sidestepDirection; // 비켜선 수평 방향, 다음 Update의 걷기 모션·회전에 씀

        private bool defaultRotateTowardMove;
        private bool isHolding;
        private bool isRestoringFacing;
        private float bodyTargetYaw;
        private float appliedBodyLead = -1f; // bodyTargetYaw에 반영된 몸 회전 오프셋, 음수면 미정
        private bool turnedForHold; // 홀딩 때문에 몸을 돌렸는지, 돌렸을 때만 끝나고 되돌림
        private float attackLookBlend; // 기본 공격 중 척추·머리 비틀기 정도
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
        private Tween upperLayerFade;
        private float attackPull; // 0: 평소, 1: 손을 당겨 모음
        private float attackPush; // 0: 평소, 1: 앞으로 다 뻗음

        // 팔을 뻗을 대상, 스킬이 지정한 대상이 들고 있는 물체보다 우선
        private Transform HoldTarget => externalHoldTarget != null ? externalHoldTarget :
            heldObject != null ? heldObject.transform : null;

        // 기본 공격 직후, 들고 있는 물체가 없어도 조준 방향으로 팔을 뻗는 동안
        private bool IsAttackStance => playerTime < attackStartTime + attackStanceDuration;

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
        public bool IsDead => isDead;
        // 염동 내려찍기처럼 시전 중 이동·구르기·공격·다른 스킬과 카메라 회전을 막는 동안
        public bool IsActionLocked { get; private set; }
        // 이동·구르기·시점은 그대로 두고 염동력·스킬만 막는 동안(보스 연출로 플레이어만 느려질 때 등), 행동 잠금도 포함
        public bool IsSuperpowerLocked => IsActionLocked || isSuperpowerLocked;
        private bool isSuperpowerLocked;

        // 마지막으로 확인한 Roll 상태 길이(초, 상태 Speed 반영)
        public float RollStateLength { get; private set; } = 1f;

        // true인 동안 걷기·달리기로 움직이지 않는다. 구르기는 된다 (염동 투사처럼 누르고 있는 스킬을 쓰는 중)
        // 입력 방향은 계속 읽어 두므로 구를 때는 누르고 있는 방향으로 구른다
        public bool IsMoveBlocked { get; set; }

        public event Action OnRollRequested;

        // 월드 시간에 플레이어 슬로모션 배율을 곱한 프레임 시간, 마무리 일격 뒤 월드가 돌아와도 플레이어는 느린 채로 남는다.
        private static float PlayerDeltaTime => Time.deltaTime * SlowMotion.PlayerRelativeScale;
        private float appliedAnimatorTimeScale = 1f; // 지금 animator.speed에 곱해 둔 플레이어 배율
        private float playerTime; // 플레이어 배율로 흐르는 시계, 공격 자세·무적처럼 시간을 재는 곳에 Time.time 대신 씀
        
        private bool isUIClose => UIManager.Instance.IsUIClose;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            psychokinesisController = GetComponent<PsychokinesisController>();
            animator = GetComponent<Animator>();

            earlyWakeUpCooldown = MaxEarlyWakeUpCooldown;

            rollStateHash = Animator.StringToHash(rollStateName);
            holdLayer = animator.GetLayerIndex(HoldLayerName);
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name != rollClipName) continue;
                RollStateLength = clip.length;
                break;
            }
        }

        private void Start()
        {
            defaultRotateTowardMove = rotateTowardMove;
            CacheLeftFingerRestPose();
        }

        private void OnDisable()
        {
            ApplyAnimatorTimeScale(1f);
            StopAttackMotion();
            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                IsInvincible = false;
            }
            hitRoutine = null;
            FinishHitReaction();
            RestoreBodyCollisions();
        }

        private void Update()
        {
            IgnoreOverlappedBodies();

            // 조작이 돌아오면 상체 레이어는 다시 매 프레임 여기서 정한다
            upperLayerFade?.Kill();
            float relativeScale = SlowMotion.PlayerRelativeScale;
            ApplyAnimatorTimeScale(relativeScale);
            if (attackSequence != null) attackSequence.timeScale = relativeScale;
            UpdateWakeUpRootMotion();

            // 죽은 뒤에도 슬로모션 배율은 애니메이터에 계속 적용하고, 이동·입력은 처리하지 않는다.
            if (isDead) return;

            playerTime += PlayerDeltaTime;
            earlyWakeUpCooldown = Mathf.Max(0f, earlyWakeUpCooldown - PlayerDeltaTime);
            
            if (IsHit) return;

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
            FaceSidestep();
            CheckRollInput();

            Debug.Log("UI Close pc"+isUIClose);
            
            heldObject = psychokinesisController.HeldObject;
            animator.SetLayerWeight(holdLayer, HoldTarget == null && IsAttackStance == false ? 0.0f : 1.0f);
        }

        private void UpdateMovement()
        {
            bool canRun = SimpleInput.GetKey(Key.LeftShift) && Player.Instance.Stamina >= NeedRunStamina;
            
            float speed = canRun ? runSpeed : walkSpeed;

            if (IsSprinting)
                Player.Instance.ConsumeStamina(NeedRunStamina * PlayerDeltaTime);

            Vector2 inputRaw = ReadMoveInput();
            
            IsSprinting = inputRaw != Vector2.zero && Mathf.Approximately(speed, runSpeed) && isUIClose;

            moveX = SmoothAxis(moveX, inputRaw.x);
            moveZ = SmoothAxis(moveZ, inputRaw.y);

            if (moveReference == null) moveReference = Camera.main.transform;

            Transform reference = moveReference;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

            Vector3 moveVector = (right * moveX) + (forward * moveZ);

            // 중력은 그대로 받도록 수평 이동만 없앤다
            if (IsMoveBlocked)
            {
                moveVector = Vector3.zero;
                IsSprinting = false;
            }

            // 기본 중력 처리
            if (IsGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            else
            {
                velocity.y += gravity * PlayerDeltaTime;
            }

            Vector3 totalMove = (moveVector * (speed * appliedPushSpeedRatio)) + new Vector3(0, velocity.y, 0);

            // 보스 발·등 위에 올라서 있으면 바깥으로 미끄러뜨려 떨어뜨림, 떨어지는 동안은 평소처럼 중력을 받음
            totalMove += slideOffDirection * slideOffSpeed;
            slideOffDirection = Vector3.zero;

            if (isUIClose) controller.Move(totalMove * PlayerDeltaTime);

            if (rotateTowardMove && moveVector.sqrMagnitude > 0.0001f && isUIClose)
            {
                Quaternion targetRotation =
                    heldObject != null
                        ? Quaternion.LookRotation(Vector3.forward)
                        : Quaternion.LookRotation(moveVector);
                transform.rotation =
                    Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * PlayerDeltaTime);
            }
        }

        #region 애니메이션

        // 행동이 잠긴 동안은 이동 입력이 없는 것으로 봄, 중력은 그대로 받음
        private Vector2 ReadMoveInput() => IsActionLocked ? Vector2.zero : SimpleInput.GetMoveAxisRaw();

        private void WalkAnimation()
        {
            Vector2 inputRaw = ReadMoveInput();

            // 걷기·달리기가 막혀 있으면 걷는 모션도 내지 않는다. 달리기는 막혀 있으면 IsSprinting이 이미 false
            // 보스 몸에서 비켜서는 동안은 미끄러져 보이지 않게 걷기 모션을 냄
            bool isMove = (inputRaw != Vector2.zero && IsMoveBlocked == false && isUIClose) || sidestepDirection != Vector3.zero;
            bool isRun = IsSprinting;

            animator.SetBool(Walk, isMove);
            animator.SetBool(Run, isRun);
        }

        // 입력 없이 비켜설 때 그쪽을 보고 걷게 함, 들기·조준 중에는 방향을 지키고 걷기 모션만 냄
        private void FaceSidestep()
        {
            if (sidestepDirection == Vector3.zero || ReadMoveInput() != Vector2.zero) return;
            if (rotateTowardMove == false || HoldTarget != null) return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(sidestepDirection), turnSpeed * PlayerDeltaTime);
        }

        public void DeathAnimation()
        {
            animator.SetTrigger(Dead);
            UIManager.Instance.LockCursor(false, true);
            isDead = true;
        }
        
        private void OnAnimatorIK(int layerIndex)
        {
            // 상체 비틀기는 Base Layer, 팔 IK는 들기 레이어에서만 (Base Layer도 발 IK로 IK Pass를 씀)
            if (layerIndex == 0) UpdateAttackLookAt();

            if (layerIndex != holdLayer) return;

            UpdateHoldArmIK();
        }

        // 기상 모션은 기어가며 자리를 옮기므로, 기상하는 동안만 루트 모션을 켜 수평 이동을 캡슐에 반영
        // (끄면 대기로 돌아갈 때 캡슐 자리로 끌려와 위치가 튐)
        private void UpdateWakeUpRootMotion()
        {
            if (animator == null) return;

            int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            int next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
            animator.applyRootMotion = isDead == false && (current == WakeupState || next == WakeupState);
        }

        // 루트 모션의 수평 이동만 CharacterController로 적용, 컨트롤러가 꺼진 동안(되감기)은 무시
        private void OnAnimatorMove()
        {
            if (animator == null || animator.applyRootMotion == false || controller == null || controller.enabled == false) return;

            Vector3 delta = animator.deltaPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0f) controller.Move(delta);
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
            if (IsActionLocked || IsHit || SimpleInput.GetKeyDown(Key.Space) == false) return;

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

            hasEnteredRollState = false;
            animator.SetTrigger(Rolling);
        }

        public void EndRoll()
        {
            IsRolling = false;
            IsInvincible = false;
        }

        private void ExecuteRollMovement()
        {
            float normalizedTime = ReadRollNormalizedTime(out bool found);
            if (found) hasEnteredRollState = true;

            Vector3 horizontal = rollDir * GetSpeedAt(normalizedTime);
            Vector3 rollMove = horizontal + new Vector3(0, gravity, 0);
            controller.Move(rollMove * PlayerDeltaTime);
        }

        // Roll 상태 진행도에서의 속도(m/s)
        public float GetSpeedAt(float normalizedTime)
        {
            if (normalizedTime < 0f || normalizedTime >= moveEndTime) return 0f;

            float moveSeconds = RollStateLength * moveEndTime;
            if (moveSeconds <= 0f) return 0f;

            // 커브 넓이로 나누지 않으므로 커브 값이 곧 기준 속도(rollDistance / moveSeconds)의 배율
            return rollDistance * Mathf.Max(0f, speedCurve.Evaluate(normalizedTime / moveEndTime)) / moveSeconds;
        }

        private float ReadRollNormalizedTime(out bool found)
        {
            found = true;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == rollStateHash)
            {
                RollStateLength = current.length;
                return current.normalizedTime;
            }

            // 다른 상태에서 Roll로 블렌딩되는 중이면 넘어가는 대상의 진행도를 사용
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == rollStateHash)
                {
                    RollStateLength = next.length;
                    return next.normalizedTime;
                }
            }

            found = false;

            // Roll 상태를 이미 빠져나갔으면 이동 끝 (EndRolling보다 전이가 먼저 끝나는 경우)
            if (hasEnteredRollState) return 1f;

            // 트리거는 켰지만 애니메이터가 아직 처리하지 않은 프레임은 시작 속도로 이동
            return 0f;
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
            body.AddForceAtPosition(pushDir * (pushForce * PlayerDeltaTime), hit.point, ForceMode.Impulse);
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

        // 걸어 들어가면 막히고, 보스가 움직여 들어오면 통과시키며 겹친 동안만 그 부위와 충돌을 꺼 둠
        // 피격 중에는 밀지 않고, 이동 입력·보스 정지·오래 겹침 중 하나면 빼내 몸 안이 안전지대가 되지 않게 함
        // 금방 지나가는 다리·꼬리는 통과, 오래 겹치면 보스 진행 방향의 옆으로 비켜섬
        // 위아래로 빼내면 발 위에 올라서거나 바닥에 묻히므로 수평으로만 밀고, Move로 밀어 벽은 뚫지 않음
        // 피격·구르기 중에도 충돌 토글은 이어야 하므로 Update의 조기 반환과 관계없이 매 프레임 검사
        // 머리와 팔 사이처럼 양쪽에서 감싼 부위에 끼이면 밀어내는 방향이 서로 상쇄되므로, 몸 밖으로 가장 가깝게 빠지는 방향을 찾아 다 빠질 때까지 밀어냄
        private void PushOutOfBodies()
        {
            sidestepDirection = Vector3.zero;
            if (bodyBlockLayers.value == 0 || controller.enabled == false) return;

            float radius = controller.radius;
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 offset = Vector3.up * Mathf.Max(controller.height * 0.5f - radius, 0f);
            int count = OverlapBodies(center, offset, GetBodyContactRadius());

            Vector3 push = Vector3.zero;
            Vector3 bodyCenterSum = Vector3.zero;
            float largestPush = 0f;
            int pushCount = 0;
            bool isBodyPassing = false;
            Vector3 passMove = Vector3.zero;
            touchedBodies.Clear();

            for (int index = 0; index < count; index++)
            {
                Collider other = bodyOverlaps[index];
                if (IsOwnCollider(other)) continue;

                if (TryGetBodyPenetration(other, out Vector3 direction, out float distance) == false)
                {
                    // skinWidth 안에만 들어온 부위는 빼내지 않고, 멈추면 충돌을 다시 켬
                    if (overlappedBodies.ContainsKey(other) && TrackOverlappedBody(other, out _) == false) touchedBodies.Remove(other);
                    continue;
                }

                isBodyPassing |= TrackOverlappedBody(other, out Vector3 bodyMove);
                passMove += bodyMove;

                Vector3 single = GetHorizontalPush(other, direction, distance);
                push += single;
                largestPush = Mathf.Max(largestPush, single.magnitude);
                bodyCenterSum += other.attachedRigidbody != null ? other.attachedRigidbody.position : other.bounds.center;
                pushCount++;
            }

            RestoreSeparatedBodies();

            bodyStillTime = isBodyPassing ? 0f : bodyStillTime + Time.deltaTime;

            bodyOverlapTime += Time.deltaTime;

            if (pushCount == 0 || IsHit)
            {
                isPushingOut = false;
                bodyStillTime = 0f;
                bodyOverlapTime = 0f;
                escapeDirection = Vector3.zero;
                blockedEscapeDirection = Vector3.zero;
                return;
            }

            if (isPushingOut == false)
                isPushingOut = IsTryingToMove || bodyStillTime >= bodyStopTime || bodyOverlapTime >= bodySidestepDelay;
            if (isPushingOut == false) return;

            // 한 번 빠져나가기 시작하면 남은 한 부위가 다른 부위 쪽으로 되밀어 다시 끼이지 않도록 몸에서 다 빠질 때까지 같은 방향으로 감
            bool isPinched = pushCount >= 2 && push.magnitude < largestPush * 0.5f;
            if (isPinched || escapeDirection != Vector3.zero)
            {
                EscapeFromBodies(center, offset, radius, bodyCenterSum / pushCount);
                sidestepDirection = escapeDirection;
                return;
            }

            escapeDirection = Vector3.zero;
            blockedEscapeDirection = Vector3.zero;

            // 깊이 겹쳐도 한 프레임에 튀지 않게 속도로 나눠 밂
            Vector3 step = isBodyPassing
                ? GetSidestepDirection(push, passMove, bodyCenterSum / pushCount) * (bodyEscapeSpeed * PlayerDeltaTime)
                : Vector3.ClampMagnitude(push, bodyEscapeSpeed * PlayerDeltaTime);
            controller.Move(step);

            step.y = 0f;
            sidestepDirection = step.sqrMagnitude > 0.000001f ? step.normalized : Vector3.zero;
        }

        // 앞으로 밀면 보스에 끌려가므로 진행 방향 성분을 빼고 옆으로 비켜섬
        // 옆 성분이 없으면 몸 중심 기준 플레이어 쪽, 그것도 없으면 진행 방향 오른쪽
        private Vector3 GetSidestepDirection(Vector3 push, Vector3 passMove, Vector3 bodyCenter)
        {
            passMove.y = 0f;
            if (passMove.sqrMagnitude < 0.000001f) return push.normalized;

            Vector3 passDirection = passMove.normalized;
            Vector3 side = Vector3.ProjectOnPlane(push, passDirection);

            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.ProjectOnPlane(transform.position - bodyCenter, passDirection);
                if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(Vector3.up, passDirection);
            }

            side.y = 0f;
            return side.normalized;
        }

        // CharacterController는 Move 중 겹친 콜라이더 밖으로 스스로 밀려나므로, 움직이기 전에 보스가 밀고 들어온 부위의 충돌을 먼저 끔
        // skinWidth 띠 안에서 움직이는 부위도 끄고, 띠 안에서 멈춘 부위는 걸어와 닿은 것일 수 있어 켜 둠
        private void IgnoreOverlappedBodies()
        {
            if (bodyBlockLayers.value == 0 || controller.enabled == false) return;

            float radius = controller.radius;
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 offset = Vector3.up * Mathf.Max(controller.height * 0.5f - radius, 0f);
            int count = OverlapBodies(center, offset, GetBodyContactRadius());

            nextNearBodies.Clear();
            for (int index = 0; index < count; index++)
            {
                Collider other = bodyOverlaps[index];
                if (IsOwnCollider(other) || overlappedBodies.ContainsKey(other)) continue;

                Vector3 current = other.transform.position;
                bool isPenetrating = TryGetBodyPenetration(other, out _, out _);
                bool isMovingNear = nearBodies.TryGetValue(other, out Vector3 last) && IsBodyMoving(current - last);

                if (isPenetrating || isMovingNear)
                {
                    Physics.IgnoreCollision(controller, other, true);
                    overlappedBodies[other] = NoBodyHistory;
                    continue;
                }

                nextNearBodies[other] = current;
            }

            (nearBodies, nextNearBodies) = (nextNearBodies, nearBodies);
        }

        // 반경 + skinWidth에 transform·물리 위치 차이만큼 여유를 더함
        private float GetBodyContactRadius() => controller.radius + controller.skinWidth + 0.05f;

        // 보스는 월드 시간으로 움직이므로 월드 시간으로 잼
        private bool IsBodyMoving(Vector3 moved)
        {
            float deltaTime = Time.deltaTime;
            return deltaTime > 0f && moved.magnitude > bodyPassingSpeed * deltaTime;
        }

        private bool IsOwnCollider(Collider other) => other == controller || other.transform.IsChildOf(transform);

        // 통과하는 몸의 부위는 충돌을 끄고 결과에서 빼서, 밀어내기·충돌 토글 대상이 되지 않게 함
        private int OverlapBodies(Vector3 center, Vector3 offset, float radius)
        {
            int count = Physics.OverlapCapsuleNonAlloc(center + offset, center - offset, radius, bodyOverlaps,
                bodyBlockLayers, QueryTriggerInteraction.Ignore);

            for (int index = count - 1; index >= 0; index--)
            {
                if (IsPassThroughBody(bodyOverlaps[index]) == false) continue;

                Physics.IgnoreCollision(controller, bodyOverlaps[index], true);
                bodyOverlaps[index] = bodyOverlaps[--count];
            }

            return count;
        }

        private bool IsPassThroughBody(Collider other)
        {
            for (int index = passThroughBodies.Count - 1; index >= 0; index--)
            {
                Transform root = passThroughBodies[index];
                if (root == null) passThroughBodies.RemoveAt(index);
                else if (other.transform.IsChildOf(root)) return true;
            }

            return false;
        }

        private bool TryGetBodyPenetration(Collider other, out Vector3 direction, out float distance)
        {
            direction = Vector3.zero;
            distance = 0f;
            if (IsOwnCollider(other)) return false;

            Transform otherTransform = other.transform;
            return Physics.ComputePenetration(controller, transform.position, transform.rotation,
                other, otherTransform.position, otherTransform.rotation, out direction, out distance);
        }

        // 구르기나 (막히지 않은) 걷기 입력으로 스스로 움직이려 하는지
        private bool IsTryingToMove => IsRolling || (ReadMoveInput() != Vector2.zero && IsMoveBlocked == false);

        // 겹친 부위의 충돌을 끄고, 지나가는 중인지와 직전 프레임부터 움직인 양을 돌려줌 (처음 겹친 부위는 지나가는 중으로 침)
        private bool TrackOverlappedBody(Collider other, out Vector3 moved)
        {
            touchedBodies.Add(other);
            moved = Vector3.zero;

            // bounds는 물리 스텝마다만 갱신되므로 매 프레임 반영되는 transform으로 잼
            Vector3 current = other.transform.position;
            bool isPassing = true;

            if (overlappedBodies.TryGetValue(other, out Vector3 last) == false)
            {
                Physics.IgnoreCollision(controller, other, true);
            }
            else if (float.IsPositiveInfinity(last.x) == false) // Vector3 ==는 무한대끼리 같다고 보지 않음
            {
                moved = current - last;
                isPassing = Time.deltaTime <= 0f || IsBodyMoving(moved);
            }

            overlappedBodies[other] = current;
            return isPassing;
        }

        // 이번 검사에서 겹치지 않은 부위의 충돌을 되돌림
        private void RestoreSeparatedBodies()
        {
            separatedBodies.Clear();
            foreach (Collider body in overlappedBodies.Keys)
            {
                if (touchedBodies.Contains(body) == false) separatedBodies.Add(body);
            }

            foreach (Collider body in separatedBodies)
            {
                if (body != null) Physics.IgnoreCollision(controller, body, false);
                overlappedBodies.Remove(body);
            }
        }

        private void RestoreBodyCollisions()
        {
            nearBodies.Clear();
            touchedBodies.Clear();
            RestoreSeparatedBodies();
            isPushingOut = false;
        }

        private void EscapeFromBodies(Vector3 center, Vector3 offset, float radius, Vector3 bodyCenter)
        {
            if (escapeDirection == Vector3.zero) escapeDirection = FindEscapeDirection(center, offset, radius, bodyCenter);
            if (escapeDirection == Vector3.zero) return;

            Vector3 step = escapeDirection * (bodyEscapeSpeed * PlayerDeltaTime);
            Vector3 before = transform.position;
            controller.Move(step);

            // 벽 등에 막혀 거의 못 나아갔으면 그 방향은 빼고 다음 프레임에 다시 찾음
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            if (moved.magnitude >= step.magnitude * 0.3f) return;

            blockedEscapeDirection = escapeDirection;
            escapeDirection = Vector3.zero;
        }

        // 수평 16방향 중 몸에서 가장 가깝게 빠지는 방향, 거리가 비슷하면 몸 중심의 바깥쪽을 고름
        private Vector3 FindEscapeDirection(Vector3 center, Vector3 offset, float radius, Vector3 bodyCenter)
        {
            const int DirectionCount = 16;

            Vector3 outward = transform.position - bodyCenter;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : -transform.forward;

            Vector3 best = Vector3.zero;
            float bestScore = float.PositiveInfinity;

            for (int index = 0; index < DirectionCount; index++)
            {
                Vector3 direction = Quaternion.AngleAxis(360f / DirectionCount * index, Vector3.up) * Vector3.forward;
                if (blockedEscapeDirection != Vector3.zero && Vector3.Dot(direction, blockedEscapeDirection) > 0.9f) continue;
                if (TryGetClearDistance(center, offset, radius, direction, out float distance) == false) continue;

                float score = distance - Vector3.Dot(direction, outward) * 0.5f;
                if (score >= bestScore) continue;

                bestScore = score;
                best = direction;
            }

            return best;
        }

        // 이 방향으로 몇 m 가야 몸에서 완전히 빠지는지, 가는 길에 벽이 있거나 탐색 거리 안에서 못 빠지면 false
        private bool TryGetClearDistance(Vector3 center, Vector3 offset, float radius, Vector3 direction, out float distance)
        {
            const float Step = 0.25f;
            float probeRadius = radius * 0.95f; // 바닥·옆 물체에 스치기만 한 것은 겹침으로 보지 않음

            for (distance = Step; distance <= bodyEscapeSearchDistance; distance += Step)
            {
                Vector3 probe = center + direction * distance;
                if (Physics.CheckCapsule(probe + offset, probe - offset, probeRadius, bodyEscapeBlockLayers, QueryTriggerInteraction.Ignore))
                    return false;
                if (Physics.CheckCapsule(probe + offset, probe - offset, probeRadius, bodyBlockLayers, QueryTriggerInteraction.Ignore) == false)
                    return true;
            }

            return false;
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

        public void SetActionLocked(bool locked) => IsActionLocked = locked;

        public void SetSuperpowerLocked(bool locked) => isSuperpowerLocked = locked;

        // 보상창처럼 조작을 멈추는 화면이 뜰 때 걷기·달리기 입력을 끊어, 재생 중인 동작(구르기·피격 등)이 끝나면
        // 애니메이터의 전환을 따라 자연스럽게 대기 자세로 돌아가게 함. 강제로 대기 상태로 넘기지 않는다.
        // 피격·공격 정리는 컴포넌트가 꺼질 때(OnDisable) 하므로, 조작을 끈 뒤에 부르면 남는 동작이 없다.
        public void EnterIdle()
        {
            if (animator == null) return;

            ApplyAnimatorTimeScale(1f);
            moveX = 0f;
            moveZ = 0f;
            IsSprinting = false;
            if (IsRolling) EndRoll();

            animator.SetBool(Walk, false);
            animator.SetBool(Run, false);
            animator.ResetTrigger(Rolling);
            animator.ResetTrigger(Hit);

            // 들기·조준 상체 레이어는 끊지 않고 서서히 내린다
            upperLayerFade?.Kill();
            upperLayerFade = DOTween.To(() => animator.GetLayerWeight(holdLayer), weight => animator.SetLayerWeight(holdLayer, weight), 0f, UpperLayerFadeDuration)
                .SetTarget(this);

            if (TryGetComponent(out CombatEntity entity) && entity.IsDead) return;

            // 넉다운 중이면 기상 신호를 주던 피격 코루틴이 꺼졌으므로 여기서 일으킨다
            bool knockedDown = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == KnockdownState
                               || animator.GetNextAnimatorStateInfo(0).shortNameHash == KnockdownState;
            if (knockedDown) animator.SetTrigger(WakeUp);
        }

        private void ApplyAnimatorTimeScale(float scale)
        {
            if (animator == null || Mathf.Approximately(scale, appliedAnimatorTimeScale)) return;

            animator.speed = animator.speed / appliedAnimatorTimeScale * scale;
            appliedAnimatorTimeScale = scale;
        }

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
            float t = 1f - Mathf.Exp(-holdTurnSpeed * PlayerDeltaTime);

            // 구르면 공격 자세도 끊음
            if (IsRolling) StopAttackMotion();

            if ((HoldTarget == null && IsAttackStance == false) || IsRolling)
            {
                if (isHolding)
                {
                    isHolding = false;
                    isRestoringFacing = IsRolling == false && turnedForHold;
                    restoreTargetYaw = aimYaw;
                    turnedForHold = false;
                    appliedBodyLead = -1f;
                }

                ikBlend = Mathf.Lerp(ikBlend, 0f, t);
                throwPrepareBlend = 0f;
                BlendAttackLook(false);
                // 몸을 되돌리는 중이면 몸과 같은 속도로, 아니면 미니맵이 몸 방향으로 천천히 돌아가게 줄임
                float minimapT = isRestoringFacing ? t : 1f - Mathf.Exp(-minimapReturnSpeed * PlayerDeltaTime);
                RestoreFacing(t);
                minimapYawOffset = Mathf.LerpAngle(minimapYawOffset, 0f, minimapT);
                if (Mathf.Abs(minimapYawOffset) < 0.01f) minimapYawOffset = 0f;
                return;
            }

            isHolding = true;
            bool standing = (ReadMoveInput() == Vector2.zero || IsMoveBlocked) && sidestepDirection == Vector3.zero;

            // 물체 없이 기본 공격만 할 때는 몸 회전 오프셋을 주지 않음
            bool attackOnly = HoldTarget == null;
            BlendAttackLook(attackOnly);

            // 기본 공격은 팔 범위 안이면 상체만 비틀고 벗어난 만큼만 몸을 돌림, 끝나도 되돌리지 않음
            if (attackOnly)
            {
                appliedBodyLead = -1f;
                isRestoringFacing = false;

                float bodyYaw = transform.eulerAngles.y;
                float aimFromBody = Mathf.DeltaAngle(bodyYaw, aimYaw); // +면 조준이 몸 오른쪽
                float targetYaw = bodyYaw;
                if (aimFromBody > attackArmRightRange) targetYaw = aimYaw - attackArmRightRange;
                else if (aimFromBody < -attackArmLeftRange) targetYaw = aimYaw + attackArmLeftRange;

                SetBodyYaw(Mathf.LerpAngle(bodyYaw, targetYaw, 1f - Mathf.Exp(-attackTurnSpeed * PlayerDeltaTime)));

                // 미니맵은 홀딩처럼 조준 방향을 따라가 홀딩·공격 전환에도 끊기지 않음
                float attackMinimapYaw = Mathf.LerpAngle(MinimapFacingYaw, aimYaw, t);
                minimapYawOffset = Mathf.DeltaAngle(attackMinimapYaw, transform.eulerAngles.y);
            }
            else
            {
                // 제자리에서 물체를 들면 조금만, 움직이면 팔이 닿도록 크게 돌림
                float bodyLead = standing ? standingHoldLeadAngle : holdBodyLeadAngle;

                // 오프셋이 바뀌면 새 오프셋 기준으로 다시 맞춤
                if (Mathf.Approximately(bodyLead, appliedBodyLead) == false)
                {
                    isRestoringFacing = false;
                    appliedBodyLead = bodyLead;
                    bodyTargetYaw = aimYaw + bodyLead;
                }

                // 서 있으면 조준을 그대로 따라가고, 움직일 때는 팔 회전 범위를 벗어날 때만 몸을 돌림
                float armYaw = Mathf.DeltaAngle(bodyTargetYaw - bodyLead, aimYaw);

                if (standing) bodyTargetYaw = aimYaw + bodyLead;
                else if (armYaw < 0f) bodyTargetYaw = aimYaw + bodyLead;
                else if (armYaw > armYawRange) bodyTargetYaw = aimYaw + bodyLead - armYawRange;

                // 표시 방향은 같은 속도로 조준을 따라가고, 홀딩 자세 때문에 더 돌아간 몸 각도만 제외한다.
                float minimapYaw = Mathf.LerpAngle(MinimapFacingYaw, aimYaw, t);
                float bodyYaw = Mathf.LerpAngle(transform.eulerAngles.y, bodyTargetYaw, t);
                SetBodyYaw(bodyYaw);
                minimapYawOffset = Mathf.DeltaAngle(minimapYaw, bodyYaw);
                turnedForHold = true;
            }

            // 공격은 한 번 누르자마자 팔이 올라와야 하므로 더 빠르게 블렌드
            if (HoldTarget != null) ikBlend = Mathf.Lerp(ikBlend, armIkWeight, t);
            else ikBlend = Mathf.Lerp(ikBlend, attackIkWeight, 1f - Mathf.Exp(-attackBlendSpeed * PlayerDeltaTime));

            bool preparingThrow = heldObject != null &&
                externalHoldTarget == null && SimpleInput.GetMouseButton(SimpleInput.MouseButton.Left);
            throwPrepareBlend = Mathf.Lerp(throwPrepareBlend, preparingThrow ? 1f : 0f, t);
        }

        private void BlendAttackLook(bool active)
        {
            float speed = 1f - Mathf.Exp(-attackLookBlendSpeed * PlayerDeltaTime);
            attackLookBlend = Mathf.Lerp(attackLookBlend, active ? 1f : 0f, speed);
            if (attackLookBlend < 0.001f) attackLookBlend = 0f;
        }

        // 카메라 조준점을 바라보게 해 척추·머리를 조준 쪽으로 비틂
        private void UpdateAttackLookAt()
        {
            // 피격·사망 중에는 블렌드를 갱신하는 Update가 멈추므로 여기서 끔
            if (attackLookBlend <= 0f || isDead || IsHit || Camera.main == null)
            {
                animator.SetLookAtWeight(0f);
                return;
            }

            Transform cam = Camera.main.transform;
            animator.SetLookAtPosition(cam.position + cam.forward * attackAimDistance);
            animator.SetLookAtWeight(attackLookBlend, attackLookBodyWeight, attackLookHeadWeight, 0f, 0.5f);
        }

        private void RestoreFacing(float t)
        {
            if (isRestoringFacing == false) return;

            // 이동 입력이 들어오면 이동 방향 회전에 맡김
            if (ReadMoveInput() != Vector2.zero)
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
            if (IsHit)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                return;
            }

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
            attackStartTime = playerTime;
            attackPull = 0f;
            attackSequence = DOTween.Sequence()
                .Append(DOTween.To(() => 0f, SetAttackWindup, 1f, attackWindup).SetEase(Ease.Linear))
                .Append(DOTween.To(() => 0f, SetAttackThrust, 1f, attackThrust).SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => 0f, SetAttackRecovery, 1f, attackRecover).SetEase(Ease.Linear))
                .OnKill(() => attackSequence = null);
            attackSequence.timeScale = SlowMotion.PlayerRelativeScale;
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
        // attackerPosition: 맞는 순간 바라볼 공격자 위치
        // 피격 반응 중 다시 맞으면 피해만 받고 반응은 새로 시작하지 않음
        // root 아래 콜라이더와는 부딪히지도 밀려나지도 않고 통과함(분신처럼 막을 필요가 없는 몸), 피격 판정은 그대로
        // 지금 있는 콜라이더는 바로 충돌을 끄고, 나중에 생긴 부위(뼈 히트박스 등)는 몸 겹침 검사에서 만나는 순간 끔
        public void AddPassThroughBody(Transform root)
        {
            if (root == null || passThroughBodies.Contains(root)) return;

            passThroughBodies.Add(root);
            foreach (Collider other in root.GetComponentsInChildren<Collider>(true))
                if (other.isTrigger == false) Physics.IgnoreCollision(controller, other, true);
        }

        public void RemovePassThroughBody(Transform root) => passThroughBodies.Remove(root);

        public void ApplyHitReaction(HitReaction reaction, Vector3 direction, HitPush push, Vector3 attackerPosition)
        {
            if (reaction == HitReaction.None || IsHit) return;

            StopAttackMotion();

            hitRoutine = StartCoroutine(PlayHitReaction(reaction, direction, push, attackerPosition));
        }

        // 피격 모션의 EndHit 신호, 넉다운은 기상 모션 끝에서 옴
        public void EndHitAnimation() => hitAnimationEnded = true;

        private IEnumerator PlayHitReaction(HitReaction reaction, Vector3 direction, HitPush push, Vector3 attackerPosition)
        {
            bool hasFinishedReaction = false;
            try
            {
                IsHit = true;
                savedHitStepOffset = controller.stepOffset;
                hasHitStepOffset = true;
                controller.stepOffset = 0f;
                velocity.y = Mathf.Min(velocity.y, 0f);
                if (psychokinesisController != null) psychokinesisController.InterruptForHit();
                heldObject = null;
                // 피격 중에는 홀딩 갱신도 멈추므로 보간을 기다리지 않고 IK 상태를 즉시 비운다.
                externalHoldTarget = null;
                isHolding = false;
                isRestoringFacing = false;
                turnedForHold = false;
                appliedBodyLead = -1f;
                attackLookBlend = 0f;
                ikBlend = 0f;
                throwPrepareBlend = 0f;
                animator.SetLayerWeight(holdLayer, 0f);
                hitAnimationEnded = false;
                // 지난 넉다운에서 조기 기상을 쓰지 않았어도 이번 넉다운의 기상 가능 시점(WakeUp 신호)부터 다시 허용
                CanWakeUp = false;

                direction.y = 0f;
                direction.Normalize();

                int motionIndex;

                if (reaction == HitReaction.Knockdown)
                {
                    motionIndex = 1;
                }
                else
                {
                    motionIndex = push.HasMovement ? 2 : 0;
                }

                Vector3 toAttacker = attackerPosition - transform.position;
                toAttacker.y = 0f;

                float hitDir = Vector3.Dot(toAttacker, transform.forward) >= 0f ? 0f : 1f;

                if (toAttacker.sqrMagnitude < 0.0001f) hitDir = 0f;

                animator.SetFloat(HitDir, hitDir);

                // 넘어지는 모션이 뒤로 쓰러지는 방향 하나뿐이므로, 공격자를 보게 돌려서 어느 방향에서 맞든 뒤로 날아가게 한다
                animator.SetInteger(HitMotion, motionIndex);
                animator.SetTrigger(Hit);

                if (push.HasMovement)
                {
                    float t = 0f, prevY = 0f;

                    while (t < 1f)
                    {
                        float step = Mathf.Min(PlayerDeltaTime / push.duration, 1f - t);
                        t += step;
                        // 가운데(t = 0.5)에서 최고 높이 height에 닿는 포물선
                        float y = 4f * push.height * t * (1f - t);
                        Vector3 move = direction * (push.distance * step);
                        move.y = y - prevY;
                        prevY = y;
                        MoveDuringHit(move, push.height > 0f);
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
                        MoveDuringHit(Vector3.zero);
                        elapsed += PlayerDeltaTime;
                        yield return null;
                    }

                    animator.SetTrigger(WakeUp);

                    // 무적은 기상 시작부터 InvincibleTime 동안, 기상 모션이 먼저 끝나면 무적인 채로 행동 가능
                    IsInvincible = true;
                    float invincibleEndTime = playerTime + InvincibleTime;

                    yield return WaitHitAnimationEnd();
                    FinishHitReaction();
                    hasFinishedReaction = true;

                    while (playerTime < invincibleEndTime) yield return null;
                    IsInvincible = false;
                    yield break;
                }

                // 밀려나는 이동과 피격 모션이 모두 끝나야 행동 가능
                yield return WaitHitAnimationEnd();
                FinishHitReaction();
                hasFinishedReaction = true;
            }
            finally
            {
                if (hasFinishedReaction == false) FinishHitReaction();
                hitRoutine = null;
            }
        }

        // 지정된 넉다운 포물선 구간만 높이를 보존하고, 그 외 피격 대기 중에는 계속 낙하한다.
        private void MoveDuringHit(Vector3 displacement, bool preserveArc = false)
        {
            if (preserveArc)
            {
                velocity.y = 0f;
            }
            else
            {
                velocity.y = IsGrounded && velocity.y < 0f ? -2f : velocity.y + gravity * PlayerDeltaTime;
                displacement.y += velocity.y * PlayerDeltaTime;
            }

            displacement += slideOffDirection * (slideOffSpeed * PlayerDeltaTime);
            slideOffDirection = Vector3.zero;
            controller.Move(displacement);
        }

        private void FinishHitReaction()
        {
            IsHit = false;
            isPushingOut = true; // 피격이 끝날 때 겹쳐 있으면 바로 빼냄
            if (hasHitStepOffset == false) return;
            if (controller != null) controller.stepOffset = savedHitStepOffset;
            hasHitStepOffset = false;
        }

        private IEnumerator WaitHitAnimationEnd()
        {
            float elapsed = 0f;
            while (hitAnimationEnded == false && elapsed < maxHitAnimationWait)
            {
                MoveDuringHit(Vector3.zero);
                elapsed += PlayerDeltaTime;
                yield return null;
            }
        }

        #endregion

        private static float SmoothAxis(float current, float target)
        {
            const float AXIS_SPEED = 3f;
            if (target != 0f && current * target < 0f) current = 0f;
            return Mathf.MoveTowards(current, target, AXIS_SPEED * PlayerDeltaTime);
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
