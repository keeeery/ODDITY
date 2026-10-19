using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.PlayerMove
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
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
        [SerializeField] private float rollDistance = 4f;   // 한 번 구를 때 이동하는 거리
        [SerializeField] private float rollDuration = 0.6f; // 구르기에 걸리는 시간
        [SerializeField] private float invincibleTime = 0.4f;
        [SerializeField] private Animator animator;
        
        private CharacterController controller;
        private Vector3 velocity;
        private Vector3 rollDirection;
        private float moveX, moveZ;

        public bool IsGrounded => controller.isGrounded;
        public bool IsRolling { get; private set; }

        [SerializeField]private bool isground;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (moveReference == null && Camera.main != null) moveReference = Camera.main.transform;
        }

        private void Update()
        {
            UpdateMovement();
            isground = IsGrounded;
        }

        private void UpdateMovement()
        {
            float speed = SimpleInput.GetKey(Key.LeftShift) ? runSpeed : walkSpeed;

            Vector2 inputRaw = SimpleInput.GetMoveAxisRaw();
            moveX = SmoothAxis(moveX, inputRaw.x);
            moveZ = SmoothAxis(moveZ, inputRaw.y);

            Transform reference = moveReference != null ? moveReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

            Vector3 moveVector = (right * moveX) + (forward * moveZ);
            controller.Move(moveVector * (speed * Time.deltaTime));

            if (rotateTowardMove && moveVector.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVector);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    turnSpeed * Time.deltaTime);
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            
            if (IsGrounded && velocity.y < 0f) velocity.y = -2f;

            if (IsGrounded && SimpleInput.GetKeyDown(Key.Space))
            {
                velocity.y = Mathf.Sqrt((jumpHeight * -2f * gravity));
                Debug.Log($"Jump");
            }
        }

        private static float SmoothAxis(float current, float target)
        {
            const float AXIS_SPEED = 3f;
            if (target != 0f && current * target < 0f) current = 0f;
            return Mathf.MoveTowards(current, target, AXIS_SPEED * Time.deltaTime);
        }
    }
}
