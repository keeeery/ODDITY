using System;
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
        
        [SerializeField] private GameObject missionUI;
        [SerializeField] private GameObject skillUI;
        [SerializeField] private GameObject battleUI;

        private void Start()
        {
            playerController = Player.Player.Instance.PlayerController;
            cameraController = Player.Player.Instance.CameraController;
        }

        private void Update()
        {
            if (isManuallyLocked == false)
            {
                if (SimpleInput.GetKeyDown(Key.Escape))
                    LockCursor(false);
                else if (Cursor.lockState != CursorLockMode.Locked ||
                         (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left)
                          || SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right)))
                    LockCursor(true);
            }
        }

        public void BattleUIActive(bool active)
        {
            // 씬에 UIManager 를 배치하지 않으면 자동 생성되어 battleUI 가 비어 있다.
            if (battleUI != null)
                battleUI.SetActive(active);
        }
        
        public void LockCursor(bool locked, bool isManuallyLock = false)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = locked == false;

            isManuallyLocked = isManuallyLock;

            playerController.enabled = locked;
            cameraController.enabled = locked;
        }
    }   
}