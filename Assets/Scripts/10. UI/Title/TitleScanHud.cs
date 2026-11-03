using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace TeamOdd
{
    // 메인 메뉴 오른쪽 홀로그램 보스를 표적처럼 두르는 최소한의 HUD
    // - 돌아가는 보스(T-포즈 팔 끝, 발밑 링까지)가 다 들어가는 화면 영역에 모서리 괄호만 두르고, 강한 박동마다 살짝 조였다 풀림
    // - 홀로그램이 지직거리는 동안 괄호도 끊기듯 옆으로 튐, 괄호 위에 대상 이름 한 줄 (플레이어가 비치는 동안엔 다른 이름)
    // - 홀로그램(TitleHologramPulse.Visibility)과 함께 나타나고 글자는 한 글자씩 찍힘, 메뉴라 실제 시간으로 진행
    public class TitleScanHud : MonoBehaviour
    {
        [SerializeField] private TitleHologramPulse pulse;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private CanvasGroup group;
        [SerializeField, Range(0f, 1f)] private float alpha = 0.5f;

        [Title("Frame")]
        [SerializeField] private RectTransform frame; // 모서리 괄호를 담은 영역, 보스 화면 영역에 맞춰 크기가 바뀜
        [SerializeField] private float framePadding = 28f; // 보스 영역 바깥으로 띄우는 거리(px)
        [SerializeField] private float screenMargin = 48f; // 화면 가장자리와 띄우는 최소 거리(px)
        [SerializeField] private float lockInset = 10f; // 강한 박동 때 괄호가 안쪽으로 조이는 거리(px)
        [SerializeField, Min(0.01f)] private float lockDuration = 0.7f;
        [SerializeField] private float glitchJitter = 5f; // 홀로그램이 지직거리는 동안 괄호가 옆으로 튀는 거리(px)
        [SerializeField, Min(0.01f)] private float glitchStep = 0.05f;

        [Title("Label")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string swapText = "SUBJECT 00  //  OPERATOR"; // 글리치 중 플레이어가 비치는 동안의 이름
        [SerializeField, Min(1f)] private float typeSpeed = 30f; // 초당 찍히는 글자 수

        private RectTransform screen;
        private int boundVersion = -1;
        private Vector2 frameCenter;
        private Vector2 frameSize;
        private float lockTime = -1f;
        private float jitter;
        private float nextJitter;
        private float typed;
        private string labelText;
        private bool showingSwap;

        private void Awake()
        {
            screen = (RectTransform)transform;
            if (label != null) labelText = label.text;
            if (group != null) group.alpha = 0f;
        }

        private void OnEnable()
        {
            if (pulse != null) pulse.Peaked += Lock;
        }

        private void OnDisable()
        {
            if (pulse != null) pulse.Peaked -= Lock;
        }

        private void Lock() => lockTime = 0f;

        private void Update()
        {
            float visibility = pulse != null ? pulse.Visibility : 0f;
            if (group != null) group.alpha = alpha * visibility;
            if (pulse == null || pulse.IsBound == false || viewCamera == null || frame == null) return;

            if (boundVersion != pulse.BindVersion)
            {
                boundVersion = pulse.BindVersion;
                Measure(pulse.HologramBounds);
                typed = 0f;
            }

            float deltaTime = Time.unscaledDeltaTime;
            UpdateFrame(deltaTime);

            if (label != null)
            {
                if (visibility > 0.3f) typed += typeSpeed * deltaTime;
                label.maxVisibleCharacters = Mathf.FloorToInt(typed);
                if (pulse.IsSwapped != showingSwap)
                {
                    showingSwap = pulse.IsSwapped;
                    label.text = showingSwap ? swapText : labelText;
                }
            }
        }

        // 몸 가운데 세로축을 중심으로 팔 끝 반지름의 원기둥(발밑~머리)을 화면에 비춰 그 사각 영역을 괄호 자리로 씀, 돌아도 괄호는 그대로
        private void Measure(Bounds bounds)
        {
            var axis = new Vector3(bounds.center.x, 0f, bounds.center.z);
            float radius = new Vector2(bounds.size.x, bounds.size.z).magnitude * 0.5f;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int index = 0; index < 16; index++)
            {
                float angle = index * Mathf.PI * 2f / 16f;
                Vector3 rim = axis + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                foreach (float height in new[] { bounds.min.y, bounds.max.y })
                {
                    Vector2 point = ToScreen(rim + Vector3.up * height);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            Vector2 limit = screen.rect.size * 0.5f - Vector2.one * screenMargin;
            min = Vector2.Max(min - Vector2.one * framePadding, -limit);
            max = Vector2.Min(max + Vector2.one * framePadding, limit);
            frameCenter = (min + max) * 0.5f;
            frameSize = max - min;
        }

        private Vector2 ToScreen(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(screen, viewCamera.WorldToScreenPoint(world), null, out Vector2 local);
            return local;
        }

        // 박동 순간 확 조였다가 천천히 풀림, 지직거리는 동안엔 끊기듯 옆으로 튐
        private void UpdateFrame(float deltaTime)
        {
            float inset = 0f;
            if (lockTime >= 0f)
            {
                lockTime += deltaTime;
                float progress = Mathf.Clamp01(lockTime / lockDuration);
                inset = lockInset * Mathf.Pow(1f - progress, 3f);
                if (progress >= 1f) lockTime = -1f;
            }

            if (pulse.IsGlitching)
            {
                nextJitter -= deltaTime;
                if (nextJitter <= 0f)
                {
                    nextJitter = glitchStep;
                    jitter = Random.value < 0.5f ? Random.Range(-1f, 1f) * glitchJitter : 0f;
                }
            }
            else
            {
                jitter = 0f;
            }

            frame.anchoredPosition = frameCenter + Vector2.right * jitter;
            frame.sizeDelta = frameSize - Vector2.one * (inset * 2f);
        }
    }
}
