using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 스킬 슬롯 한 칸. 발동 키, 칩 아이콘, 칩 이름, 쿨타임을 그린다.
    /// 패널의 스킬 슬롯 줄(SkillSlotBarView)과 게임 화면 스킬 바(SkillBarHUD)가 같이 쓴다.
    /// 슬롯 수가 고정이라 프리팹 없이 런타임에 만든다. 드래그 입력은 이벤트로 넘기기만 하고, 순서 변경은 SkillSlotBarView가 한다.
    /// </summary>
    public class SkillSlotView : MonoBehaviour, IPointerEnterHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // 런타임에 AddComponent로 붙이므로 인스펙터 값 대신 상수로 둔다
        private const string FillSpritePath = "WhiteSquare"; // Filled 이미지는 스프라이트가 있어야 채워진다 (PlayerHUD와 같은 스프라이트)
        // 여백과 글자 크기는 높이 BaseHeight 슬롯 기준이다. HUD 슬롯은 이 높이 그대로고, 증강칩 패널 슬롯은 더 커서 같은 비율로 키운다
        private const float BaseHeight = 56f;
        private const float Padding = 6f;
        private static readonly Color BackgroundColor = new(0.12f, 0.13f, 0.16f, 0.9f);
        private static readonly Color HighlightColor = new(0.30f, 0.75f, 0.45f, 0.9f);
        private static readonly Color CooldownColor = new(0f, 0f, 0f, 0.65f);
        private static readonly Color EmptyNameColor = new(1f, 1f, 1f, 0.35f);

        private Image background;
        private Image icon;
        private Image cooldownFill;
        private Text cooldownText;
        private Text nameLabel;

        public int Index { get; private set; }   // 슬롯 번호 (0부터). 발동 키는 SkillCaster.SlotKeys[Index]
        public RectTransform Rect { get; private set; }
        public OwnedChip Chip { get; private set; } // 빈 슬롯이면 null

        public event Action<SkillSlotView> PointerEntered;
        public event Action<SkillSlotView, PointerEventData> DragBegan;
        public event Action<SkillSlotView, PointerEventData> Dragged;
        public event Action<SkillSlotView, PointerEventData> DragEnded;

        // interactive: 포인터 입력을 받는다. HUD와 드래그 고스트는 false로 만들어 아래 UI를 가리지 않게 한다
        public static SkillSlotView Create(int index, Transform parent, Vector2 size, Font font, bool interactive)
        {
            RectTransform rect = CreateRect($"SkillSlot_{index + 1}", parent);
            rect.sizeDelta = size;

            SkillSlotView view = rect.gameObject.AddComponent<SkillSlotView>();
            view.Index = index;
            view.Rect = rect;
            view.Build(size, font, interactive);
            view.Show(null);
            return view;
        }

        private void Build(Vector2 size, Font font, bool interactive)
        {
            background = gameObject.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = interactive;

            float scale = size.y / BaseHeight;
            float padding = Padding * scale;

            // 아이콘은 슬롯 높이에 맞춘 정사각형으로 왼쪽에 둔다
            float iconSize = size.y - padding * 2f;
            Vector2 iconPosition = new Vector2(padding, -padding);
            icon = CreateImage("Icon", iconPosition, new Vector2(iconSize, iconSize));
            icon.preserveAspect = true;

            // 쿨타임은 아이콘 위를 어둡게 덮고 남은 비율만큼 위에서부터 줄어든다
            cooldownFill = CreateImage("Cooldown", iconPosition, new Vector2(iconSize, iconSize));
            cooldownFill.sprite = Resources.Load<Sprite>(FillSpritePath);
            cooldownFill.color = CooldownColor;
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Vertical;
            cooldownFill.fillOrigin = (int)Image.OriginVertical.Bottom;

            cooldownText = CreateText("CooldownText", font, iconPosition, new Vector2(iconSize, iconSize), Mathf.RoundToInt(16 * scale), TextAnchor.MiddleCenter);
            Text keyLabel = CreateText("Key", font, new Vector2(padding + 2f * scale, -padding), new Vector2(20f, 20f) * scale, Mathf.RoundToInt(14 * scale), TextAnchor.UpperLeft);
            keyLabel.text = SkillCaster.GetKeyLabel(Index);
            keyLabel.fontStyle = FontStyle.Bold;

            // 아이콘 오른쪽에 칩 이름을 쓴다
            float nameX = padding + iconSize + padding;
            nameLabel = CreateText("Name", font, new Vector2(nameX, -padding), new Vector2(size.x - nameX - padding, iconSize), Mathf.RoundToInt(13 * scale), TextAnchor.MiddleLeft);
        }

        // 슬롯에 든 칩을 그린다. null이면 빈 슬롯
        public void Show(OwnedChip chip)
        {
            Chip = chip;
            icon.enabled = chip != null;
            if (chip != null)
                icon.sprite = chip.Def.IconSprite;

            nameLabel.text = chip != null ? chip.Def.Name : "비어 있음";
            nameLabel.color = chip != null ? Color.white : EmptyNameColor;

            RefreshCooldown();
        }

        // 남은 쿨타임만큼 아이콘을 덮는다. 매 프레임 불러도 된다
        public void RefreshCooldown()
        {
            // 빈 슬롯이면 Chip이 null
            SuperpowerSkill skill = null;
            if (Chip != null)
                skill = Chip.Skill;
            float remaining = skill != null ? skill.RemainingCooldown : 0f;

            cooldownFill.enabled = remaining > 0f;
            cooldownFill.fillAmount = remaining > 0f && skill.Cooldown > 0f ? remaining / skill.Cooldown : 0f;
            cooldownText.text = remaining > 0f ? remaining.ToString("0.0") : "";
        }

        // 드래그 중에 놓을 수 있는 슬롯을 표시한다
        public void SetHighlight(bool on) => background.color = on ? HighlightColor : BackgroundColor;

        #region Input

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke(this);

        // 드래그는 왼쪽 버튼만 받는다 (ChipItemView와 같은 규칙)
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                DragBegan?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                DragEnded?.Invoke(this, eventData);
        }

        #endregion

        private Image CreateImage(string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, transform);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Font font, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, transform);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); // 좌상단 기준
            return rect;
        }
    }
}
