using System;
using System.Collections;
using DG.Tweening;
using TeamOdd.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.UI
{
    // 플레이어가 죽으면 신호가 끊기듯 화면이 꺼지고 "작전 실패"와 정비소 이동 카운트다운을 띄우는 연출
    // 히트스탑·슬로모션 → 채도가 빠지고 가장자리가 어두워짐 → 사망 애니메이션이 거의 끝나면 화면 전체와 HUD가 강하게 지직거리다 HUD가 사라짐
    // → 화면이 가로 선으로 접혔다가 점으로 꺼짐 → 검은 화면에 띠가 선에서 열리며 문구가 짧게 지직거리며 켜짐 → 카운트다운
    // - 화면 전체 효과: ScreenGlitch(PC_Renderer의 Screen Glitch 렌더러 기능)
    // - 띠·문구: 보상 화면과 같은 부품(UILineReveal·UIGlitch·UIStaggerReveal)을 세기만 다르게 쓴다.
    // UI Canvas 아래 빈 오브젝트에 붙이고, 컴포넌트 우클릭 → "1. Build Death Screen UI" 로 자식 UI 를 만든다.
    // 입력 잠금은 PlayerController.DeathAnimation이 맡는다. 연출은 실제 시간 기준이다.
    [RequireComponent(typeof(RectTransform))]
    public class DeathScreen : MonoBehaviour
    {
        [Header("References")]
        // 비워두면 Player 싱글톤에서 찾는다
        [SerializeField] private CombatEntity playerEntity;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private UILineReveal bandReveal;
        [SerializeField] private UIGlitch bandGlitch;
        [SerializeField] private UIStaggerReveal bandContent;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text countdownText;
        // 사망 순간 함께 지직거리다 사라질 HUD, 빌드할 때 부모 캔버스에 붙여 연결한다
        [SerializeField] private UIGlitch hudGlitch;

        [Header("Message")]
        [SerializeField] private string message = "작전 실패";
        // {0}에 남은 초가 들어간다
        [SerializeField] private string countdownFormat = "{0}초 뒤 정비소로 이동합니다.";
        [SerializeField] private string movingMessage = "정비소로 이동합니다.";
        // 빌드할 때 글자에 적용할 폰트, 비워두면 NotoSansKR-Regular SDF
        [SerializeField] private TMP_FontAsset font;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.12f;
        [SerializeField, Range(0.01f, 1f)] private float slowScale = 0.3f;
        [SerializeField, Min(0f)] private float slowHold = 0.6f;
        [SerializeField, Min(0f)] private float slowRecover = 0.4f;

        [Header("Signal Lost (초, 시간 정지에도 흐름)")]
        // 채도가 빠지고 가장자리가 어두워지는 시간
        [SerializeField, Min(0.01f)] private float drainDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float vignette = 0.8f;
        // 사망 애니메이션이 이만큼(0~1) 재생되면 화면 전체 글리치를 시작한다. 글리치·접힘 동안 쓰러지는 끝부분이 이어진다
        [SerializeField, Range(0f, 1f)] private float animationProgressToContinue = 0.7f;
        // 위 진행도에 닿은 뒤 글리치가 시작될 때까지
        [SerializeField, Min(0f)] private float afterAnimationDelay = 0f;
        // 사망 애니메이션을 찾지 못하거나 끝나지 않을 때 이 시간이 지나면 그냥 진행
        [SerializeField, Min(0f)] private float maxAnimationWait = 6f;
        [SerializeField, Min(0.01f)] private float glitchDuration = 0.5f;
        // 화면이 가로 선으로 접히는 시간
        [SerializeField, Min(0.01f)] private float foldDuration = 0.35f;
        // 선이 점으로 줄어드는 시간
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float dotFadeDuration = 0.3f;

        [Header("Message Timing")]
        // 화면이 접히기 시작한 뒤 문구 띠가 열리기 시작할 때까지 (0이면 꺼지는 연출과 동시에)
        [SerializeField, Min(0f)] private float messageDelay = 0f;
        [SerializeField, Min(0.01f)] private float messageGlitchDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float messageGlitchStrength = 0.6f;
        [SerializeField, Min(1)] private int countdownSeconds = 5;
        // 이동 문구를 보여 준 뒤 넘어가기까지
        [SerializeField, Min(0f)] private float movingHold = 1f;

        private static readonly int DeadState = Animator.StringToHash("Dead");

        private Animator playerAnimator;
        private Graphic blackout;            // 패널의 검은 배경, 화면이 다 꺼진 뒤에만 불투명하게 둔다
        private Sequence drain;              // 채도·가장자리, 사망 순간부터
        private Sequence sequence;           // 사망 애니메이션이 끝난 뒤의 화면 꺼짐·문구
        private Coroutine waitRoutine;
        private bool isShowing;              // 연출이 켜져 있는지

        // 카운트다운이 끝나 정비소로 넘어갈 때. 씬 이동은 아직 연결하지 않아 지금은 화면만 닫는다
        public event Action Finished;

        private void Awake()
        {
            // 씬에서 크기나 순서가 흐트러져도 화면을 꽉 채우고 다른 HUD 위에 그려지도록 맞춘다
            Stretch((RectTransform)transform);
            transform.SetAsLastSibling();

            if (messageText != null) messageText.text = message;
            if (panelRoot != null)
            {
                panelRoot.TryGetComponent(out blackout);
                panelRoot.SetActive(false);
            }
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
            playerAnimator = playerEntity.GetComponentInChildren<Animator>();
        }

        private void OnDestroy()
        {
            if (playerEntity != null) playerEntity.Died -= HandleDied;

            drain?.Kill();
            sequence?.Kill();
            if (isShowing) ScreenGlitch.Clear();
        }

        private void HandleDied()
        {
            if (isShowing) return;
            if (panelRoot == null || bandReveal == null || countdownText == null)
            {
                Debug.LogError($"[{nameof(DeathScreen)}] 자식 UI 가 연결되지 않았습니다. '1. Build Death Screen UI' 를 실행하세요.", this);
                return;
            }

            isShowing = true;
            // 누를 버튼이 없으므로 커서는 숨긴 채 둔다 (조작 잠금은 PlayerController.DeathAnimation 이 먼저 한다)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            HitStop.Request(hitStopDuration);
            SlowMotion.Play(slowScale, slowHold, slowRecover);

            sequence?.Kill();
            ScreenGlitch.Clear();
            panelRoot.SetActive(false);

            drain?.Kill();
            drain = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            drain.Insert(0f, DOTween.To(() => ScreenGlitch.Desaturate, value => ScreenGlitch.Desaturate = value, 1f, drainDuration)
                .SetEase(Ease.OutQuad));
            drain.Insert(0f, DOTween.To(() => ScreenGlitch.Vignette, value => ScreenGlitch.Vignette = value, vignette, drainDuration)
                .SetEase(Ease.OutQuad));

            // 쓰러지는 모습을 대부분 보여 준 뒤에 화면을 끈다
            waitRoutine = StartCoroutine(WaitDeathAnimation());
        }

        private IEnumerator WaitDeathAnimation()
        {
            float giveUpTime = Time.unscaledTime + maxAnimationWait;
            bool entered = false;
            while (Time.unscaledTime < giveUpTime && playerAnimator != null)
            {
                AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
                bool inDead = state.shortNameHash == DeadState && playerAnimator.IsInTransition(0) == false;
                if (inDead) entered = true;
                // 사망 상태는 나가는 전환이 없으므로 끝까지 재생되면 마지막 자세로 멈춰 있다
                if (entered && (inDead == false || state.normalizedTime >= animationProgressToContinue)) break;
                yield return null;
            }

            waitRoutine = null;
            BeginSignalLost();
        }

        // 화면 전체 글리치 → 선으로 접힘 → 점으로 꺼짐 → 문구 띠 → 카운트다운
        private void BeginSignalLost()
        {
            float glitchStart = afterAnimationDelay;
            float glitchEnd = glitchStart + glitchDuration;
            float messageTime = glitchEnd + messageDelay;
            float contentTime = messageTime + bandReveal.OpenDuration;

            sequence?.Kill();
            sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);

            float offTime = ScreenOffEffect.Insert(sequence, glitchStart, glitchDuration, foldDuration, shrinkDuration, dotFadeDuration);
            if (hudGlitch != null) sequence.InsertCallback(glitchStart, () => hudGlitch.Play(glitchDuration, 1f, true));

            // 화면이 접히는 동안 문구 띠가 같은 가운데 선에서 열린다. 배경은 꺼지는 화면이 보이도록 투명하게 둔다
            sequence.InsertCallback(messageTime, () =>
            {
                SetBlackoutAlpha(0f);
                panelRoot.SetActive(true);
                if (bandContent != null) bandContent.HideAll();
                bandReveal.Open();
            });
            // 다 꺼진 뒤에는 검은 배경으로 덮고 화면 효과를 걷어 둔다
            sequence.InsertCallback(offTime, () =>
            {
                SetBlackoutAlpha(1f);
                drain?.Kill();
                ScreenGlitch.Clear();
            });
            sequence.InsertCallback(contentTime, () =>
            {
                countdownText.text = string.Format(countdownFormat, countdownSeconds);
                if (bandContent != null) bandContent.Play();
                if (bandGlitch != null) bandGlitch.Play(messageGlitchDuration, messageGlitchStrength);
            });

            for (int second = 1; second < countdownSeconds; second++)
            {
                int remaining = countdownSeconds - second;
                sequence.InsertCallback(contentTime + second, () => countdownText.text = string.Format(countdownFormat, remaining));
            }
            sequence.InsertCallback(contentTime + countdownSeconds, () => countdownText.text = movingMessage);
            sequence.InsertCallback(contentTime + countdownSeconds + movingHold, Finish);
        }

        // 화면과 효과를 걷는다. 플레이어는 죽은 채이므로 조작은 계속 막아 둔다
        public void Close()
        {
            drain?.Kill();
            sequence?.Kill();
            sequence = null;
            if (waitRoutine != null) StopCoroutine(waitRoutine);
            waitRoutine = null;
            ScreenGlitch.Clear();
            if (hudGlitch != null) hudGlitch.Stop();
            if (bandGlitch != null) bandGlitch.Stop();
            SetBlackoutAlpha(1f);
            if (panelRoot != null) panelRoot.SetActive(false);
            if (isShowing == false) return;

            isShowing = false;
            if (SlowMotion.IsSlowed) SlowMotion.Resume();
        }

        private void Finish()
        {
            Close();
            Finished?.Invoke();
        }

        private void SetBlackoutAlpha(float alpha)
        {
            if (blackout == null) return;
            Color color = blackout.color;
            color.a = alpha;
            blackout.color = color;
        }

#if UNITY_EDITOR
        private const string PanelName = "Panel";
        private static readonly Color HighlightColor = new(0.93f, 0.91f, 0.87f, 0.9f);
        private static readonly Color TextColor = new(0.847f, 0.824f, 0.769f, 1f);

        [ContextMenu("1. Build Death Screen UI")]
        private void BuildUI()
        {
            if (transform.Find(PanelName) != null)
            {
                Debug.LogWarning($"[{nameof(DeathScreen)}] '{PanelName}' 이(가) 이미 있습니다. 먼저 '2. Clear Death Screen UI' 를 실행하세요.", this);
                return;
            }

            // 자신은 화면을 꽉 채우고, 다른 HUD 위에 그려지도록 맨 뒤로 보낸다
            RectTransform self = (RectTransform)transform;
            Undo.RecordObject(self, "Build Death Screen UI");
            Stretch(self);
            self.SetAsLastSibling();

            // HUD를 사라지게 하는 캔버스 그룹의 영향을 받지 않고, 보상 화면보다 위에 그린다
            if (TryGetComponent(out CanvasGroup selfGroup) == false) selfGroup = Undo.AddComponent<CanvasGroup>(gameObject);
            selfGroup.ignoreParentGroups = true;
            selfGroup.blocksRaycasts = false;
            if (TryGetComponent(out Canvas selfCanvas) == false) selfCanvas = Undo.AddComponent<Canvas>(gameObject);
            selfCanvas.overrideSorting = true;
            selfCanvas.sortingOrder = 300;

            // 처음엔 꺼 둔 검은 화면
            GameObject panel = NewUI(PanelName, transform);
            Stretch((RectTransform)panel.transform);
            Image blackout = panel.AddComponent<Image>();
            blackout.color = Color.black;
            blackout.raycastTarget = false;

            // 선에서 펼쳐지는 문구 띠
            GameObject reveal = NewUI("BandReveal", panel.transform);
            SetRect((RectTransform)reveal.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 140f));
            reveal.AddComponent<RectMask2D>();
            UILineReveal lineReveal = reveal.AddComponent<UILineReveal>();
            // 꺼진 화면 뒤라 보상 화면보다 천천히 연다
            SetValue(lineReveal, "lineDuration", 0.35f);
            SetValue(lineReveal, "openDuration", 0.45f);
            SetValue(lineReveal, "lineFadeDuration", 0.35f);

            GameObject band = NewUI("Band", reveal.transform);
            Stretch((RectTransform)band.transform);
            band.AddComponent<VectorPanel>().raycastTarget = false;
            band.AddComponent<CanvasGroup>();
            UIGlitch glitch = band.AddComponent<UIGlitch>();
            UIStaggerReveal content = band.AddComponent<UIStaggerReveal>();
            // 제목이 자리 잡은 뒤 안내 문구가 켜지도록 간격을 둔다
            SetValue(content, "interval", 0.25f);
            SetValue(content, "flashDuration", 0.5f);

            TMP_FontAsset textFont = font != null ? font : FindDefaultFont();
            TMP_Text title = NewText("MessageText", band.transform, textFont, message, 32f, TextColor,
                new Vector2(0f, 18f), new Vector2(1000f, 50f));
            Color subColor = TextColor;
            subColor.a = 0.55f;
            TMP_Text countdown = NewText("CountdownText", band.transform, textFont, string.Format(countdownFormat, countdownSeconds), 18f, subColor,
                new Vector2(0f, -26f), new Vector2(1000f, 30f));

            // 띠가 열리기 전 가운데 1px 선, 마스크에 가려지지 않게 띠 밖에 둔다
            GameObject line = NewUI("BandLine", panel.transform);
            ((RectTransform)line.transform).sizeDelta = new Vector2(0f, 1f);
            Image lineImage = line.AddComponent<Image>();
            lineImage.color = HighlightColor;
            lineImage.raycastTarget = false;

            SetReference(lineReveal, "line", lineImage);
            SetReferences(glitch, "splitGraphics", title);

            // 부모 캔버스(HUD)의 다른 자식들을 함께 지직거리게 한다
            UIGlitch hud = null;
            Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            if (parentCanvas != null)
            {
                GameObject hudObject = parentCanvas.gameObject;
                if (hudObject.TryGetComponent(out CanvasGroup hudGroup) == false) hudGroup = Undo.AddComponent<CanvasGroup>(hudObject);
                if (hudObject.TryGetComponent(out hud) == false) hud = Undo.AddComponent<UIGlitch>(hudObject);
                System.Collections.Generic.List<UnityEngine.Object> targets = new();
                foreach (Transform child in parentCanvas.transform)
                    if (child != transform) targets.Add(child);
                SetReference(hud, "group", hudGroup);
                SetReferences(hud, "targets", targets.ToArray());
            }

            panel.SetActive(false);
            Undo.RegisterCreatedObjectUndo(panel, "Build Death Screen UI");

            Undo.RecordObject(this, "Build Death Screen UI");
            panelRoot = panel;
            bandReveal = lineReveal;
            bandGlitch = glitch;
            bandContent = content;
            messageText = title;
            countdownText = countdown;
            hudGlitch = hud;
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(DeathScreen)}] 사망 화면 UI 생성 완료.", this);
        }

        [ContextMenu("2. Clear Death Screen UI")]
        private void ClearUI()
        {
            Transform panel = transform.Find(PanelName);
            if (panel == null)
            {
                Debug.Log($"[{nameof(DeathScreen)}] 삭제할 '{PanelName}' 이(가) 없습니다.", this);
                return;
            }

            Undo.DestroyObjectImmediate(panel.gameObject);
            Undo.RecordObject(this, "Clear Death Screen UI");
            panelRoot = null;
            bandReveal = null;
            bandGlitch = null;
            bandContent = null;
            messageText = null;
            countdownText = null;
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(DeathScreen)}] '{PanelName}' 삭제.", this);
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset textFont, string text, float size, Color color,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject go = NewUI(name, parent);
            SetRect((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPosition, sizeDelta);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            if (textFont != null) label.font = textFont;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_FontAsset FindDefaultFont()
        {
            foreach (string guid in AssetDatabase.FindAssets("NotoSansKR-Regular SDF t:TMP_FontAsset"))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetValue(UnityEngine.Object target, string field, float value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetReferences(UnityEngine.Object target, string field, params UnityEngine.Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedProperties();
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
