using System;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 플레이어가 보유한 증강칩 목록과 장착 기록. Player 오브젝트(CombatEntity가 있는 곳)에 붙인다.
    /// 칩을 얻는 쪽(보상, 드랍 등)이 Add로 넣고, 장착 UI가 Equip/Unequip을 부른다.
    /// 능력치 칩은 장착/해제할 때 칩의 StatType/Value만큼 같은 오브젝트의 CombatEntity.CurrentStats에 보너스를 더하고 뺀다.
    /// 스킬 칩은 장착하면 스킬 슬롯(1~5번 키)의 가장 앞 빈 자리에 들어가고, 해제하면 그 자리를 비워 둔다. 다른 스킬의 번호는 바뀌지 않는다.
    /// 그리드 칸 밖이나 겹치는 자리인지는 칩을 놓는 쪽(ChipGridView)이 확인한다.
    /// </summary>
    public class AugmentationChipInventory : MonoBehaviour
    {
        // 스킬 칩은 이 개수까지만 장착한다. 슬롯 번호(0부터)가 곧 발동 키 순서다
        public const int SkillSlotCount = 5;

        private static readonly List<OwnedChip> chips = new();
        private static readonly OwnedChip[] skillSlots = new OwnedChip[SkillSlotCount]; // 빈 슬롯은 null
        // 저장 데이터에서 불러온 장착 기록 (칩, 스킬 슬롯). 스탯을 더할 플레이어가 생기면(Start) 장착한다
        // 플레이어가 이미 살아 있으면(메인 화면으로 돌아갔다가 이어하기) 다음 씬이 열릴 때 장착한다
        private static readonly List<(OwnedChip chip, int slot)> pendingEquips = new();
        // Start까지 간 살아 있는 플레이어의 인벤토리. 중복으로 파괴되는 플레이어는 Start 전에 사라지므로 여기 들어오지 않는다
        private static AugmentationChipInventory live;
        private CombatEntity owner;
        private SkillCaster skillCaster;

        public IReadOnlyList<OwnedChip> Chips => chips;
        public IReadOnlyList<OwnedChip> SkillSlots => skillSlots;

        // 저장(GameSave)이 플레이어 없이 읽는다
        public static IReadOnlyList<OwnedChip> AllChips => chips;
        public static IReadOnlyList<OwnedChip> AllSkillSlots => skillSlots;

        // 칩을 얻거나 장착·해제·스킬 슬롯 순서가 바뀌면 알린다 (GameSave가 듣고 저장한다)
        public static event Action Changed;

        // 패널이 현재 스탯과 칩 보너스를 보여줄 때 읽는다
        public RuntimeStats Stats => owner.CurrentStats;

        // 스킬 슬롯이 바뀌면 알린다 (패널의 스킬 슬롯 줄, 게임 화면의 스킬 바가 다시 그린다)
        public event Action SkillSlotsChanged;

        private void Awake()
        {
            owner = GetComponent<CombatEntity>();
            skillCaster = GetComponent<SkillCaster>();
            foreach (OwnedChip chip in chips)
                if (chip.Skill is PsychoSlamSkill slam) slam.BindDescriptionCaster(skillCaster);
        }

        // 불러온 장착 기록은 CombatEntity가 Awake에서 스탯을 만든 뒤에 반영해야 하므로 Start에서 장착한다
        // 플레이어는 씬마다 새로 생기므로, 이전 씬에서 장착해 둔 스탯 칩 보너스도 새 플레이어 스탯에 다시 더한다
        // (불러온 장착 기록은 아직 IsEquipped가 아니라 겹치지 않는다. 스킬 칩은 static 슬롯에 그대로 남아 있다)
        private void Start()
        {
            live = this;

            foreach (OwnedChip chip in chips)
            {
                if (chip.IsEquipped && chip.Def is StatChipDef stat)
                    owner.CurrentStats.Add(stat.StatType, stat.Value);
            }

            ApplyPendingEquips();
        }

        private void OnDestroy()
        {
            if (live == this)
                live = null;
        }

        private void ApplyPendingEquips()
        {
            foreach ((OwnedChip chip, int slot) in pendingEquips)
                Equip(chip, chip.Origin, chip.Rotation, slot);
            pendingEquips.Clear();
        }

        // 씬마다 새 플레이어가 구독하고, 새 씬 오브젝트의 OnEnable 뒤에 sceneLoaded가 오므로 그 씬 전환을 듣는다
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // 장착된 스킬 칩만 쿨타임이 돈다. 해제해 둔 칩은 해제한 순간의 쿨타임에서 멈춰 있다
        // 패널이 열려 있어도 흐르도록 커서 상태와 상관없이 여기서 돌린다
        private void Update()
        {
            foreach (OwnedChip chip in skillSlots)
            {
                if (chip != null)
                    chip.Skill.Tick(Time.deltaTime);
            }
        }

        // cells는 칩을 주는 쪽이 ChipShapeLibrary.Roll로 정한 모양
        // 스킬 칩이면 칩마다 스킬을 새로 만들어 넣는다
        public OwnedChip Add(AugmentationChipDef def, Vector2Int[] cells)
        {
            SuperpowerSkill skill = def is SkillChipDef skillDef ? SkillFactory.Create(skillDef) : null;
            if (skill is PsychoSlamSkill slam) slam.BindDescriptionCaster(skillCaster);
            // 칩은 버리지 않으므로 지금까지 얻은 개수가 곧 얻은 순서다
            OwnedChip chip = new OwnedChip(def, cells, skill, chips.Count);
            chips.Add(chip);
            Changed?.Invoke();
            return chip;
        }

        // 저장 데이터의 칩 한 장을 보유 목록에 넣는다 (메인 화면의 이어하기). 장착 기록은 플레이어가 생길 때 반영한다
        public static OwnedChip Restore(AugmentationChipDef def, Vector2Int[] cells, bool equipped, Vector2Int origin, int rotation, int skillSlot)
        {
            SuperpowerSkill skill = def is SkillChipDef skillDef ? SkillFactory.Create(skillDef) : null;
            // 살아 있는 플레이어의 Awake는 이미 지나갔으므로 설명용 시전자를 여기서 붙인다
            if (live != null && skill is PsychoSlamSkill slam) slam.BindDescriptionCaster(live.skillCaster);
            OwnedChip chip = new OwnedChip(def, cells, skill, chips.Count) { IsNew = false, Origin = origin, Rotation = rotation };
            chips.Add(chip);
            if (equipped)
                pendingEquips.Add((chip, skillSlot));
            return chip;
        }

        // 보유 칩과 스킬 슬롯을 모두 비운다 (메인 화면의 새 게임·이어하기 직전)
        // 메인 화면으로 돌아와도 플레이어는 살아 있으므로, 장착해 둔 능력치 칩의 보너스를 먼저 뺀다
        // 저장은 부르지 않는다 (불러오는 도중에 빈 목록으로 저장 파일을 덮지 않게)
        public static void ResetAll()
        {
            if (live != null)
            {
                foreach (OwnedChip chip in chips)
                {
                    if (chip.IsEquipped && chip.Def is StatChipDef stat)
                        live.owner.CurrentStats.Remove(stat.StatType, stat.Value);
                }
            }

            chips.Clear();
            Array.Clear(skillSlots, 0, skillSlots.Length);
            pendingEquips.Clear();
            if (live != null)
                live.SkillSlotsChanged?.Invoke();
        }

        // 스킬 칩은 빈 스킬 슬롯이 있어야 장착할 수 있다
        public bool CanEquip(OwnedChip chip)
        {
            if (chip.IsEquipped)
                return false;

            return chip.Def is StatChipDef || FindEmptySkillSlot() >= 0;
        }

        // 장착 기록을 남기고 스탯 보너스를 더하거나 스킬 슬롯에 넣는다. 스킬 슬롯이 가득 차 있으면 장착하지 않고 false
        // preferredSlot: 스킬 칩을 넣고 싶은 슬롯. 비어 있으면 그 슬롯에, 아니면 가장 앞의 빈 슬롯에 넣는다
        //   (그리드 위에서 칩을 옮길 때 원래 번호를 지키려고 패널이 넘긴다)
        public bool Equip(OwnedChip chip, Vector2Int origin, int rotation, int preferredSlot = -1)
        {
            if (CanEquip(chip) == false)
                return false;

            chip.IsEquipped = true;
            chip.Origin = origin;
            chip.Rotation = rotation;

            if (chip.Def is StatChipDef stat)
            {
                owner.CurrentStats.Add(stat.StatType, stat.Value);
            }
            else
            {
                int slot = IsEmptySkillSlot(preferredSlot) ? preferredSlot : FindEmptySkillSlot();
                skillSlots[slot] = chip;
                SkillSlotsChanged?.Invoke();
            }

            Changed?.Invoke();
            return true;
        }

        public void Unequip(OwnedChip chip)
        {
            if (chip.IsEquipped == false)
                return;

            chip.IsEquipped = false;

            if (chip.Def is StatChipDef stat)
            {
                owner.CurrentStats.Remove(stat.StatType, stat.Value);
            }
            else
            {
                // 자리를 비워 두기만 하고 뒤의 스킬을 당기지 않는다
                skillSlots[GetSkillSlot(chip)] = null;
                SkillSlotsChanged?.Invoke();
            }

            Changed?.Invoke();
        }

        // 마을에서 아레나로, 아레나에서 마을로 씬이 바뀔 때마다 부른다
        // 장착 여부와 상관없이 모든 스킬 칩의 쿨타임을 끝내고, 패시브가 아레나에서 쌓은 효과를 지운다
        // 해제해 둔 칩도 멈춘 쿨타임을 들고 다음 씬으로 가지 않게 한다
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 메인 화면에서 이어하기를 눌렀으면 이미 Start가 지난 살아 있는 플레이어가 여기서 장착한다
            // 새 씬의 Awake·OnEnable 뒤에 불리므로 스킬 슬롯 HUD도 이미 구독해 있다
            if (live == this)
                ApplyPendingEquips();

            foreach (OwnedChip chip in chips)
            {
                if (chip.Skill == null)
                    continue;

                chip.Skill.ResetCooldown();
                if (chip.Skill is PassiveSkill passive)
                    passive.ResetStacks();
            }
        }

        // 칩이 들어 있는 스킬 슬롯 번호. 없으면 -1
        public int GetSkillSlot(OwnedChip chip) => Array.IndexOf(skillSlots, chip);

        // 스킬 슬롯 순서 바꾸기 (패널의 드래그앤드롭). 옮길 자리에 스킬이 있으면 서로 바꾸고, 비어 있으면 그 번호로 옮긴다
        public void SwapSkillSlots(int from, int to)
        {
            if (from == to)
                return;

            (skillSlots[from], skillSlots[to]) = (skillSlots[to], skillSlots[from]);
            SkillSlotsChanged?.Invoke();
            Changed?.Invoke();
        }

        private int FindEmptySkillSlot() => Array.IndexOf(skillSlots, null);

        private bool IsEmptySkillSlot(int slot) => slot >= 0 && slot < SkillSlotCount && skillSlots[slot] == null;
    }
}
