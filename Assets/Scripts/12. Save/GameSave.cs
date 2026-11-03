using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Study.MiniDefence;
using TeamOdd.Mission;
using TeamOdd.Superpower;
using UnityEngine;

namespace TeamOdd
{
    // 보유한 증강 칩(모양·장착 기록 포함)과 수락한 임무를 로컬(persistentDataPath/save.json)에 저장한다.
    // 칩을 얻거나 장착을 바꿀 때, 임무를 수락하거나 클리어해 목록이 바뀔 때마다 바로 저장한다.
    // 메인 화면의 새 게임은 Delete로 지우고 목록을 비우며, 이어하기는 Load로 목록을 채운 뒤 로비로 간다.
    public static class GameSave
    {
        private const string FileName = "save.json";

        [Serializable]
        private class SaveData
        {
            public List<ChipData> chips = new();
            public List<int> acceptedMissions = new();
        }

        [Serializable]
        private class ChipData
        {
            public int id;
            public Vector2Int[] cells;
            public bool equipped;
            public Vector2Int origin;
            public int rotation;
            public int skillSlot = -1; // 스킬 칩이 들어 있던 슬롯 번호, 능력치 칩·해제된 칩은 -1
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(FilePath);

        // 플레이 모드에 들어갈 때마다 한 번 구독한다 (도메인 리로드를 꺼 둬도 두 번 붙지 않게 먼저 뺀다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            AugmentationChipInventory.Changed -= Save;
            AugmentationChipInventory.Changed += Save;
            MissionListUI.OnAcceptedMissionsChanged -= Save;
            MissionListUI.OnAcceptedMissionsChanged += Save;
        }

        public static void Save()
        {
            var data = new SaveData();
            IReadOnlyList<OwnedChip> slots = AugmentationChipInventory.AllSkillSlots;

            foreach (OwnedChip chip in AugmentationChipInventory.AllChips)
            {
                data.chips.Add(new ChipData
                {
                    id = chip.Def.ID,
                    cells = chip.Cells,
                    equipped = chip.IsEquipped,
                    origin = chip.Origin,
                    rotation = chip.Rotation,
                    skillSlot = IndexOf(slots, chip),
                });
            }

            foreach (MissionDef mission in MissionListUI.AcceptedMissionList)
                data.acceptedMissions.Add(mission.ID);

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSave] 저장하지 못했습니다: {exception.Message}");
            }
        }

        // 저장 파일로 보유 칩·수락 임무 목록을 채운다. 표에서 사라진 칩·임무는 건너뛴다
        // 칩의 장착 기록은 플레이어가 생길 때(AugmentationChipInventory.Start) 스탯·스킬 슬롯에 반영된다
        public static bool Load()
        {
            if (HasSave == false)
                return false;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSave] 저장 파일을 읽지 못했습니다: {exception.Message}");
                return false;
            }

            if (data == null)
                return false;

            ResetProgress();

            var chipTable = new AugmentationChipTable();
            chipTable.Load();
            Dictionary<int, AugmentationChipDef> chipDefs = chipTable.GetDefs().ToDictionary(def => def.ID);
            foreach (ChipData chip in data.chips)
            {
                if (chipDefs.TryGetValue(chip.id, out AugmentationChipDef def) == false || chip.cells == null || chip.cells.Length == 0)
                {
                    Debug.LogWarning($"[GameSave] {chip.id}번 칩을 표에서 찾지 못해 불러오지 않습니다.");
                    continue;
                }

                AugmentationChipInventory.Restore(def, chip.cells, chip.equipped, chip.origin, chip.rotation, chip.skillSlot);
            }

            var missionTable = new MissionTable();
            missionTable.Load();
            Dictionary<int, MissionDef> missionDefs = missionTable.GetDefs.ToDictionary(def => def.ID);
            MissionListUI.RestoreMissions(data.acceptedMissions.Where(missionDefs.ContainsKey).Select(id => missionDefs[id]));
            return true;
        }

        // 저장 파일을 지우고 보유 칩·수락 임무 목록도 비운다 (새 게임)
        public static void Delete()
        {
            try
            {
                if (HasSave) File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSave] 저장 파일을 지우지 못했습니다: {exception.Message}");
            }

            ResetProgress();
        }

        private static void ResetProgress()
        {
            AugmentationChipInventory.ResetAll();
            MissionListUI.RestoreMissions(Array.Empty<MissionDef>());
        }

        private static int IndexOf(IReadOnlyList<OwnedChip> list, OwnedChip chip)
        {
            for (int index = 0; index < list.Count; index++)
                if (list[index] == chip) return index;
            return -1;
        }
    }
}
