using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // UI를 잠깐 지직거리게 하는 연출: 대상 위치가 튀고, 깜빡이고, 지정한 그래픽은 붉은색·청록색 복사본으로 색이 갈라진다.
    // 값은 stepInterval마다 한 번씩만 바꿔 디지털 신호처럼 끊겨 보이게 한다. 실제 시간 기준이다.
    // 길이와 세기는 부르는 쪽이 정한다(보상: 짧고 약하게, 사망: 길고 강하게).
    [DisallowMultipleComponent]
    public sealed class UIGlitch : MonoBehaviour
    {
        [Tooltip("위치가 튀는 대상, 비워 두면 이 오브젝트")]
        [SerializeField] private RectTransform[] targets;
        [Tooltip("깜빡임을 줄 그룹, 비워 두면 이 오브젝트의 CanvasGroup(없으면 만듦)")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("색 분리 복사본을 띄울 그래픽(제목 글자 등), 레이아웃 그룹 밖에 있어야 한다.")]
        [SerializeField] private Graphic[] splitGraphics;

        [Header("세기 1 기준")]
        [SerializeField, Min(0f)] private float maxOffset = 14f;
        [SerializeField, Min(0f)] private float splitOffset = 4f;
        [Tooltip("깜빡일 때 내려가는 투명도")]
        [SerializeField, Range(0f, 1f)] private float flickerAlpha = 0.25f;
        [SerializeField, Min(0.01f)] private float stepInterval = 0.04f;
        [SerializeField] private Color splitColorA = new(1f, 0.2f, 0.25f, 0.6f);
        [SerializeField] private Color splitColorB = new(0.2f, 0.85f, 1f, 0.6f);

        private Vector2[] basePositions;
        private Graphic[] ghostsA;
        private Graphic[] ghostsB;
        private Tween tween;
        private float baseAlpha = -1f;
        private float nextStep;

        // fadeOut이면 지직거리며 점점 사라져 끝나면 투명하게 남는다(Stop으로 되돌림).
        public Tween Play(float duration, float strength, bool fadeOut = false)
        {
            Stop();
            Prepare();

            for (int i = 0; i < targets.Length; i++)
                basePositions[i] = targets[i].anchoredPosition;
            for (int i = 0; i < splitGraphics.Length; i++)
            {
                if (splitGraphics[i] is TMP_Text text)
                {
                    ((TMP_Text)ghostsA[i]).text = text.text;
                    ((TMP_Text)ghostsB[i]).text = text.text;
                }
            }

            nextStep = 0f;
            tween = DOVirtual.Float(0f, 1f, duration, progress => Step(progress, strength, fadeOut))
                .SetEase(Ease.Linear).SetUpdate(true).SetTarget(this)
                .OnComplete(() =>
                {
                    Restore();
                    if (fadeOut) group.alpha = 0f;
                });
            return tween;
        }

        // 지직거림을 멈추고 위치·투명도를 원래대로 되돌린다.
        public void Stop()
        {
            tween?.Kill();
            tween = null;
            Restore();
        }

        private void OnDisable() => Stop();

        private void Prepare()
        {
            if (targets == null || targets.Length == 0) targets = new[] { (RectTransform)transform };
            if (group == null && TryGetComponent(out group) == false) group = gameObject.AddComponent<CanvasGroup>();
            if (baseAlpha < 0f) baseAlpha = group.alpha;
            basePositions ??= new Vector2[targets.Length];
            splitGraphics ??= new Graphic[0];
            if (ghostsA != null) return;

            ghostsA = new Graphic[splitGraphics.Length];
            ghostsB = new Graphic[splitGraphics.Length];
            for (int i = 0; i < splitGraphics.Length; i++)
            {
                ghostsA[i] = CreateGhost(splitGraphics[i], splitColorA);
                ghostsB[i] = CreateGhost(splitGraphics[i], splitColorB);
            }
        }

        private void Step(float progress, float strength, bool fadeOut)
        {
            if (Time.unscaledTime < nextStep) return;
            nextStep = Time.unscaledTime + stepInterval;

            // 매 단계 크게 튀거나 제자리에 있거나 둘 중 하나여서 끊겨 보인다.
            bool burst = Random.value < 0.65f;
            for (int i = 0; i < targets.Length; i++)
            {
                Vector2 jump = burst
                    ? new Vector2(Random.Range(-1f, 1f), Random.Range(-0.15f, 0.15f)) * (maxOffset * strength)
                    : Vector2.zero;
                targets[i].anchoredPosition = basePositions[i] + jump;
            }

            float alpha = fadeOut ? baseAlpha * (1f - progress) : baseAlpha;
            if (Random.value < 0.3f + 0.3f * strength) alpha *= Mathf.Lerp(1f, flickerAlpha, strength);
            group.alpha = alpha;

            for (int i = 0; i < splitGraphics.Length; i++)
            {
                float split = splitOffset * strength * Random.Range(0.5f, 1.5f);
                PlaceGhost(ghostsA[i], splitGraphics[i], burst, new Vector2(split, 0f));
                PlaceGhost(ghostsB[i], splitGraphics[i], burst, new Vector2(-split, 0f));
            }
        }

        private void Restore()
        {
            if (basePositions == null) return;

            for (int i = 0; i < targets.Length; i++)
                targets[i].anchoredPosition = basePositions[i];
            group.alpha = baseAlpha;
            for (int i = 0; i < ghostsA.Length; i++)
            {
                ghostsA[i].gameObject.SetActive(false);
                ghostsB[i].gameObject.SetActive(false);
            }
        }

        private static void PlaceGhost(Graphic ghost, Graphic source, bool visible, Vector2 offset)
        {
            ghost.gameObject.SetActive(visible);
            ghost.rectTransform.anchoredPosition = source.rectTransform.anchoredPosition + offset;
        }

        // 원본 바로 뒤(먼저 그려지는 쪽)에 그래픽만 남긴 복사본을 만든다.
        private static Graphic CreateGhost(Graphic source, Color tint)
        {
            GameObject copy = Instantiate(source.gameObject, source.transform.parent, false);
            copy.name = $"{source.name} (Glitch)";
            foreach (Transform child in copy.transform) Destroy(child.gameObject);
            foreach (MonoBehaviour behaviour in copy.GetComponents<MonoBehaviour>())
                if (behaviour is not Graphic) DestroyImmediate(behaviour);
            copy.AddComponent<LayoutElement>().ignoreLayout = true;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex());

            Graphic ghost = copy.GetComponent<Graphic>();
            ghost.raycastTarget = false;
            ghost.color = tint;
            copy.SetActive(false);
            return ghost;
        }
    }
}
