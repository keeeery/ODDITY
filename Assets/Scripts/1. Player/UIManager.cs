using Jay;
using Study.Utilities;
using TeamOdd.CameraMove;
using TeamOdd.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd
{
    public class UIManager : SingletonBase<UIManager>
    {
        private PlayerController playerController;
        private CameraController cameraController;
        
        private bool isManuallyLocked = false;

        [SerializeField] private GameObject battleUI;

        private void Update()
        {
            if (isManuallyLocked == false)
            {
                if (SimpleInput.GetKeyDown(Key.Escape))
                    LockCursor(false);
                else if (Cursor.lockState != CursorLockMode.Locked &&
                         (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left)
                          || SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right)))
                    LockCursor(true);
            }
        }

        public void BattleUIActive(bool active)
        {
            battleUI.SetActive(active);
        }
        
        public void LockCursor(bool locked, bool isManuallyLock = false)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = locked == false;

            isManuallyLocked = isManuallyLock;

            // Start 실행 순서에 기대지 않고, 처음 쓸 때 가져온다
            if (playerController == null)
                playerController = Player.Player.Instance.PlayerController;
            if (cameraController == null)
                cameraController = Player.Player.Instance.CameraController;

            playerController.enabled = locked;
            cameraController.enabled = locked;
        }
    }   
}