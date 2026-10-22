using UnityEngine;

namespace TeamOdd.Boss
{
    // 테스트용: 보스의 내부쿨 패턴별 남은 쿨을 1초마다 로그로 출력
    public sealed class BossPatternCooldownLogger : MonoBehaviour
    {
        private const float LogInterval = 1f;

        [SerializeField] private BossMonster boss;

        private BossCooldownPatternBase[] patterns;

        private void Start()
        {
            patterns = boss.GetComponentsInChildren<BossCooldownPatternBase>(true);
            InvokeRepeating(nameof(LogCooldowns), 0f, LogInterval);
        }

        private void LogCooldowns()
        {
            foreach (BossCooldownPatternBase pattern in patterns)
            {
                string state = pattern.IsReady ? "사용 가능" : $"{pattern.RemainingCooldown:F1}초 남음";
                Debug.Log($"[Cooldown] {pattern.GetType().Name}: {state}");
            }
        }
    }
}
