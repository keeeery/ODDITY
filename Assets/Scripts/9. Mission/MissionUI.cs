using Study.MiniDefence;
using TeamOdd.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private TMP_Text clickFailText;
        [SerializeField] private MissionListUI missionListUI;

        private string stageSceneName;
        private MissionDef selectedMission;
        private Color bossImageEmptyColor;

        private void Awake()
        {
            missionTable = new MissionTable();
            missionTable.Load();
            bossImageEmptyColor = bossImage.color;

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
            selectedMission = missionDef;

            bossName.SetText(missionDef.BossName);
            bossImage.sprite = missionDef.BossSprite;
            // 그림이 있으면 원래 색으로, 없으면 빈 칸 색으로 (StageMoveUI와 같게)
            bossImage.color = missionDef.BossSprite != null ? Color.white : bossImageEmptyColor;
            missionDescription.SetText($"{missionDef.MissionDescription} \n Reward : {missionDef.Reward}");
            stageSceneName = missionDef.NextStageName;
            Debug.Log(stageSceneName);
        }

        // 선택한 미션을 Quest List UI 에 추가한다. 이동은 Stage Move NPC 에서 한다.
        // 추가하지 못하면 ClickFail UI 에 이유를 띄운다.
        public void RegisterMission()
        {
            if (TryRegisterMission()) CloseUI();
        }

        // 선택한 미션을 수락하고 바로 그 스테이지로 이동한다. 수락하지 못하면 이동하지 않고 이유를 띄운다.
        public void RegisterAndMove()
        {
            if (TryRegisterMission()) MoveToNextStage();
        }

        private bool TryRegisterMission()
        {
            string failMessage = missionListUI.AddMission(selectedMission) switch
            {
                AddMissionResult.NoStageScene => "이전 미션을 클리어 해주세요",
                AddMissionResult.AlreadyAccepted => "이미 수락한 미션입니다",
                AddMissionResult.ListFull => $"미션은 최대 {MissionListUI.MaxMissionCount}개까지 수락할 수 있습니다",
                _ => null,
            };

            if (failMessage == null)
            {
                SystemMessage.Show($"{selectedMission.QuestName} 퀘스트를 수락했습니다.");
                return true;
            }

            clickFailText.SetText(failMessage);
            clickFailUI.SetActive(true);
            return false;
        }
        
        public void MoveToNextStage()
        {
            if (string.IsNullOrEmpty(stageSceneName))
            {
                clickFailUI.SetActive(true);
                return;
            }
            
            Player.Player.Instance.CharacterController.enabled = false;
            Player.Player.Instance.CharacterController.enabled = true;

            SceneLoadManager.Instance.LoadScene(stageSceneName);
            UIManager.Instance.BattleUIActive(true);
            CloseUI();
        }

        public void CloseUI()
        {
            UIManager.Instance.LockCursor(true);
            gameObject.SetActive(false);
        }
    }
}