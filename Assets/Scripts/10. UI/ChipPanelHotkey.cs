using Study.Utilities;
using TeamOdd.Superpower;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 로비 어디서든 E로 증강 칩 장착 창(AugmentationChipPanel)을 열고 닫는다. 다른 창이 열려 있으면 열지 않는다.
    // 화면 아래 가운데, 시스템 메시지 바로 위에 [E] 증강 칩 장착 안내를 띄우고, 창이 열려 있는 동안은 숨긴다.
    // 안내는 상호작용 안내(InteractionPrompt)와 같은 생김새로, 자식 UI는 실행할 때 KeyHintBuilder로 만든다.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ChipPanelHotkey : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비어 있으면 씬에서 찾는다(꺼져 있는 것 포함).")]
        [SerializeField] private AugmentationChipPanel panel;

        [Header("Input")]
        [SerializeField] private Key key = Key.E;

        [Header("Style")]
        [Tooltip("비워두면 ReadableTextStyle의 폰트(Pretendard-Medium SDF)")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string openLabel = "증강 칩 관리";
        [Tooltip("다른 Overlay 캔버스 위에 그리되 시스템 메시지(100)보다는 아래")]
        [SerializeField] private int sortingOrder = 90;

        [Header("Placement")]
        [Tooltip("화면 아래에서 안내 아래쪽까지의 거리(px, 기준 해상도). 시스템 메시지(아래에서 204~236)보다 조금 위")]
        [SerializeField] private float bottomOffset = 248f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float fadeSpeed = 14f;

        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private CanvasGroup group;
        // 열린 창 여부에 따른 자기 알파, 씬 전환 뒤에는 SceneLoadManager.UIRevealAlpha 를 곱해 다른 UI와 함께 나타난다
        private float shown;

        private bool IsOpen => panel != null && panel.gameObject.activeSelf;

        private void Awake()
        {
            if (panel == null) panel = FindAnyObjectByType<AugmentationChipPanel>(FindObjectsInactive.Include);
            if (panel == null) Debug.LogWarning($"{name}: 씬에 AugmentationChipPanel이 없어 {key} 키로 증강 칩 장착 창을 열 수 없습니다.", this);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform row = KeyHintBuilder.NewRow("Row", transform, 1f);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            // 아래 가운데를 화면 아래에서 bottomOffset 지점에 맞춘다
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, bottomOffset);

            KeyHintBuilder.AddKey(row, key.ToString(), font, 1f);
            KeyHintBuilder.AddText(row, "Action", openLabel, font, KeyHintBuilder.DefaultFontSize, KeyHintBuilder.TextColor);
        }

        private void Update()
        {
            if (panel == null) return;

            if (SimpleInput.GetKeyDown(key))
            {
                bool open = IsOpen == false;
                // 임무 창처럼 다른 창이 열려 있으면 그 위에 겹쳐 열지 않는다
                if (open && UIManager.Instance.IsUIClose == false) return;

                AugmentationChipInventory inventory = Player.Player.IsInitialized ? Player.Player.Instance.GetComponent<AugmentationChipInventory>() : null;
                if (open && inventory == null) return;

                panel.IsActivePanel(open, inventory);
                UIManager.Instance.LockCursor(open == false, open);
            }
            else if (IsOpen && SimpleInput.GetKeyDown(Key.Escape))
            {
                panel.Close();
                UIManager.Instance.LockCursor(true);
            }
        }

        // 열린 창이 없을 때만 열기 안내를 띄운다 (장착 창 위에 겹쳐 보이지 않도록 창이 열려 있으면 숨김)
        private void LateUpdate()
        {
            bool visible = panel != null && IsOpen == false && UIManager.Instance.IsUIClose;

            float blend = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            shown = Mathf.Lerp(shown, visible ? 1f : 0f, blend);
            if (visible == false && shown < 0.01f) shown = 0f;
            group.alpha = shown * SceneLoadManager.UIRevealAlpha;
        }
    }
}
