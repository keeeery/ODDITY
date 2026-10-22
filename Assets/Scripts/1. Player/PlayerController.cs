using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private static readonly int Rolling = Animator.StringToHash("Rolling");
        private static readonly int Walk = Animator.StringToHash("IsWalk");
        private static readonly int Run = Animator.StringToHash("IsRun");
        
        [Header("이동")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -9.81f;

        [Header("방향")]
        [SerializeField] private Transform moveReference;
        [SerializeField] private bool rotateTowardMove = true;
        [SerializeField] private float turnSpeed = 720f;
        
        [Header("구르기")]
        [SerializeField] private float rollDistance = 4f;
        [SerializeField] private float rollDuration = 0.6f;
        
        [SerializeField] private Animator animator;
        
        private CharacterController controller;
        private Vector3 velocity;
        private Vector3 rollDir;
        private float moveX, moveZ;

        public bool IsGrounded => controller.isGrounded;
        public bool IsRolling { get; private set; }
        
        public event Action OnRollRequested;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            // 구르는 동안에는 일반 이동 및 입력 처리를 막습니다.
            if (IsRolling)
            {
                ExecuteRollMovement();
                return;
            }

            UpdateMovement();
            WalkAnimation();
            CheckRollInput();
        }

        private void UpdateMovement()
        {
            float speed = SimpleInput.GetKey(Key.LeftShift) ? runSpeed : walkSpeed;

            Vector2 inputRaw = SimpleInput.GetMoveAxisRaw();
            
            moveX = SmoothAxis(moveX, inputRaw.x);
            moveZ = SmoothAxis(moveZ, inputRaw.y);

            if (moveReference == null)
            {
                moveReference = Camera.main.transform;
            }

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

            Vector3 totalMove = (moveVector * speed) + new Vector3(0, velocity.y, 0);
            
            controller.Move(totalMove * Time.deltaTime);

            if (rotateTowardMove && moveVector.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVector);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        private void WalkAnimation()
        {
            Vector2 inputRaw = SimpleInput.GetMoveAxisRaw();

            bool isMove = inputRaw != Vector2.zero;
            bool isRun = SimpleInput.GetKey(Key.LeftShift);
            
            animator.SetBool(Walk, isMove);
            animator.SetBool(Run, isRun);
        }
        
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
        }
        
        private void ExecuteRollMovement()
        {
            float rollSpeed = rollDistance / rollDuration;
            Vector3 rollMove = (rollDir * rollSpeed) + new Vector3(0, gravity, 0);
            controller.Move(rollMove * Time.deltaTime);
        }
        
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