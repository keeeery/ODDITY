using DG.Tweening;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamOdd.UI
{
    // 보스 등장 연출 중 화면 중앙에서 약간 오른쪽 아래에 수식어와 이름을 왼쪽 정렬로 띄우는 UI
    // 수식어(작게) 위, 이름(크게) 아래. 옆으로 살짝 밀려 들어오며 나타나고 제자리에서 흐려지며 사라진다.
    // UI Canvas 아래 빈 오브젝트에 붙이고, 컴포넌트 우클릭 → "1. Build Boss Intro Title UI" 로 자식 UI 를 만든다.
    // 표시는 BossIntroCinematic이 Show/Hide로 부른다. 연출은 실제 시간 기준이다.
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class BossIntroTitle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text epithetText;
        [SerializeField] private TMP_Text nameText;

        [Header("Layout")]
        [Tooltip("화면 중앙에서 글자 블록 왼쪽 위 모서리까지의 거리(px, 기준 해상도). +x 오른쪽, -y 아래")]
        [SerializeField] private Vector2 screenOffset = new(160f, -60f);
        [Tooltip("빌드할 때 글자에 적용할 폰트, 비워두면 NotoSansKR-Regular SDF")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("HUD와 보상 화면 위에 그려지도록 쓰는 정렬 순서, 사망 화면(300)보다는 아래")]
        [SerializeField] private int sortingOrder = 250;

        [Header("Timing (초)")]
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.45f;
        [Tooltip("나타날 때 왼쪽에서 밀려 들어오는 거리(px)")]
        [SerializeField] private float slideDistance = 40f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.3f;

        private CanvasGroup group;
        private Sequence sequence;

        // 화면 가운데에서 글자 블록 왼쪽 끝까지 가로 거리, 화면 너비 비율. 루트가 화면 전체를 덮는다고 봄
        public float HorizontalOffsetRatio
        {
            get
            {
                float width = ((RectTransform)transform).rect.width;
                return width > 0f ? screenOffset.x / width : 0f;
            }
        }

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            // 연출 동안 HUD 캔버스 그룹을 숨겨도 이름은 보이도록 부모 그룹을 무시
            // 부모 캔버스와 같은 묶음이면 부모 알파가 0일 때 함께 그려지지 않으므로 사망 화면처럼 자체 캔버스로 분리
            group.ignoreParentGroups = true;
            if (TryGetComponent(out Canvas canvas) == false) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        private void OnDestroy()
        {
            sequence?.Kill();
        }

        public void Show(string bossName, string epithet)
        {
            if (nameText != null) nameText.text = bossName;
            if (epithetText != null)
            {
                epithetText.text = epithet;
                epithetText.gameObject.SetActive(string.IsNullOrEmpty(epithet) == false);
            }

            sequence?.Kill();
            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Join(group.DOFade(1f, fadeInDuration).From(0f).SetEase(Ease.OutCubic));

            if (content != null)
            {
                content.anchoredPosition = screenOffset + Vector2.left * slideDistance;
                sequence.Join(content.DOAnchorPos(screenOffset, fadeInDuration).SetEase(Ease.OutCubic));
            }

            sequence.OnKill(() => sequence = null);
        }

        public void Hide()
        {
            sequence?.Kill();
            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Join(group.DOFade(0f, fadeOutDuration).SetEase(Ease.InCubic))
                .OnKill(() => sequence = null);
        }

#if UNITY_EDITOR
        private const string ContentName = "Boss Intro Title";
        private static readonly Color TextColor = new(0.847f, 0.824f, 0.769f, 1f);

        [ContextMenu("1. Build Boss Intro Title UI")]
        private void BuildUI()
        {
            if (transform.Find(ContentName) != null)
            {
                Debug.Log($"[{nameof(BossIntroTitle)}] 이미 '{ContentName}' 이(가) 있습니다. 먼저 지우고 다시 만드세요.", this);
                return;
            }

            // 화면 전체를 덮는 루트, 글자 블록은 그 중앙을 기준으로 둔다
            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            GameObject block = NewUI(ContentName, transform);
            RectTransform blockRect = (RectTransform)block.transform;
            blockRect.anchorMin = blockRect.anchorMax = new Vector2(0.5f, 0.5f);
            blockRect.pivot = new Vector2(0f, 1f);
            blockRect.anchoredPosition = screenOffset;
            blockRect.sizeDelta = new Vector2(900f, 110f);

            TMP_FontAsset textFont = font != null ? font : FindDefaultFont();
            Color subColor = TextColor;
            subColor.a = 0.65f;
            TMP_Text epithet = NewText("EpithetText", block.transform, textFont, "수식어", 22f, subColor, 0f, 32f);
            TMP_Text title = NewText("NameText", block.transform, textFont, "보스 이름", 60f, TextColor, -32f, 76f);
            title.fontStyle = FontStyles.Bold;

            Undo.RegisterCreatedObjectUndo(block, "Build Boss Intro Title UI");
            Undo.RecordObject(this, "Build Boss Intro Title UI");
            content = blockRect;
            epithetText = epithet;
            nameText = title;
            EditorUtility.SetDirty(this);

            Debug.Log($"[{nameof(BossIntroTitle)}] 보스 등장 타이틀 UI 생성 완료.", this);
        }

        [ContextMenu("2. Clear Boss Intro Title UI")]
        private void ClearUI()
        {
            Transform block = transform.Find(ContentName);
            if (block == null) return;

            Undo.DestroyObjectImmediate(block.gameObject);
            Undo.RecordObject(this, "Clear Boss Intro Title UI");
            content = null;
            epithetText = null;
            nameText = null;
            EditorUtility.SetDirty(this);
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        // 블록 위쪽 기준으로 y만큼 내려와 가로로 꽉 차는 왼쪽 정렬 글자
        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset textFont, string text, float size, Color color,
            float top, float height)
        {
            GameObject go = NewUI(name, parent);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = new Vector2(0f, height);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            if (textFont != null) label.font = textFont;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.MidlineLeft;
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
#endif
    }
}
