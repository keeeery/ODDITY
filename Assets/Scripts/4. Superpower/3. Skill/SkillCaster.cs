using Study.Utilities;
using TeamOdd.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 발동 키로 스킬 슬롯의 스킬을 쓴다. 인벤토리가 있는 플레이어 오브젝트에 같이 붙인다.
    /// 슬롯에 어떤 스킬이 있는지는 모르고 SuperpowerSkill.TryUse만 부른다. (전략 패턴에서 전략을 쓰는 쪽. A_Coupon의 DiscountDemo 자리)
    /// 커서가 풀려 있으면(증강칩 패널이 열려 있거나 ESC로 풀었을 때) 키를 받지 않는다. PsychokinesisController와 같은 기준이다.
    /// </summary>
    [RequireComponent(typeof(AugmentationChipInventory))]
    public class SkillCaster : MonoBehaviour
    {
        // i번 슬롯을 쓰는 키. 우선 숫자 1~5. 슬롯 UI의 키 표시도 여기를 읽으므로 키를 바꿀 때는 이 배열만 고친다
        public static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        private AugmentationChipInventory inventory;
        private CombatEntity owner;

        [Header("Psycho Slam Override")]
        [Tooltip("이 플레이어의 내려찍기에 씬 전용 피해·무력화 값을 사용한다.")]
        [SerializeField] private bool overridePsychoSlamDamage;
        [SerializeField, Min(1)] private int psychoSlamDamage = 120;
        [SerializeField, Min(0)] private int psychoSlamGroggy = 60;

        public void ApplyPsychoSlamDamageOverride(ref int damage, ref int groggy)
        {
            if (overridePsychoSlamDamage == false) return;
            damage = psychoSlamDamage;
            groggy = psychoSlamGroggy;
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
        }

        private void Update()
        {
            if (Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            int count = Mathf.Min(SlotKeys.Length, AugmentationChipInventory.SkillSlotCount);
            for (int i = 0; i < count; i++)
            {
                // Key.None은 Keyboard.current[]에서 예외가 나므로 비워 둔 키는 건너뛴다
                if (SlotKeys[i] != Key.None && SimpleInput.GetKeyDown(SlotKeys[i]))
                    Use(i);
            }
        }

        private void Use(int slot)
        {
            if (owner != null && owner.IsDead)
                return;

            OwnedChip chip = inventory.SkillSlots[slot];
            if (chip == null)
            {
                Debug.Log($"[SkillCaster] {slot + 1}번 슬롯이 비어 있습니다.");
                return;
            }

            if (chip.Skill.TryUse(new SkillContext(transform, owner, GetAimDirection())) == false && chip.Skill.IsReady == false)
                Debug.Log($"[SkillCaster] {chip.Skill.Name} 쿨타임 {chip.Skill.RemainingCooldown:0.0}초 남음");
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
