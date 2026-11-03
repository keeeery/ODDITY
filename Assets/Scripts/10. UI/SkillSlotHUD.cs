using System;
using System.Collections.Generic;
using DG.Tweening;
using TeamOdd.Player;
using TeamOdd.Superpower;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.UI
{
    // 화면 하단 중앙의 액티브 스킬 슬롯(1~5번), 장착한 스킬 아이콘과 남은 쿨타임을 보여준다.
    // 슬롯 줄 위에는 넉다운 조기 기상의 쿨타임이 도는 동안만 같은 모양의 기상기 슬롯을 띄운다.
    // 슬롯은 씬에 배치해 두고 참조로 연결한다. 컴포넌트 우클릭 → "1. Build Skill Slot UI" 로 자식 슬롯을 만든다.
    // 장착·해제·순서 변경은 인벤토리의 SkillSlotsChanged로 다시 그리고, 쿨타임은 매 프레임 스킬에서 읽는다.
    // 스킬 아이콘 이미지는 이미 배치된 씬과 프리팹에도 들어가도록 시작할 때 슬롯마다 만든다.
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
            [NonSerialized] public Image Icon;
            [NonSerialized] public OwnedChip Chip;
            [NonSerialized] public bool WasCoolingDown;
            [NonSerialized] public Tween FlashTween;
            [NonSerialized] public Tween PunchTween;
        }

        // 칩 칸 스프라이트 하나에 슬롯 바탕색 하나를 짝지어 둔다.
        [Serializable]
        private class SkillColor
        {
            public Sprite UnitSprite;
            public Color Color = Color.white;
        }

        // 플레이어는 씬을 넘어 살아 있고 새 씬에 놓인 플레이어는 중복으로 파괴되므로, 살아 있는 플레이어(Player.Instance)를 먼저 쓴다.
        // 아래 참조는 살아 있는 플레이어가 아직 없을 때만 쓴다.
        [Header("References")]
        [Tooltip("살아 있는 플레이어가 없을 때 쓴다. 비어 있으면 씬에서 찾는다.")]
        [SerializeField] private AugmentationChipInventory inventory;
        [Tooltip("기상기 쿨타임을 읽을 플레이어, 살아 있는 플레이어가 없을 때 쓴다. 비어 있으면 씬에서 찾는다.")]
        [SerializeField] private PlayerController player;
        [Tooltip("1~5번 슬롯, 순서대로")]
        [SerializeField] private List<Slot> slots = new();
        [SerializeField] private Slot wakeUpSlot;
        [Tooltip("'1. Build Skill Slot UI' 로 만드는 글자에 쓰는 폰트, 비어 있으면 NotoSansKR-Regular SDF")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Style")]
        [Tooltip("모든 슬롯 그래픽에 같은 값을 넘긴다.")]
        [SerializeField] private SkillSlotStyle style = new();
        [Tooltip("슬롯 테두리와 스킬 아이콘 사이 여백")]
        [SerializeField, Min(0f)] private float iconPadding = 6f;
        [Tooltip("장착한 칩의 칸 스프라이트(UnitSprite)에 맞춰 슬롯 바탕을 칠할 색, 목록에 없는 칩은 Style의 Track Color로 칠한다.")]
        [SerializeField] private List<SkillColor> skillColors = new();

        [Header("Cooldown Text")]
        [Tooltip("쿨타임 중 아이콘 투명도, 낮을수록 남은 초가 잘 보인다.")]
        [SerializeField, Range(0f, 1f)] private float cooldownIconAlpha = 0.35f;

        [Header("Ready Flash")]
        [Tooltip("쿨타임이 끝나거나 당겨지는 순간 슬롯이 하얗게 번쩍였다 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float readyFlashDuration = 0.3f;

        [Header("Cooldown Reduced Punch")]
        [Tooltip("쿨타임이 당겨지는 순간 슬롯이 커지는 정도, 0.15면 1.15배까지 커졌다 돌아온다.")]
        [SerializeField, Min(0f)] private float reducedPunchScale = 0.15f;
        [Tooltip("커졌다 돌아오는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float reducedPunchDuration = 0.25f;

        // 지금 SkillSlotsChanged를 구독하고 있는 인벤토리
        private AugmentationChipInventory boundInventory;

        private void Awake()
        {
            if (slots.Count != AugmentationChipInventory.SkillSlotCount)
                Debug.LogWarning($"{name}: 배치된 스킬 슬롯이 {slots.Count}개로 {AugmentationChipInventory.SkillSlotCount}개와 다릅니다. '1. Build Skill Slot UI' 로 다시 만드세요.", this);

            ApplyStyle();
            CreateIcons();
            UpdateWakeUpSlot();
        }

        // 그래픽은 스타일을 직렬화하지 않으므로 에디터에서도 HUD 값으로 보이게 한다.
        private void OnValidate() => ApplyStyle();

        // 인벤토리는 플레이어에 있어 이 HUD보다 오래 살 수 있으므로 꺼질 때 구독을 끊는다.
        private void OnEnable() => BindPlayer();

        // 처음 켜질 때는 씬의 플레이어가 아직 Awake 전일 수 있으므로, 모든 Awake가 끝난 뒤 살아 있는 플레이어로 다시 맞춘다.
        private void Start()
        {
            BindPlayer();
            if (boundInventory == null) Debug.LogWarning($"{name}: 씬에 AugmentationChipInventory가 없어 스킬 슬롯을 비워 둡니다.", this);
        }

        private void OnDisable()
        {
            if (boundInventory != null) boundInventory.SkillSlotsChanged -= Refresh;
            boundInventory = null;
            foreach (Slot slot in slots)
            {
                // 칩은 씬을 넘어 남으므로 쿨타임 감소 알림을 끊어 둔다.
                if (slot.Chip != null) slot.Chip.Skill.CooldownReduced -= HandleCooldownReduced;
                slot.Chip = null;
                if (slot.Graphic == null) continue;
                slot.FlashTween?.Kill();
                slot.FlashTween = null;
                slot.Graphic.FlashAlpha = 0f;
                slot.PunchTween?.Kill();
                slot.PunchTween = null;
                slot.Graphic.transform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            // 연결한 플레이어가 중복으로 파괴됐으면 살아 있는 플레이어로 다시 연결
            if (boundInventory == null || player == null) BindPlayer();

            foreach (Slot slot in slots)
                UpdateCooldown(slot);
            UpdateWakeUpSlot();
        }

        // 살아 있는 플레이어의 인벤토리·컨트롤러에 연결하고, 인벤토리가 바뀌면 구독을 옮겨 다시 그린다.
        private void BindPlayer()
        {
            AugmentationChipInventory targetInventory = inventory;
            if (Player.Player.IsInitialized)
            {
                Player.Player alive = Player.Player.Instance;
                targetInventory = alive.GetComponent<AugmentationChipInventory>();
                player = alive.PlayerController != null ? alive.PlayerController : alive.GetComponent<PlayerController>();
            }
            if (targetInventory == null) targetInventory = FindAnyObjectByType<AugmentationChipInventory>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            if (targetInventory == boundInventory) return;

            if (boundInventory != null) boundInventory.SkillSlotsChanged -= Refresh;
            boundInventory = targetInventory;
            if (boundInventory == null) return;

            boundInventory.SkillSlotsChanged += Refresh;
            Refresh();
        }

        private void ApplyStyle()
        {
            foreach (Slot slot in slots)
                if (slot.Graphic != null) slot.Graphic.SetStyle(style);
            if (wakeUpSlot?.Graphic != null) wakeUpSlot.Graphic.SetStyle(style);
        }

        // 아이콘은 슬롯 그래픽 위, 글자(키, 남은 초) 아래에 둔다.
        private void CreateIcons()
        {
            foreach (Slot slot in slots)
            {
                if (slot.Graphic == null) continue;

                GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
                iconObject.layer = slot.Graphic.gameObject.layer;
                RectTransform rect = (RectTransform)iconObject.transform;
                rect.SetParent(slot.Graphic.transform, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(iconPadding, iconPadding);
                rect.offsetMax = new Vector2(-iconPadding, -iconPadding);

                slot.Icon = iconObject.AddComponent<Image>();
                slot.Icon.preserveAspect = true;
                slot.Icon.raycastTarget = false;
                slot.Icon.enabled = false;
            }
        }

        private void Refresh()
        {
            int count = Mathf.Min(slots.Count, boundInventory.SkillSlots.Count);
            for (int index = 0; index < count; index++)
            {
                Slot slot = slots[index];
                if (slot.Graphic == null) continue;

                // 아드레날린 과분비 칩처럼 쿨타임을 당겨 주는 효과가 있으면 슬롯을 번쩍이도록 지금 칩의 알림만 받는다.
                if (slot.Chip != null) slot.Chip.Skill.CooldownReduced -= HandleCooldownReduced;
                slot.Chip = boundInventory.SkillSlots[index];
                if (slot.Chip != null) slot.Chip.Skill.CooldownReduced += HandleCooldownReduced;
                slot.Graphic.HasSkill = slot.Chip != null;
                slot.Icon.enabled = slot.Chip != null;
                if (slot.Chip != null) slot.Icon.sprite = slot.Chip.Def.IconSprite;
                slot.Graphic.SkillColor = slot.Chip != null ? FindSkillColor(slot.Chip) : null;
                // 장착하는 순간 쿨타임이 이미 끝나 있으면 번쩍이는 연출 없이 바로 사용 가능 상태로 둔다.
                slot.WasCoolingDown = slot.Chip != null && slot.Chip.Skill.IsReady == false;
                slot.FlashTween?.Kill();
                slot.FlashTween = null;
                slot.Graphic.FlashAlpha = 0f;
                UpdateCooldown(slot);
            }
        }

        // 칩의 칸 스프라이트와 짝지은 색, 목록에 없으면 null
        private Color? FindSkillColor(OwnedChip chip)
        {
            foreach (SkillColor entry in skillColors)
                if (entry.UnitSprite == chip.Def.UnitSprite) return entry.Color;
            return null;
        }

        private void UpdateCooldown(Slot slot)
        {
            if (slot.Graphic == null) return;

            SuperpowerSkill skill = slot.Chip?.Skill;
            float remaining = skill != null ? skill.RemainingCooldown : 0f;
            bool coolingDown = remaining > 0f;

            slot.Graphic.CooldownRatio = skill != null && skill.Cooldown > 0f ? remaining / skill.Cooldown : 0f;
            // 전투당 한 번인 스킬은 쓴 뒤 덮개와 흐린 아이콘만 남기고 남은 초는 띄우지 않는다.
            slot.Cooldown.text = coolingDown && skill.IsOncePerBattle == false ? Mathf.CeilToInt(remaining).ToString() : "";

            // 쿨타임 중에는 아이콘을 흐리게 깔고 남은 초를 위에 띄운다.
            slot.Icon.color = new Color(1f, 1f, 1f, coolingDown ? cooldownIconAlpha : 1f);
            slot.Key.alpha = slot.Chip != null ? 0.55f : 0.3f;

            if (slot.WasCoolingDown && coolingDown == false) Flash(slot);
            slot.WasCoolingDown = coolingDown;
        }

        // 쿨타임이 당겨진 스킬이 든 슬롯을 번쩍이고 살짝 커졌다 돌아오게 한다.
        private void HandleCooldownReduced(SuperpowerSkill skill)
        {
            foreach (Slot slot in slots)
            {
                if (slot.Chip == null || slot.Chip.Skill != skill) continue;

                Flash(slot);
                // 연달아 맞아 펀치가 겹치면 크기가 어긋나므로 원래 크기로 돌려놓고 다시 시작한다.
                slot.PunchTween?.Kill();
                slot.Graphic.transform.localScale = Vector3.one;
                slot.PunchTween = slot.Graphic.transform.DOPunchScale(Vector3.one * reducedPunchScale, reducedPunchDuration, 1, 0f)
                    .OnKill(() => slot.PunchTween = null);
            }
        }

        // 슬롯이 하얗게 번쩍였다 사라진다. 쿨타임이 끝날 때와 쿨타임이 당겨졌을 때 같이 쓴다.
        private void Flash(Slot slot)
        {
            slot.FlashTween?.Kill();
            slot.Graphic.FlashAlpha = 1f;
            slot.FlashTween = DOTween.To(() => slot.Graphic.FlashAlpha,
                    value => slot.Graphic.FlashAlpha = value, 0f, readyFlashDuration)
                .SetEase(Ease.Linear).OnKill(() => slot.FlashTween = null);
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

#if UNITY_EDITOR
        private const string SkillSlotPrefix = "SkillSlot_";
        private const string WakeUpSlotName = "WakeUpSlot";
        private static readonly Vector2 SlotSize = new(54f, 54f);
        private const float SlotGap = 8f;
        private const float WakeUpSlotGap = 30f;
        private static readonly Vector2 WakeUpSlotSize = new(36f, 36f);
        private static readonly Color TextColor = new(0.84705883f, 0.8235294f, 0.76862746f, 1f);
        private const float KeyFontSize = 11f;
        private const float NameFontSize = 13f;
        private const float CooldownFontSize = 20f;

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
            // 기상기 슬롯은 이름 없이 키만 가운데에 둔다.
            wakeUp.Key.alignment = TextAlignmentOptions.Center;
            wakeUp.Key.margin = Vector4.zero;
            wakeUp.Key.alpha = 0.55f;
            wakeUp.Cooldown.text = "8";

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
                Cooldown = NewReadableText("Cooldown", rect, CooldownFontSize),
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

        // 남은 초는 칩 바탕 위에서도 읽히도록 외곽선·그림자를 켠 Readable 스타일 폰트로 쓴다.
        private TMP_Text NewReadableText(string objectName, Transform parent, float fontSize)
        {
            TMP_FontAsset readableFont = ReadableTextStyle.Font;
            TMP_Text text = NewText(objectName, parent, readableFont != null ? readableFont : font, fontSize, TextAlignmentOptions.Center, Vector4.zero);
            text.color = Color.white;
            ReadableTextStyle.Apply(text);
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
