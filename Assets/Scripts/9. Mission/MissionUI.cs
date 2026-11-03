using System.Collections.Generic;
using Study.MiniDefence;
using TeamOdd.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TeamOdd.Mission
{
    public class MissionUI : MonoBehaviour
    {
        private MissionTable missionTable;

        private const string SelectGuideText = "왼쪽 임무 목록에서 임무를 선택하세요.";
        private const string NoMissionText = "수락 가능한 임무가 없습니다.";

        [Header("미션UI 세팅")] 
        [SerializeField] private Transform missionContent;
        [SerializeField] private Button missionRowTemplate;  // missions.tsv 한 줄마다 복제하는 임무 칸
        [SerializeField] private Button[] acceptButtons;     // 고른 임무가 없으면 누를 수 없게 한다
        [SerializeField] private TMP_Text missionDescription;
        [SerializeField] private TMP_Text bossName;
        [SerializeField] private Image bossImage;
        [SerializeField] private GameObject clickFailUI;
        [SerializeField] private TMP_Text clickFailText;
        [SerializeField] private MissionListUI missionListUI;

        private readonly List<Button> rows = new();
        private string stageSceneName;
        private MissionDef selectedMission;
        private Color bossImageEmptyColor;

        private void Awake()
        {
            missionTable = new MissionTable();
            missionTable.Load();
            bossImageEmptyColor = bossImage.color;

            // 프리팹에 배치해 둔 샘플 칸은 지우고, 첫 칸만 복제용 틀로 숨겨 둔다
            foreach (Transform child in missionContent)
            {
                if (child != missionRowTemplate.transform)
                    Destroy(child.gameObject);
            }
            missionRowTemplate.gameObject.SetActive(false);
        }

        // NPC 가 창을 열 때마다 아직 수락하지 않은 임무로 목록을 다시 그린다.
        private void OnEnable()
        {
            clickFailUI.SetActive(false);
            RefreshList();
        }

        private void RefreshList()
        {
            foreach (Button row in rows)
                Destroy(row.gameObject);
            rows.Clear();

            foreach (MissionDef mission in missionTable.GetDefs)
            {
                if (IsAccepted(mission))
                    continue;

                Button row = Instantiate(missionRowTemplate, missionContent);
                row.gameObject.SetActive(true);
                row.name = $"Mission {mission.ID}";
                row.transform.Find("QuestTitleText").GetComponent<TMP_Text>().SetText(mission.BossName);
                // 틀에 연결된 MissionClick(번호) 호출은 끄고, 복제한 칸이 자기 임무를 고르게 한다
                for (int i = 0; i < row.onClick.GetPersistentEventCount(); i++)
                    row.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
                row.onClick.AddListener(() => MissionClick(mission.ID));
                rows.Add(row);
            }

            ShowEmptyDetail(rows.Count > 0 ? SelectGuideText : NoMissionText);
        }

        private static bool IsAccepted(MissionDef mission)
        {
            foreach (MissionDef accepted in MissionListUI.AcceptedMissionList)
            {
                if (accepted.ID == mission.ID)
                    return true;
            }
            return false;
        }

        // 고른 임무가 없을 때 우측에 안내 글만 보여 준다
        private void ShowEmptyDetail(string message)
        {
            selectedMission = null;
            stageSceneName = null;
            SetAcceptButtonsInteractable(false);

            bossName.SetText("-");
            bossImage.sprite = null;
            bossImage.color = bossImageEmptyColor;
            missionDescription.SetText(message);
        }

        private void SetAcceptButtonsInteractable(bool interactable)
        {
            foreach (Button button in acceptButtons)
                button.interactable = interactable;
        }

        public void MissionClick(int i)
        {
            MissionDef missionDef = missionTable.Get(i);
            selectedMission = missionDef;
            SetAcceptButtonsInteractable(true);

            bossName.SetText(missionDef.BossName);
            bossImage.sprite = missionDef.BossSprite;
            // 그림이 있으면 원래 색으로, 없으면 빈 칸 색으로 (StageMoveUI와 같게)
            bossImage.color = missionDef.BossSprite != null ? Color.white : bossImageEmptyColor;
            missionDescription.SetText($"{missionDef.MissionDescription}\n\n보상\n{missionDef.GetRewardText()}");
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
            if (selectedMission == null)
                return false;

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