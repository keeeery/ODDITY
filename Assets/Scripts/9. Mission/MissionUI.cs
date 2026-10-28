using RPGCharacterAnims;
using Study.MiniDefence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CameraController = TeamOdd.CameraMove.CameraController;

namespace TeamOdd.Mission
{
    public class MissionUI : MonoBehaviour
    {
        private MissionTable missionTable;

        [Header("미션UI 세팅")] 
        [SerializeField] private TMP_Text[] missionNames;
        [SerializeField] private TMP_Text missionDescription;
        [SerializeField] private TMP_Text bossName;
        [SerializeField] private Image bossImage;
        [SerializeField] private GameObject clickFailUI;
        
        private string stageSceneName;

        private void Awake()
        {
            missionTable = new MissionTable();
            missionTable.Load();

            for (int i = 0; i < missionNames.Length; i++)
            {
                missionNames[i].SetText(missionTable.Get(i).BossName);
            }
        }

        private void Start()
        {
            MissionClick(0);
        }

        public void MissionClick(int i)
        {
            MissionDef missionDef = missionTable.Get(i);
            
            bossName.SetText(missionDef.BossName);
            bossImage.sprite = missionDef.BossSprite;
            missionDescription.SetText($"{missionDef.MissionDescription} \n Reward : {missionDef.Reward}");
            stageSceneName = missionDef.NextStageName;
        }

        public void MoveToNextStage()
        {
            if (string.IsNullOrEmpty(stageSceneName))
            {
                clickFailUI.SetActive(true);
                return;
            }
            
            Player.Player.Instance.CharacterController.enabled = false;
            //Player.Player.Instance.transform.position = Vector3.zero;
            Player.Player.Instance.CharacterController.enabled = true;

            SceneLoadManager.Instance.LoadScene(stageSceneName);
            Player.Player.Instance.CameraController = Camera.main.GetComponent<CameraController>();
            Debug.Log("move scene");
            CloseUI();
            // SceneManager.LoadScene(stageSceneName);
        }

        public void CloseUI()
        {
            Player.Player.Instance.LockCursor(true);
            gameObject.SetActive(false);
        }
    }
}