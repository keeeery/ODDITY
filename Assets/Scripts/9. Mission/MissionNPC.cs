using System;
using Study.Utilities;
using TeamOdd.Superpower;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace TeamOdd.Mission
{
    public class MissionNPC : MonoBehaviour
    {
        [SerializeField] private GameObject popupUI;
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
                    bool isClose = popupUI.activeSelf == false;
                    
                    if(popupUI.TryGetComponent(out AugmentationChipPanel panel))
                        panel.IsActivePanel(isClose, player.GetComponent<AugmentationChipInventory>());
                    else popupUI.SetActive(isClose);
                    
                    UIManager.Instance.LockCursor(isClose == false, isClose);
                }
                else if (SimpleInput.GetKeyDown(Key.Escape))
                {
                    if(popupUI.TryGetComponent(out AugmentationChipPanel panel))
                        panel.Close();
                    else popupUI.SetActive(false);
                    
                    UIManager.Instance.LockCursor(true);
                }
            }
        }
    }
   
}