using System.Collections;
using Jay;
using UnityEngine;

namespace TeamOdd.TimeControl
{
    /// <summary>
    /// 정해진 시간 동안만 시간을 멈추고 알아서 푸는 역할을 하는 매니저 클래스
    /// 스킬은 MonoBehaviour가 아니라 코루틴을 돌릴 수 없으므로 여기서 대신 기다림
    /// 정지 시간은 Time.time 기준(실제 게임 시간)이고, 멈춘 오브젝트의 LocalTime과는 관계 없음
    /// 전체 정지의 시작/끝을 알고 싶으면 TimeFreezable.WorldFrozenChanged를 구독
    /// </summary>
    public sealed class TimeFreezeManager : SingletonBase<TimeFreezeManager>
    {
        // 플레이어를 제외한 모든 TimeFreezable을 duration초 동안 멈춤. timeScale을 0보다 크게 주면 멈추지 않고 그 배율로 느리게
        public void FreezeWorld(float duration, float timeScale = 0f)
        {
            StartCoroutine(FreezeWorldRoutine(duration, timeScale));
        }

        // target 하나만 duration초 동안 멈춤
        public void Freeze(TimeFreezable target, float duration)
        {
            if (target == null) return;
            StartCoroutine(FreezeRoutine(target, duration));
        }

        private IEnumerator FreezeWorldRoutine(float duration, float timeScale)
        {
            TimeFreezable.FreezeWorld(timeScale);
            yield return new WaitForSeconds(duration);
            TimeFreezable.UnfreezeWorld(timeScale);
        }

        private IEnumerator FreezeRoutine(TimeFreezable target, float duration)
        {
            target.Freeze();
            yield return new WaitForSeconds(duration);

            // 기다리는 사이 파괴됐으면 풀 것도 없음
            if (target != null) target.Unfreeze();
        }
    }
}
