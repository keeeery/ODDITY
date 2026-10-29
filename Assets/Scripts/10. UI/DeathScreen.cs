using DG.Tweening;
using TeamOdd.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.UI
{
    // 플레이어가 죽으면 화면이 흑백으로 바래며 어두워지고, 가운데에 "YOU DIED" 가 서서히 떠오르는 연출
    // - 흑백·어둡게: 코드로 만든 Global Volume (TimeReverseEffect 와 같은 방식)
    // - 글자: 자식 MessageGroup(CanvasGroup)의 alpha
    // UI Canvas 아래 빈 오브젝트에 붙이고, 컴포넌트 우클릭 → "1. Build Death Screen UI" 로 자식 UI 를 만든다.
    [RequireComponent(typeof(RectTransform))]
    public class DeathScreen : MonoBehaviour
    {
        [Header("References")]
        // 비워두면 Player 싱글톤에서 찾는다
        [SerializeField] private CombatEntity playerEntity;
        [SerializeField] private CanvasGroup messageGroup;
        [SerializeField] private TMP_Text messageText;

        [Header("Message")]
        [SerializeField] private string message = "YOU DIED";
        [SerializeField] private Color messageColor = new(0.62f, 0.07f, 0.07f, 1f);
        // 빌드할 때 글자에 적용할 폰트, 비워두면 TMP 기본 폰트
        [SerializeField] private TMP_FontAsset font;

        [Header("Gray Out")]
        // 채도 (-100이면 완전 흑백)
        [SerializeField, Range(-100f, 0f)] private float saturation = -100f;
        // 노출 (음수일수록 어두움)
        [SerializeField, Range(-3f, 0f)] private float postExposure = -0.8f;

        [Header("Timing (초, 시간 정지에도 흐름)")]
        // 죽은 뒤 회색으로 바래기 시작할 때까지
        [SerializeField, Min(0f)] private float grayDelay = 1f;
        [SerializeField, Min(0.01f)] private float grayDuration = 1.5f;
        // 회색으로 바래기 시작한 뒤 글자가 떠오르기 시작할 때까지
        [SerializeField, Min(0f)] private float messageDelay = 1f;
        [SerializeField, Min(0.01f)] private float messageDuration = 1.5f;

        private Volume volume;
        private Sequence sequence;
        private bool isShowing;              // 연출이 켜져 있는지
        private bool prevPostProcessing;     // 연출을 켜기 전 카메라의 Post Processing 설정

        private void Awake()
        {
            // 씬에서 크기나 순서가 흐트러져도 화면을 꽉 채우고 다른 HUD 위에 그려지도록 맞춘다
            Stretch((RectTransform)transform);
            transform.SetAsLastSibling();

            volume = CreateVolume();
            if (messageText != null) messageText.text = message;
            SetMessageAlpha(0f);
        }

        // Player 는 Instance 로 처음 불릴 때 찾아지므로 구독은 Start 에서 한다
        private void Start()
        {
            if (playerEntity == null) playerEntity = Player.Player.Instance.GetComponent<CombatEntity>();
            if (playerEntity == null)
            {
                Debug.LogWarning($"[{nameof(DeathScreen)}] 플레이어 CombatEntity 를 찾지 못해 사망 연출을 하지 않습니다.", this);
                return;
            }

            playerEntity.Died += HandleDied;
        }

        private void OnDestroy()
        {
            if (playerEntity != null) playerEntity.Died -= HandleDied;

            sequence?.Kill();
            if (isShowing) SetPostProcessing(prevPostProcessing);
            if (volume != null) Destroy(volume.gameObject);
        }

        private void HandleDied()
        {
            if (isShowing) return;

            isShowing = true;
            prevPostProcessing = SetPostProcessing(true);

            sequence?.Kill();
            sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true)
                .AppendInterval(grayDelay)
                .Append(DOTween.To(() => volume.weight, value => volume.weight = value, 1f, grayDuration)
                    .SetEase(Ease.InOutSine))
                .Insert(grayDelay + messageDelay,
                    DOTween.To(GetMessageAlpha, SetMessageAlpha, 1f, messageDuration).SetEase(Ease.InSine));
        }

        private float GetMessageAlpha() => messageGroup != null ? messageGroup.alpha : 0f;

        private void SetMessageAlpha(float alpha)
        {
            if (messageGroup != null) messageGroup.alpha = alpha;
        }

        // 카메라의 Post Processing을 켜거나 끄고 원래 값을 돌려준다. 꺼져 있으면 Volume이 보이지 않는다
        private static bool SetPostProcessing(bool on)
        {
            if (Camera.main == null) return false;

            UniversalAdditionalCameraData cameraData = Camera.main.GetUniversalAdditionalCameraData();
            bool prev = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = on;
            return prev;
        }

        // 흑백 + 어둡게 효과를 담은 Global Volume. weight 0으로 두었다가 죽으면 올린다
        private Volume CreateVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.saturation.Override(saturation);
            colorAdjustments.postExposure.Override(postExposure);

            // UI 오브젝트 아래에 두면 레이어가 섞이므로 씬 루트에 둔다
            GameObject volumeObject = new GameObject("DeathScreenVolume");

            Volume created = volumeObject.AddComponent<Volume>();
            created.isGlobal = true;
            created.priority = 100f; // 씬의 다른 Volume보다 위에서 섞이도록
            created.weight = 0f;
            created.sharedProfile = profile;
            return created;
        }

