using Study3D.CameraControl;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Psychokinesis
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PsychokinesisTestPlayer : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform cameraRig;
        [SerializeField] private BasicThirdPersonController cameraController;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;

        private CharacterController characterController;
        private float verticalSpeed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Escape))
                LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked &&
                     (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left) ||
                      SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right)))
                LockCursor(true);

            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            cameraController.enabled = cursorLocked;
            Vector2 moveInput = cursorLocked ? SimpleInput.GetMoveAxisRaw() : Vector2.zero;
            bool sprinting = cursorLocked && (SimpleInput.GetKey(Key.LeftShift) || SimpleInput.GetKey(Key.RightShift));

            if (characterController.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;

            verticalSpeed += Physics.gravity.y * Time.deltaTime;

            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 velocity = (forward * moveInput.y + right * moveInput.x) * (sprinting ? sprintSpeed : moveSpeed);
            velocity.y = verticalSpeed;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void OnDisable()
        {
            LockCursor(false);
        }

        private void LateUpdate()
        {
            Vector3 playerPosition = transform.position;
            cameraRig.position = playerPosition;
            // 플레이어가 리그의 자식이어도 부모 이동이 캐릭터 이동에 중복 적용되지 않게 한다.
            if (transform.IsChildOf(cameraRig)) transform.position = playerPosition;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = locked == false;
        }
    }
}
