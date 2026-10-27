using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Mission
{
    public class MissionNPC : MonoBehaviour
    {
        [SerializeField] private GameObject missionUI;
        [SerializeField] private  float distance;
        [SerializeField] private Player.Player player;

        private void Start()
        {
            player = Player.Player.Instance;
        }

        private void Update()
        {
            if (Vector3.Distance(player.transform.position, transform.position) < distance)
            {
                if (SimpleInput.GetKeyDown(Key.F))
                {
                    missionUI.SetActive(true);
                    UIManager.Instance.LockCursor(false, true);
                }
            }
        }
    }
   
}