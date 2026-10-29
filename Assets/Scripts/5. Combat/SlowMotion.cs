using System.Collections;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 월드 전체를 잠깐 느리게 하는 슬로모션(마무리 일격 등). 곧바로 scale까지 떨어져 hold 동안 머문 뒤 recover 동안 원래 속도로 돌아온다.
    // 시간은 실제 시간으로 재고, 배율은 HitStop.BaseTimeScale로 바꿔 히트스탑과 겹쳐도 서로 덮어쓰지 않는다.
    // 물리가 끊겨 보이지 않도록 fixedDeltaTime도 배율에 맞춰 줄였다가 되돌린다. 새 요청은 진행 중인 슬로모션을 대신한다.
    // playerScale이 1보다 작으면 월드가 돌아오는 동안 플레이어·카메라 회전(PlayerScale)은 같은 곡선으로 playerScale까지만 돌아와 남고,
    // Resume을 불러야 풀린다(마무리 일격 → 보상창). 같은 곡선을 따라 갈라지므로 둘의 차이가 서서히 벌어진다.
    public class SlowMotion : MonoBehaviour
    {
        private static SlowMotion instance;
        private static float defaultFixedDeltaTime = -1f;

        private Coroutine routine;

        // 지금 슬로모션 배율, 슬로모션 중이 아니면 1. 시간으로 움직이지 않는 것(마우스 시점 회전 등)을 함께 느리게 할 때 씀
        public static float Scale { get; private set; } = 1f;
        // 플레이어·카메라 회전이 따르는 배율, 월드와 같이 가다가 playerScale을 주면 그 배율에 남는다.
        public static float PlayerScale { get; private set; } = 1f;
        // 월드 시간(Time.deltaTime)에 곱해 플레이어 배율이 되게 하는 값, 플레이어 쪽 이동·애니메이션에 씀
        public static float PlayerRelativeScale => Scale > 0f ? Mathf.Clamp(PlayerScale / Scale, 0.01f, 1f) : 1f;
        // 월드나 플레이어 중 하나라도 아직 느린 동안
        public static bool IsSlowed => Scale < 1f || PlayerScale < 1f;

        public static void Play(float scale, float hold, float recover, float playerScale = 1f)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SlowMotion") { hideFlags = HideFlags.HideAndDontSave };
                instance = go.AddComponent<SlowMotion>();
            }

            if (defaultFixedDeltaTime < 0f) defaultFixedDeltaTime = Time.fixedDeltaTime;
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = instance.StartCoroutine(instance.Run(Mathf.Clamp(scale, 0.01f, 1f), hold, recover, Mathf.Clamp(playerScale, 0.01f, 1f)));
        }

        // 진행 중인 슬로모션을 끊고 월드와 플레이어 모두 곧바로 원래 속도로 되돌림
        public static void Resume()
        {
            if (instance == null) return;
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = null;
            Apply(1f);
            PlayerScale = 1f;
        }

        private IEnumerator Run(float scale, float hold, float recover, float playerScale)
        {
            Apply(scale);
            PlayerScale = scale;

            float endTime = Time.unscaledTime + hold;
            while (Time.unscaledTime < endTime) yield return null;

            // 느린 쪽에 오래 머물다 끝에서 빠르게 풀리도록 ease-in
            float startTime = Time.unscaledTime;
            while (recover > 0f && Time.unscaledTime < startTime + recover)
            {
                float progress = (Time.unscaledTime - startTime) / recover;
                Apply(Mathf.Lerp(scale, 1f, progress * progress));
                PlayerScale = Mathf.Lerp(scale, Mathf.Max(scale, playerScale), progress * progress);
                yield return null;
            }

            Restore();
            PlayerScale = Mathf.Max(scale, playerScale);
        }

        private static void Apply(float scale)
        {
            Scale = scale;
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
        private static void ResetStatics()
        {
            defaultFixedDeltaTime = -1f;
            Scale = 1f;
            PlayerScale = 1f;
        }

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            Restore();
            PlayerScale = 1f;
        }
    }
}
