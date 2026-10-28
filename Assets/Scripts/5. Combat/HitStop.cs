using System.Collections;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 월드 전체를 잠깐 멈추는 히트스탑. 연속 요청은 남은 시간과 긴 쪽으로 합치고, 종료 직후에는 무시한다.
    public class HitStop : MonoBehaviour
    {
        private const float StopTimeScale = 0f; // 0이면 완전 정지
        private const float RequestCooldown = 0.1f; // 종료 후 새 요청을 무시하는 시간(초)

        private static HitStop instance;

        private Coroutine routine;
        private float savedTimeScale = 1f;
        private float stopEndTime;
        private float nextRequestTime;

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

            savedTimeScale = Time.timeScale;
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
            Time.timeScale = savedTimeScale;
            nextRequestTime = Time.unscaledTime + RequestCooldown;
        }

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            Restore();
        }
    }
}
