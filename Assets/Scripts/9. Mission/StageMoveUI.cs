using System.Collections.Generic;
using Study.MiniDefence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Mission
{
    // Stage Move NPC 가 여는 창. 수락한 미션 중 하나를 골라 해당 미션 씬으로 이동한다.
    public class StageMoveUI : MonoBehaviour
    {
        [Header("미션 목록")]
        [SerializeField] private MissionListUI missionListUI;
        [SerializeField] private Transform missionContent;
        [SerializeField] private Button missionRowTemplate;
        [SerializeField] private GameObject emptyText;
        [SerializeField] private Color rowNormalColor = new Color(0.68f, 0.56f, 0.38f, 1f);
        [SerializeField] private Color rowSelectedColor = new Color(0.96f, 0.62f, 0.16f, 1f);

        [Header("미션 상세")]
        [SerializeField] private TMP_Text bossName;
        [SerializeField] private Image bossImage;
        [SerializeField] private TMP_Text missionDescription;
        [SerializeField] private Button moveButton;
        [SerializeField] private GameObject moveFailUI;

        private readonly List<Button> rows = new();
        private MissionDef selectedMission;
        private Color bossImageEmptyColor;

        private void Awake()
        {
            bossImageEmptyColor = bossImage.color;
            missionRowTemplate.gameObject.SetActive(false);
        }

        // NPC 가 창을 열 때마다 수락한 미션 목록을 다시 그린다.
        private void OnEnable()
        {
            moveFailUI.SetActive(false);
            RefreshList();
        }

        private void RefreshList()
        {
            foreach (Button row in rows)
                Destroy(row.gameObject);
            rows.Clear();

            IReadOnlyList<MissionDef> missions = missionListUI.AcceptedMissions;
            emptyText.SetActive(missions.Count == 0);

            foreach (MissionDef mission in missions)
            {
                Button row = Instantiate(missionRowTemplate, missionContent);
                row.gameObject.SetActive(true);
                row.name = $"Mission {rows.Count + 1}";
                row.GetComponentInChildren<TMP_Text>().SetText(mission.QuestName);
                row.onClick.AddListener(() => SelectMission(mission, row));
                rows.Add(row);
            }

            if (rows.Count > 0)
                SelectMission(missions[0], rows[0]);
            else
                SelectMission(null, null);
        }

        private void SelectMission(MissionDef mission, Button selectedRow)
        {
            selectedMission = mission;

            foreach (Button row in rows)
                row.image.color = row == selectedRow ? rowSelectedColor : rowNormalColor;

            bool hasMission = mission != null;
            moveButton.interactable = hasMission;

            if (hasMission == false)
            {
                bossName.SetText("-");
                bossImage.sprite = null;
                bossImage.color = bossImageEmptyColor;
                missionDescription.SetText("Mission NPC 에게서 미션을 먼저 수락하세요.");
                return;
            }

            bossName.SetText(mission.BossName);
            bossImage.sprite = mission.BossSprite;
            bossImage.color = mission.BossSprite != null ? Color.white : bossImageEmptyColor;
            missionDescription.SetText($"{mission.MissionName}\n{mission.MissionDescription} \n Reward : {mission.Reward}");
        }

        public void MoveToStage()
        {
            if (selectedMission == null)
                return;

            if (string.IsNullOrEmpty(selectedMission.NextStageName))
            {
                moveFailUI.SetActive(true);
                return;
            }

            Player.Player.Instance.CharacterController.enabled = false;
            Player.Player.Instance.CharacterController.enabled = true;

            SceneLoadManager.Instance.LoadScene(selectedMission.NextStageName);
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
