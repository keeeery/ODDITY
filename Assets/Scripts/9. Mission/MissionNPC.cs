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
                    if(popupUI.TryGetComponent(out AugmentationChipPanel panel))
                        panel.Open(player.GetComponent<AugmentationChipInventory>());
                    else popupUI.SetActive(true);
                    
                    UIManager.Instance.LockCursor(false, true);
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