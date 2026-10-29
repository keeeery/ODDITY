using System.Collections;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 월드 전체를 잠깐 멈추는 히트스탑. 새로 요청하면 진행 중인 정지를 끊고 새 시간으로 대체한다.
    public class HitStop : MonoBehaviour
    {
        private static HitStop instance;

        private Coroutine routine;
        private float savedTimeScale = 1f;

        public static void Request(float duration)
        {
            if (duration <= 0f) return;

            if (instance == null)
            {
                var go = new GameObject("HitStop") { hideFlags = HideFlags.HideAndDontSave };
                instance = go.AddComponent<HitStop>();
            }
            instance.Begin(duration);
        }

        private void Begin(float duration)
        {
            if (routine != null) StopCoroutine(routine);
            else savedTimeScale = Time.timeScale; // 정지 중이 아닐 때만 원래 값을 저장

            Time.timeScale = 0f;
            routine = StartCoroutine(Release(duration));
        }

        private IEnumerator Release(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            Restore();
        }

        private void Restore()
        {
            if (routine == null) return;
            routine = null;
            Time.timeScale = savedTimeScale;
        }

        private void OnDisable() => Restore();
    }
}
