using DG.Tweening;
using UnityEngine;

namespace TeamOdd.UI
{
    // 켜질 때 투명도가 0에서 차오르며 살짝 작았던 크기가 제자리로 돌아온다. 창·팝업이 툭 나타나지 않게 한다.
    // 투명도는 이 오브젝트의 CanvasGroup으로 조절하며, 없으면 만든다. 시간 정지 중에도 열리도록 실제 시간 기준이다.
    [DisallowMultipleComponent]
    public sealed class UIPopIn : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float duration = 0.18f;
        [SerializeField, Range(0.8f, 1f)] private float startScale = 0.985f;
        [Tooltip("아래에서 올라오는 거리(캔버스 단위), 0이면 제자리")]
        [SerializeField] private float rise = 0f;

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 basePosition;
        private Sequence sequence;

        private void Awake()
        {
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
            if (TryGetComponent(out group) == false) group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            sequence?.Kill();
            group.alpha = 0f;
            rect.localScale = Vector3.one * startScale;
            rect.anchoredPosition = basePosition - new Vector2(0f, rise);

            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
                .Join(group.DOFade(1f, duration).SetEase(Ease.OutQuad))
                .Join(rect.DOScale(1f, duration).SetEase(Ease.OutCubic))
                .Join(rect.DOAnchorPos(basePosition, duration).SetEase(Ease.OutCubic));
        }

        private void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
            group.alpha = 1f;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = basePosition;
        }
    }
}
