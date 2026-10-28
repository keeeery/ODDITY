/*
using System;
using TeamOdd.CameraMove;
using TeamOdd.Player;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private PlayerController playerController;
    private CameraController cameraController;

    private void Start()
    {
        playerController = Player.Instance.PlayerController;
        cameraController = Player.Instance.CameraController;
    }

    private void Update()
    {
        if (isManuallyLocked == false)
        {
            if (SimpleInput.GetKeyDown(Key.Escape))
                LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked &&
                     (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left) || SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right)))
                LockCursor(true);
        }
    }
    
    public void ControllerActive()
    {
        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            
        PlayerController.enabled = cursorLocked;
        CameraController.enabled = cursorLocked;
    }
        
    public void LockCursor(bool locked, bool isManuallyLock = false)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = locked == false;

        isManuallyLocked = isManuallyLock;
    }
    
}
*/
