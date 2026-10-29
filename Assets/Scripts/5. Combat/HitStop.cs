using System.Collections;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 월드 전체를 잠깐 멈추는 히트스탑. 연속 요청은 남은 시간과 긴 쪽으로 합치고, 종료 직후에는 무시한다.
    // 멈춤이 끝나면 BaseTimeScale로 돌아가므로, 슬로모션처럼 평소 시간 배율을 바꾸는 쪽은 Time.timeScale 대신 BaseTimeScale을 바꾼다.
    public class HitStop : MonoBehaviour
    {
        private const float StopTimeScale = 0f; // 0이면 완전 정지
        private const float RequestCooldown = 0.1f; // 종료 후 새 요청을 무시하는 시간(초)

        private static HitStop instance;
        private static float baseTimeScale = 1f;

        private Coroutine routine;
        private float stopEndTime;
        private float nextRequestTime;

        public static bool IsStopping => instance != null && instance.routine != null;

        // 멈춰 있지 않을 때의 시간 배율, 멈춘 중에 바꾸면 멈춤이 끝난 뒤에 적용된다.
        public static float BaseTimeScale
        {
            get => baseTimeScale;
            set
            {
                baseTimeScale = Mathf.Max(0f, value);
                if (IsStopping == false) Time.timeScale = baseTimeScale;
            }
        }

        public static void Request(float duration)
        {
            if (duration <= 0f) return;

            if (instance == null)
            {
                GameObject go = new GameObject("HitStop") { hideFlags = HideFlags.HideAndDontSave };
                instance = go.AddComponent<HitStop>();
            }
            instance.Begin(duration);
        }

        private void Begin(float duration)
        {
            float now = Time.unscaledTime;
            if (routine != null)
            {
                stopEndTime = Mathf.Max(stopEndTime, now + duration);
                return;
            }

            if (now < nextRequestTime) return;

            stopEndTime = now + duration;
            Time.timeScale = StopTimeScale;
            routine = StartCoroutine(Release());
        }

        private IEnumerator Release()
        {
            while (Time.unscaledTime < stopEndTime) yield return null;
            Restore();
        }

        private void Restore()
        {
            if (routine == null) return;
            routine = null;
            Time.timeScale = baseTimeScale;
            nextRequestTime = Time.unscaledTime + RequestCooldown;
        }

        // 도메인 리로드 없이 플레이할 때를 위한 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => baseTimeScale = 1f;

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            Restore();
        }
    }
}
