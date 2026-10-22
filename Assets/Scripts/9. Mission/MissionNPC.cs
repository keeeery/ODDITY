using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Mission
{
    public class MissionNPC : MonoBehaviour
    {
        [SerializeField] private GameObject missionUI;

        private void OnTriggerStay(Collider other)
        {
            if (SimpleInput.GetKeyDown(Key.F))
            {
                missionUI.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
   
}