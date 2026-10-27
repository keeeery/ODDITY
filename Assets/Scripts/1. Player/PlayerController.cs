using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Study.Utilities;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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
        public bool isHit;
        public bool CanWakeUp;
        private Coroutine hitReactionRoutine;

        private CharacterController controller;
        private PsychokinesisController psychokinesisController;
        private Vector3 velocity;
        private Vector3 rollDir;
        private float moveX, moveZ;

        private float pushSpeedRatio = 1f; // 이번 프레임 Move 중 밀고 있는 물체로 계산
        private float appliedPushSpeedRatio = 1f; // 이번 프레임 이동에 적용 (직전 프레임 계산값)

        private bool defaultRotateTowardMove;
        private bool isHolding;
        private bool isRestoringFacing;
        private float bodyTargetYaw;
        private float restoreTargetYaw;
        private float ikBlend;
        private float throwPrepareBlend; // 0: 팔을 뻗은 홀딩 자세, 1: 던지기 준비 자세
        private Vector3 lastHandTarget;
        private Quaternion lastHandRotation;
        private Transform externalHoldTarget; // 염동 내려찍기처럼 물체가 아닌 대상을 붙잡는 스킬이 지정
        private float attackStartTime = float.NegativeInfinity;
        private float attackPushAtStart;
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
        public bool IsGrounded => controller.isGrounded;
        public bool IsRolling { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsInvincible { get; private set; }

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
            //Debug.Log(speed);

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

        private void LateUpdate()
        {
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

            animator.SetTrigger(Rolling);
        }

        public void EndRoll()
        {
            IsRolling = false;
            IsInvincible = false;
        }

        private void ExecuteRollMovement()
        {
            float rollSpeed = rollDistance / rollDuration;
            Vector3 rollMove = (rollDir * rollSpeed) + new Vector3(0, gravity, 0);
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
            if (IsRolling) attackStartTime = float.NegativeInfinity;
            UpdateAttackPulse();

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

            SetBodyYaw(Mathf.LerpAngle(transform.eulerAngles.y, bodyTargetYaw, t));

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
            attackPushAtStart = attackPush;
            attackStartTime = Time.time;
            return attackWindup + attackThrust * attackReleasePoint;
        }

        // 연타 중 다시 누르면 뻗어 있던 팔을 당기면서 이어지도록, 시작할 때 뻗은 정도에서 출발
        private void UpdateAttackPulse()
        {
            float elapsed = Time.time - attackStartTime;

            if (elapsed < attackWindup)
            {
                float k = Mathf.SmoothStep(0f, 1f, elapsed / attackWindup);
                attackPull = k;
                attackPush = attackPushAtStart * (1f - k);
            }
            else if (elapsed < attackWindup + attackThrust)
            {
                float k = 1f - Mathf.Pow(1f - (elapsed - attackWindup) / attackThrust, 3f);
                attackPull = 1f - k;
                attackPush = k;
            }
            else
            {
                attackPull = 0f;
                attackPush = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - attackWindup - attackThrust) / attackRecover);
            }
        }

        #endregion

        #region 피격

        public void ApplyHitReaction(Vector3 attackerPos, HitReaction reaction, float dist = 6f, float height = 2f,
            float dur = 0.6f)
        {
            if(reaction==HitReaction.None || isHit) return;
            
            // 넉백 중 다시 맞으면 이전 넉백을 끊고 새로 시작
            if (hitReactionRoutine != null) StopCoroutine(hitReactionRoutine);

            if (reaction == HitReaction.Stagger)
                hitReactionRoutine = StartCoroutine(PlayHitReaction(attackerPos, reaction, 0, dist, 0f, dur));
            if (reaction == HitReaction.Knockdown)
                hitReactionRoutine = StartCoroutine(PlayHitReaction(attackerPos, reaction, 1, dist, height, dur));
        }

        private IEnumerator PlayHitReaction(Vector3 bossPos, HitReaction reaction, int motionIndex,float dist = 6f, float height = 2f, float dur = 0.6f)
        {
            isHit = true;
            
            Vector3 dir = transform.position - bossPos;
            
            dir.y = 0;
            dir.Normalize();
            
            float t = 0, prevY = 0;
            
            animator.SetInteger(HitMotion, motionIndex);
            animator.SetTrigger(Hit);
            
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float y = height * t * (1f - t);
                Vector3 move = dir * (dist * (Time.deltaTime / dur));
                move.y = y - prevY;
                prevY = y;
                controller.Move(move); // 벽 충돌은 Move가 처리
                yield return null;
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
            
                IsInvincible = true;
                yield return new WaitForSeconds(InvincibleTime);
                IsInvincible = false;   
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