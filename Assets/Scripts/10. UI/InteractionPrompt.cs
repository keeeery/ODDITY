using TeamOdd.Mission;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 말을 걸 수 있는 NPC가 있을 때 화면 가운데 조준점 바로 위에 띄우는 상호작용 안내. [F] 또는 [우클릭] 상호작용
    // 우클릭은 정비소 안에서만 되므로 그때만 함께 보인다. 대상은 외곽선을 그리는 NPC(MissionNPC.OutlinedNPC)를 따른다.
    // 조준점 링과 같은 때에 나타나 시선을 옮기지 않고 읽히게 한다. 자식 UI는 실행할 때 KeyHintBuilder로 만든다.
    // 로비 씬에는 Tools/Team Odd/Set Up Lobby Repair Shop 메뉴가 정비소 구역과 함께 놓는다.
    [RequireComponent(typeof(RectTransform))]
    public sealed class InteractionPrompt : MonoBehaviour
    {
        [Header("Style")]
        [Tooltip("비워두면 ReadableTextStyle의 폰트(Pretendard-Medium SDF)")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string actionLabel = "상호작용";
        [Tooltip("다른 Overlay 캔버스 위에 그리되 시스템 메시지(100)보다는 아래")]
        [SerializeField] private int sortingOrder = 90;

        [Header("Placement")]
        [Tooltip("화면 가운데(조준점)에서 안내 아래쪽까지의 거리(px, 기준 해상도). 조준점 링(반지름 18)과 겹치지 않게 둔다")]
        [SerializeField] private float crosshairGap = 36f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float fadeSpeed = 14f;

        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private CanvasGroup group;
        private GameObject orLabel;
        private GameObject rightClickKey;

        private void Awake()
        {
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
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            // 아래 가운데를 조준점 위 crosshairGap 지점에 맞춘다
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, crosshairGap);

            KeyHintBuilder.AddKey(row, "F", font, 1f);
            orLabel = KeyHintBuilder.AddSubText(row, "Or", "또는", font, 1f).gameObject;
            rightClickKey = KeyHintBuilder.AddKey(row, "우클릭", font, 1f);
            KeyHintBuilder.AddText(row, "Action", actionLabel, font, KeyHintBuilder.DefaultFontSize, KeyHintBuilder.TextColor);
        }

        private void LateUpdate()
        {
            MissionNPC npc = MissionNPC.OutlinedNPC;
            bool visible = npc != null;

            if (visible)
            {
                bool rightClick = npc.CanInteractWithRightClick;
                if (orLabel.activeSelf != rightClick) orLabel.SetActive(rightClick);
                if (rightClickKey.activeSelf != rightClick) rightClickKey.SetActive(rightClick);
            }

            float blend = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            group.alpha = Mathf.Lerp(group.alpha, visible ? 1f : 0f, blend);
            if (visible == false && group.alpha < 0.01f) group.alpha = 0f;
        }
    }
}
