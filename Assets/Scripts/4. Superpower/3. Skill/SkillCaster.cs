using Study.Utilities;
using TeamOdd.Combat;
using TeamOdd.Mission;
using TeamOdd.Player;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 발동 키로 스킬 슬롯의 스킬을 쓴다. 인벤토리가 있는 플레이어 오브젝트에 같이 붙인다.
    /// 슬롯에 어떤 스킬이 있는지는 모르고 SuperpowerSkill.TryUse만 부른다. (전략 패턴에서 전략을 쓰는 쪽. A_Coupon의 DiscountDemo 자리)
    /// UIManager.IsUIClose가 false이면 키를 받지 않는다. PsychokinesisController와 같은 기준이다.
    /// </summary>
    [RequireComponent(typeof(AugmentationChipInventory))]
    public class SkillCaster : MonoBehaviour
    {
        // i번 슬롯을 쓰는 키. 우선 숫자 1~5. 슬롯 UI의 키 표시도 여기를 읽으므로 키를 바꿀 때는 이 배열만 고친다
        public static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        private AugmentationChipInventory inventory;
        private CombatEntity owner;
        private Player.Player player; // 스킬을 쓸 때 스태미나를 줄인다
        private PlayerController playerController;

        [Header("Psycho Slam Override")]
        [Tooltip("이 플레이어의 내려찍기에 씬 전용 피해 값을 사용한다. 무력화는 이 피해에 SkillChip.tsv의 GroggyRate를 곱해 정한다.")]
        [SerializeField] private bool overridePsychoSlamDamage;
        [SerializeField, Min(1)] private int psychoSlamDamage = 120;

        public void ApplyPsychoSlamDamageOverride(ref int damage)
        {
            if (overridePsychoSlamDamage == false) return;
            damage = psychoSlamDamage;
        }

        // 슬롯 UI에 쓸 키 이름 ("1", "Q" 등 키보드 배열에 맞는 이름). 키가 없으면 빈 문자열
        public static string GetKeyLabel(int slot)
        {
            if (slot >= SlotKeys.Length || SlotKeys[slot] == Key.None)
                return "";

            Key key = SlotKeys[slot];
            return Keyboard.current != null ? Keyboard.current[key].displayName : key.ToString();
        }

        private void Awake()
        {
            inventory = GetComponent<AugmentationChipInventory>();
            owner = GetComponent<CombatEntity>();
            playerController = GetComponent<PlayerController>();
            player = GetComponent<Player.Player>();
        }

        private void Update()
        {
            int count = Mathf.Min(SlotKeys.Length, AugmentationChipInventory.SkillSlotCount);

            // UI 입력 제한이나 피격·행동 잠금으로 스킬을 쓸 수 없으면 채널링도 멈춘다
            if (Keyboard.current == null || UIManager.Instance.IsUIClose == false
                || (playerController != null && (playerController.IsHit || playerController.IsSuperpowerLocked)))
            {
                for (int i = 0; i < count; i++)
                    Release(i);
                return;
            }

            // 채널링 중에 정비소로 들어서면 그 자리에서 멈춘다
            bool inRepairShop = RepairShopZone.Contains(transform.position);

            for (int i = 0; i < count; i++)
            {
                // Key.None은 Keyboard.current[]에서 예외가 나므로 비워 둔 키는 건너뛴다
                if (SlotKeys[i] == Key.None)
                    continue;

                if (SimpleInput.GetKeyDown(SlotKeys[i]))
                    Use(i);
                else if (inRepairShop || SimpleInput.GetKeyUp(SlotKeys[i]))
                    Release(i);
            }
        }

        // 누르고 있는 동안 계속되는 스킬(채널링)을 멈춘다. 그런 스킬이 아니면 아무 일도 없다
        private void Release(int slot)
        {
            OwnedChip chip = inventory.SkillSlots[slot];
            if (chip != null)
                chip.Skill.Release();
        }

        private void Use(int slot)
        {
            if (owner != null && owner.IsDead)
                return;

            // 피격 중이거나 다른 스킬(염동 내려찍기 등)을 시전하는 동안은 쓰지 않는다.
            if (playerController != null && (playerController.IsHit || playerController.IsSuperpowerLocked))
                return;

            if (RepairShopZone.Contains(transform.position))
            {
                SystemMessage.Show(RepairShopZone.SkillBlockedMessage, SystemMessage.Tone.Warning);
                return;
            }

            // 염동 연사처럼 시전이 이어지는 스킬이 있으면 끝날 때까지 다른 스킬을 쓰지 않는다
            for (int i = 0; i < AugmentationChipInventory.SkillSlotCount; i++)
            {
                OwnedChip slotChip = inventory.SkillSlots[i];
                if (slotChip != null && slotChip.Skill.IsCasting)
                {
                    Debug.Log($"[SkillCaster] {slotChip.Skill.Name} 시전 중");
                    return;
                }
            }

            OwnedChip chip = inventory.SkillSlots[slot];
            if (chip == null)
            {
                SystemMessage.Show("장착된 스킬이 없습니다.", SystemMessage.Tone.Warning);
                return;
            }

            // 패시브는 키가 아니라 트리거로 발동한다 (PassiveSkillRunner)
            if (chip.Skill is PassiveSkill)
                return;

            if (chip.Skill.IsReady == false)
            {
                SystemMessage.Show("재사용 대기시간 중입니다.", SystemMessage.Tone.Warning);
                return;
            }

            // 슬롯에는 스킬 칩만 들어가므로 Def는 항상 SkillChipDef다
            float cost = ((SkillChipDef)chip.Def).Cost;
            if (player != null && player.Stamina < cost)
            {
                SystemMessage.Show(SystemMessage.NotEnoughStamina, SystemMessage.Tone.Warning);
                return;
            }

            // 대상이 없는 등으로 스킬이 나가지 않았으면 스태미나도 쓰지 않는다
            if (chip.Skill.TryUse(new SkillContext(transform, owner, GetAimDirection())) && player != null)
                player.ConsumeStamina(cost);
        }

        // 카메라가 보는 방향을 수평으로 눕힌 것. 카메라가 없으면 플레이어 정면
        private Vector3 GetAimDirection()
        {
            Transform reference = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }
    }
}
