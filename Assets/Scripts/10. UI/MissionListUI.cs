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

        // 씬이 바뀌어도 남도록 static 으로 둔다. 화면의 행은 씬마다 이 목록을 보고 다시 만든다.
        private static readonly List<MissionDef> acceptedMissions = new();

        // 수락한 미션 목록 (Stage Move UI 에서 이동할 미션을 고를 때 사용)
        public IReadOnlyList<MissionDef> AcceptedMissions => acceptedMissions;

        private void Awake()
        {
            // 씬에 배치해 둔 샘플 행은 지우고, 수락한 미션만 다시 그린다.
            foreach (Transform child in questContent)
                Destroy(child.gameObject);

            for (int i = 0; i < acceptedMissions.Count; i++)
                CreateRow(acceptedMissions[i], i + 1);

            UpdateCount();
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
            CreateRow(missionDef, acceptedMissions.Count);

            UpdateCount();
            return AddMissionResult.Added;
        }

        // 클리어한 보스의 미션을 수락 목록에서 지운다.
        // 보스 씬에는 이 UI가 없으므로 static 으로 지우고, 로비로 돌아오면 Awake 에서 남은 미션만 다시 그린다.
        public static bool RemoveMission(string bossName)
        {
            int index = acceptedMissions.FindIndex(mission => mission.BossName == bossName);
            if (index < 0) return false;

            acceptedMissions.RemoveAt(index);
            return true;
        }

        private void CreateRow(MissionDef missionDef, int number)
        {
            GameObject row = Instantiate(questRowPrefab, questContent);
            row.name = $"Quest {number}";
            row.transform.Find("QuestNameText").GetComponent<TMP_Text>().SetText(missionDef.QuestName);
        }

        private void UpdateCount()
        {
            questCountText.SetText($"{acceptedMissions.Count} / {MaxMissionCount}");
        }
    }
}
