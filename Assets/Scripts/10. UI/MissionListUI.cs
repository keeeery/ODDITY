using System.Collections.Generic;
using Study.MiniDefence;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.Mission
{
    public enum AddMissionResult
    {
        Added,
        AlreadyAccepted,
        ListFull,
        NoStageScene,
    }

    public class MissionListUI : MonoBehaviour
    {
        // 동시에 수락할 수 있는 미션 수
        public const int MaxMissionCount = 3;

        [SerializeField] private GameObject progressList;
        [SerializeField] private GameObject clearList;
        [SerializeField] private Color disableColor;
        [SerializeField] private Color enableColor;

        [Header("퀘스트 목록")]
        [SerializeField] private Transform questContent;
        [SerializeField] private GameObject questRowPrefab;
        [SerializeField] private TMP_Text questCountText;

        private readonly List<MissionDef> acceptedMissions = new();

        public GameObject ProgressList => progressList;
        public GameObject ClearList => clearList;

        // 수락한 미션 목록 (Stage Move UI 에서 이동할 미션을 고를 때 사용)
        public IReadOnlyList<MissionDef> AcceptedMissions => acceptedMissions;

        private void Awake()
        {
            // 씬에 배치해 둔 샘플 행은 지우고, 수락한 미션만 표시한다.
            foreach (Transform child in questContent)
                Destroy(child.gameObject);

            UpdateCount();
        }

        public void ActiveTab(Image activeTab)
        {
            GameObject clicked = EventSystem.current.currentSelectedGameObject;
            activeTab.color = disableColor;
            clicked.GetComponent<Image>().color = enableColor;
        }

        // 이동할 씬이 없는 미션 · 이미 수락한 미션 · 목록이 가득 찬 경우에는 추가하지 않는다.
        public AddMissionResult AddMission(MissionDef missionDef)
        {
            if (string.IsNullOrEmpty(missionDef.NextStageName))
                return AddMissionResult.NoStageScene;

            if (acceptedMissions.Exists(mission => mission.ID == missionDef.ID))
                return AddMissionResult.AlreadyAccepted;

            if (acceptedMissions.Count >= MaxMissionCount)
                return AddMissionResult.ListFull;

            acceptedMissions.Add(missionDef);

            GameObject row = Instantiate(questRowPrefab, questContent);
            row.name = $"Quest {acceptedMissions.Count}";
            row.transform.Find("QuestNameText").GetComponent<TMP_Text>().SetText(missionDef.QuestName);

            UpdateCount();
            return AddMissionResult.Added;
        }

        private void UpdateCount()
        {
            questCountText.SetText($"{acceptedMissions.Count} / {MaxMissionCount}");
        }
    }
}
