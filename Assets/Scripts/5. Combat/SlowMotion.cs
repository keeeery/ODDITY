using System.Collections;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 월드 전체를 잠깐 느리게 하는 슬로모션(마무리 일격 등). 곧바로 scale까지 떨어져 hold 동안 머문 뒤 recover 동안 원래 속도로 돌아온다.
    // 시간은 실제 시간으로 재고, 배율은 HitStop.BaseTimeScale로 바꿔 히트스탑과 겹쳐도 서로 덮어쓰지 않는다.
    // 물리가 끊겨 보이지 않도록 fixedDeltaTime도 배율에 맞춰 줄였다가 되돌린다. 새 요청은 진행 중인 슬로모션을 대신한다.
    public class SlowMotion : MonoBehaviour
    {
        private static SlowMotion instance;
        private static float defaultFixedDeltaTime = -1f;

        private Coroutine routine;

        public static void Play(float scale, float hold, float recover)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SlowMotion") { hideFlags = HideFlags.HideAndDontSave };
                instance = go.AddComponent<SlowMotion>();
            }

            if (defaultFixedDeltaTime < 0f) defaultFixedDeltaTime = Time.fixedDeltaTime;
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = instance.StartCoroutine(instance.Run(Mathf.Clamp(scale, 0.01f, 1f), hold, recover));
        }

        private IEnumerator Run(float scale, float hold, float recover)
        {
            Apply(scale);

            float endTime = Time.unscaledTime + hold;
            while (Time.unscaledTime < endTime) yield return null;

            // 느린 쪽에 오래 머물다 끝에서 빠르게 풀리도록 ease-in
            float startTime = Time.unscaledTime;
            while (recover > 0f && Time.unscaledTime < startTime + recover)
            {
                float progress = (Time.unscaledTime - startTime) / recover;
                Apply(Mathf.Lerp(scale, 1f, progress * progress));
                yield return null;
            }

            Restore();
        }

        private static void Apply(float scale)
        {
            HitStop.BaseTimeScale = scale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * scale;
        }

        private void Restore()
        {
            if (routine == null) return;
            routine = null;
            Apply(1f);
        }

        // 도메인 리로드 없이 플레이할 때를 위한 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => defaultFixedDeltaTime = -1f;

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            Restore();
        }
    }
}
