using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace TeamOdd.UI
{
    // 켜져 있는 자식들을 interval 간격으로 하나씩 켠다. 켜지는 순간 짧게 깜빡이고,
    // VectorPanel이면 스킬 슬롯의 사용 가능 번쩍임처럼 흰빛이 빠르게 빠진다. 실제 시간 기준이다.
    // 투명도는 자식의 CanvasGroup으로 조절하며, 없으면 만들어 둔다.
    [DisallowMultipleComponent]
    public sealed class UIStaggerReveal : MonoBehaviour
    {
        private const float BlinkAlpha = 0.15f;

        [SerializeField, Min(0f)] private float interval = 0.1f;
        [Tooltip("번쩍임이 빠지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float flashDuration = 0.45f;
        [Tooltip("켜지는 순간 꺼졌다 다시 켜지는 깜빡임 길이(초), 0이면 바로 켜짐")]
        [SerializeField, Min(0f)] private float blinkDuration = 0.03f;

        private readonly List<CanvasGroup> groups = new();
        private readonly List<VectorPanel> panels = new();
        private Sequence sequence;

        public Sequence Play()
        {
            HideAll();

            sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            for (int i = 0; i < groups.Count; i++)
            {
                CanvasGroup group = groups[i];
                VectorPanel panel = panels[i];
                float at = i * interval;

                sequence.InsertCallback(at, () => SetVisible(group, panel, 1f));
                if (blinkDuration > 0f)
                {
                    sequence.InsertCallback(at + blinkDuration, () => group.alpha = BlinkAlpha);
                    sequence.InsertCallback(at + blinkDuration * 2f, () => group.alpha = 1f);
                }
                if (panel != null)
                    sequence.Insert(at, DOVirtual.Float(1f, 0f, flashDuration, value => panel.FlashAlpha = value).SetEase(Ease.Linear));
            }
            return sequence;
        }

        // 켜져 있는 자식을 모두 숨기고 다음 Play에 쓸 목록을 다시 모은다.
        public void HideAll()
        {
            sequence?.Kill();
            sequence = null;
            Collect();
            for (int i = 0; i < groups.Count; i++)
                SetVisible(groups[i], panels[i], 0f);
        }

        // 연출을 멈추고 모두 보이게 한다.
        public void ShowAll()
        {
            sequence?.Kill();
            sequence = null;
            Collect();
            for (int i = 0; i < groups.Count; i++)
            {
                SetVisible(groups[i], panels[i], 1f);
                if (panels[i] != null) panels[i].FlashAlpha = 0f;
            }
        }

        private void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
        }

        private void Collect()
        {
            groups.Clear();
            panels.Clear();
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf == false) continue;
                if (child.TryGetComponent(out CanvasGroup group) == false) group = child.gameObject.AddComponent<CanvasGroup>();
                child.TryGetComponent(out VectorPanel panel);
                groups.Add(group);
                panels.Add(panel);
            }
        }

        // 숨긴 동안에는 마우스를 받지 않아 보이지 않는 슬롯에 툴팁이 뜨지 않는다.
        private static void SetVisible(CanvasGroup group, VectorPanel panel, float alpha)
        {
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0f;
            if (panel != null && alpha > 0f) panel.FlashAlpha = 1f;
        }
    }
}