#if UNITY_EDITOR
        private const string MessageGroupName = "MessageGroup";

        [ContextMenu("1. Build Death Screen UI")]
        private void BuildUI()
        {
            if (transform.Find(MessageGroupName) != null)
            {
                Debug.LogWarning($"[{nameof(DeathScreen)}] '{MessageGroupName}' 이(가) 이미 있습니다. 먼저 '2. Clear Death Screen UI' 를 실행하세요.", this);
                return;
            }

            // 자신은 화면을 꽉 채우고, 다른 HUD 위에 그려지도록 맨 뒤로 보낸다
            RectTransform self = (RectTransform)transform;
            Undo.RecordObject(self, "Build Death Screen UI");
            Stretch(self);
            self.SetAsLastSibling();

            // 글자 묶음 (처음엔 투명)
            GameObject group = NewUI(MessageGroupName, transform);
            Stretch((RectTransform)group.transform);
            CanvasGroup canvasGroup = group.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // 글자 뒤 가로 띠
            GameObject band = NewUI("MessageBand", group.transform);
            SetRect((RectTransform)band.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 200f));
            Image bandImage = band.AddComponent<Image>();
            bandImage.color = new Color(0f, 0f, 0f, 0.55f);
            bandImage.raycastTarget = false;

            // 가운데 글자
            GameObject textObject = NewUI("MessageText", group.transform);
            SetRect((RectTransform)textObject.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 200f));
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = message;
            text.fontSize = 110f;
            text.characterSpacing = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = messageColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            Undo.RegisterCreatedObjectUndo(group, "Build Death Screen UI");

            Undo.RecordObject(this, "Build Death Screen UI");
            messageGroup = canvasGroup;
            messageText = text;
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(DeathScreen)}] 사망 화면 UI 생성 완료.", this);
        }

        [ContextMenu("2. Clear Death Screen UI")]
        private void ClearUI()
        {
            Transform group = transform.Find(MessageGroupName);
            if (group == null)
            {
                Debug.Log($"[{nameof(DeathScreen)}] 삭제할 '{MessageGroupName}' 이(가) 없습니다.", this);
                return;
            }

            Undo.DestroyObjectImmediate(group.gameObject);
            Undo.RecordObject(this, "Clear Death Screen UI");
            messageGroup = null;
            messageText = null;
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(DeathScreen)}] '{MessageGroupName}' 삭제.", this);
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

#endif

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
