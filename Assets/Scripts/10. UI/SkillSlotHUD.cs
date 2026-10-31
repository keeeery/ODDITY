using System;
using System.Collections.Generic;
using DG.Tweening;
using TeamOdd.Player;
using TeamOdd.Superpower;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.UI
{
    // 화면 하단 중앙의 액티브 스킬 슬롯(1~5번), 장착한 스킬 이름과 남은 쿨타임을 보여준다.
    // 슬롯 줄 위에는 넉다운 조기 기상의 쿨타임이 도는 동안만 같은 모양의 기상기 슬롯을 띄운다.
    // 슬롯은 씬에 배치해 두고 참조로 연결한다. 컴포넌트 우클릭 → "1. Build Skill Slot UI" 로 자식 슬롯을 만든다.
    // 장착·해제·순서 변경은 인벤토리의 SkillSlotsChanged로 다시 그리고, 쿨타임은 매 프레임 스킬에서 읽는다.
    public class SkillSlotHUD : MonoBehaviour
    {
        // 슬롯 하나에 붙는 그래픽과 글자
        [Serializable]
        private class Slot
        {
            public SkillSlotGraphic Graphic;
            public TMP_Text Key;
            public TMP_Text Name;
            public TMP_Text Cooldown;
            [NonSerialized] public OwnedChip Chip;
            [NonSerialized] public bool WasCoolingDown;
            [NonSerialized] public Tween FlashTween;
        }

        [Header("References")]
        [Tooltip("비어 있으면 씬에서 찾는다.")]
        [SerializeField] private AugmentationChipInventory inventory;
        [Tooltip("기상기 쿨타임을 읽을 플레이어, 비어 있으면 씬에서 찾는다.")]
        [SerializeField] private PlayerController player;
        [Tooltip("1~5번 슬롯, 순서대로")]
        [SerializeField] private List<Slot> slots = new();
        [SerializeField] private Slot wakeUpSlot;
        [Tooltip("'1. Build Skill Slot UI' 로 만드는 글자에 쓰는 폰트, 비어 있으면 NotoSansKR-Regular SDF")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Style")]
        [Tooltip("모든 슬롯 그래픽에 같은 값을 넘긴다.")]
        [SerializeField] private SkillSlotStyle style = new();
        [Tooltip("이름 앞에서부터 보여줄 글자 수, 아이콘이 생기기 전까지 이름 첫 단어로 슬롯을 구분한다.")]
        [SerializeField, Min(1)] private int nameLength = 3;

        [Header("Ready Flash")]
        [Tooltip("쿨타임이 끝나는 순간 슬롯이 하얗게 번쩍였다 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float readyFlashDuration = 0.3f;

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<AugmentationChipInventory>();
            if (inventory == null) Debug.LogWarning($"{name}: 씬에 AugmentationChipInventory가 없어 스킬 슬롯을 비워 둡니다.", this);
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (slots.Count != AugmentationChipInventory.SkillSlotCount)
                Debug.LogWarning($"{name}: 배치된 스킬 슬롯이 {slots.Count}개로 {AugmentationChipInventory.SkillSlotCount}개와 다릅니다. '1. Build Skill Slot UI' 로 다시 만드세요.", this);

            ApplyStyle();
            UpdateWakeUpSlot();
        }

        // 그래픽은 스타일을 직렬화하지 않으므로 에디터에서도 HUD 값으로 보이게 한다.
        private void OnValidate() => ApplyStyle();

        // 인벤토리는 플레이어에 있어 이 HUD보다 오래 살 수 있으므로 꺼질 때 구독을 끊는다.
        private void OnEnable()
        {
            if (inventory == null) return;

            inventory.SkillSlotsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.SkillSlotsChanged -= Refresh;
            foreach (Slot slot in slots)
            {
                if (slot.Graphic == null) continue;
                slot.FlashTween?.Kill();
                slot.FlashTween = null;
                slot.Graphic.FlashAlpha = 0f;
            }
        }

        private void Update()
        {
            foreach (Slot slot in slots)
                UpdateCooldown(slot);
            UpdateWakeUpSlot();
        }

        private void ApplyStyle()
        {
            foreach (Slot slot in slots)
                if (slot.Graphic != null) slot.Graphic.SetStyle(style);
            if (wakeUpSlot?.Graphic != null) wakeUpSlot.Graphic.SetStyle(style);
        }

        private void Refresh()
        {
            int count = Mathf.Min(slots.Count, inventory.SkillSlots.Count);
            for (int index = 0; index < count; index++)
            {
                Slot slot = slots[index];
                if (slot.Graphic == null) continue;

                slot.Chip = inventory.SkillSlots[index];
                slot.Graphic.HasSkill = slot.Chip != null;
                slot.Name.text = slot.Chip != null ? GetShortName(slot.Chip.Def.Name) : "";
                // 장착하는 순간 쿨타임이 이미 끝나 있으면 번쩍이는 연출 없이 바로 사용 가능 상태로 둔다.
                slot.WasCoolingDown = slot.Chip != null && slot.Chip.Skill.IsReady == false;
                slot.FlashTween?.Kill();
                slot.FlashTween = null;
                slot.Graphic.FlashAlpha = 0f;
                UpdateCooldown(slot);
            }
        }

        private void UpdateCooldown(Slot slot)
        {
            if (slot.Graphic == null) return;

            SuperpowerSkill skill = slot.Chip?.Skill;
            float remaining = skill != null ? skill.RemainingCooldown : 0f;
            bool coolingDown = remaining > 0f;

            slot.Graphic.CooldownRatio = skill != null && skill.Cooldown > 0f ? remaining / skill.Cooldown : 0f;
            slot.Cooldown.text = coolingDown ? Mathf.CeilToInt(remaining).ToString() : "";

            // 쿨타임 중에는 이름을 흐리게 깔고 남은 초를 위에 띄운다.
            slot.Name.alpha = coolingDown ? 0.45f : 1f;
            slot.Key.alpha = slot.Chip != null ? 0.55f : 0.3f;

            if (slot.WasCoolingDown && coolingDown == false)
            {
                slot.FlashTween?.Kill();
                slot.Graphic.FlashAlpha = 1f;
                slot.FlashTween = DOTween.To(() => slot.Graphic.FlashAlpha,
                        value => slot.Graphic.FlashAlpha = value, 0f, readyFlashDuration)
                    .SetEase(Ease.Linear).OnKill(() => slot.FlashTween = null);
            }
            slot.WasCoolingDown = coolingDown;
        }

        // 기상기 슬롯은 조기 기상 쿨타임이 돌 때만 켠다.
        private void UpdateWakeUpSlot()
        {
            if (wakeUpSlot?.Graphic == null) return;

            float remaining = player != null ? player.EarlyWakeUpCooldown : 0f;
            bool coolingDown = remaining > 0f;
            GameObject slotObject = wakeUpSlot.Graphic.gameObject;
            if (slotObject.activeSelf != coolingDown) slotObject.SetActive(coolingDown);
            if (coolingDown == false) return;

            wakeUpSlot.Graphic.CooldownRatio = player.MaxEarlyWakeUpCooldown > 0f ? remaining / player.MaxEarlyWakeUpCooldown : 0f;
            wakeUpSlot.Cooldown.text = Mathf.CeilToInt(remaining).ToString();
        }

        // "시간 정지 칩" → "시간", 첫 단어를 nameLength 글자까지
        private string GetShortName(string chipName)
        {
            string firstWord = chipName.Trim().Split(' ')[0];
            return firstWord.Length > nameLength ? firstWord[..nameLength] : firstWord;
        }

#if UNITY_EDITOR
        private const string SkillSlotPrefix = "SkillSlot_";
        private const string WakeUpSlotName = "WakeUpSlot";
        private const string WakeUpName = "기상";
        private static readonly Vector2 SlotSize = new(54f, 54f);
        private const float SlotGap = 8f;
        private const float WakeUpSlotGap = 54f;
        private static readonly Vector2 WakeUpSlotSize = new(36f, 36f);
        private static readonly Color TextColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        private const float KeyFontSize = 11f;
        private const float NameFontSize = 13f;
        private const float CooldownFontSize = 15f;

        public bool HasBuiltSlots => slots.Count > 0 || wakeUpSlot?.Graphic != null;

        // 1~5번 슬롯을 한 줄로 놓고 이 오브젝트 크기를 줄에 맞춘 뒤, 줄 가운데 위에 기상기 슬롯을 둔다.
        [ContextMenu("1. Build Skill Slot UI")]
        public void BuildUI()
        {
            if (HasBuiltSlots)
            {
                Debug.LogWarning($"[{nameof(SkillSlotHUD)}] 슬롯이 이미 있습니다. 먼저 '2. Clear Skill Slot UI' 를 실행하세요.", this);
                return;
            }

            TMP_FontAsset textFont = font != null ? font : FindDefaultFont();
            int count = AugmentationChipInventory.SkillSlotCount;

            RectTransform self = (RectTransform)transform;
            Undo.RecordObject(self, "Build Skill Slot UI");
            self.sizeDelta = new Vector2(count * SlotSize.x + (count - 1) * SlotGap, SlotSize.y);

            List<Slot> built = new();
            for (int index = 0; index < count; index++)
            {
                Slot slot = NewSlot($"{SkillSlotPrefix}{index + 1}", textFont, out RectTransform rect);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(index * (SlotSize.x + SlotGap), 0f);
                slot.Key.text = SkillCaster.GetKeyLabel(index);
                slot.Key.alpha = 0.3f;
                built.Add(slot);
            }

            Slot wakeUp = NewSlot(WakeUpSlotName, textFont, out RectTransform wakeUpRect);
            wakeUpRect.anchorMin = wakeUpRect.anchorMax = new Vector2(0.5f, 1f);
            wakeUpRect.pivot = new Vector2(0.5f, 0f);
            wakeUpRect.anchoredPosition = new Vector2(0f, WakeUpSlotGap);
            wakeUpRect.sizeDelta = WakeUpSlotSize;
            wakeUp.Graphic.HasSkill = true;
            wakeUp.Key.text = Keyboard.current != null ? Keyboard.current[Key.Space].displayName : Key.Space.ToString();
            wakeUp.Key.alpha = 0.55f;
            wakeUp.Name.text = WakeUpName;
            wakeUp.Name.alpha = 0.45f;
            wakeUp.Cooldown.text = "5";

            Undo.RecordObject(this, "Build Skill Slot UI");
            slots = built;
            wakeUpSlot = wakeUp;
            ApplyStyle();
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(SkillSlotHUD)}] 스킬 슬롯 UI 생성 완료.", this);
        }

        [ContextMenu("2. Clear Skill Slot UI")]
        public void ClearUI()
        {
            List<GameObject> targets = new();
            foreach (Transform child in transform)
                if (child.name.StartsWith(SkillSlotPrefix) || child.name == WakeUpSlotName)
                    targets.Add(child.gameObject);

            foreach (GameObject target in targets)
                Undo.DestroyObjectImmediate(target);

            Undo.RecordObject(this, "Clear Skill Slot UI");
            slots = new List<Slot>();
            wakeUpSlot = null;
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(SkillSlotHUD)}] 슬롯 {targets.Count}개 삭제.", this);
        }

        private Slot NewSlot(string objectName, TMP_FontAsset textFont, out RectTransform rect)
        {
            GameObject slotObject = new GameObject(objectName, typeof(RectTransform));
            slotObject.layer = gameObject.layer;
            rect = (RectTransform)slotObject.transform;
            rect.SetParent(transform, false);
            rect.sizeDelta = SlotSize;
            Undo.RegisterCreatedObjectUndo(slotObject, "Build Skill Slot UI");

            SkillSlotGraphic graphic = slotObject.AddComponent<SkillSlotGraphic>();
            graphic.raycastTarget = false;

            return new Slot
            {
                Graphic = graphic,
                Key = NewText("Key", rect, textFont, KeyFontSize, TextAlignmentOptions.BottomLeft, new Vector4(4f, 2f, 4f, 2f)),
                Name = NewText("Name", rect, textFont, NameFontSize, TextAlignmentOptions.Center, Vector4.zero),
                Cooldown = NewText("Cooldown", rect, textFont, CooldownFontSize, TextAlignmentOptions.Center, Vector4.zero),
            };
        }

        private static TMP_Text NewText(string objectName, Transform parent, TMP_FontAsset textFont, float fontSize, TextAlignmentOptions alignment, Vector4 margin)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (textFont != null) text.font = textFont;
            text.text = "";
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = alignment;
            text.margin = margin;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_FontAsset FindDefaultFont()
        {
            foreach (string guid in AssetDatabase.FindAssets("NotoSansKR-Regular SDF t:TMP_FontAsset"))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }
#endif
    }
}
