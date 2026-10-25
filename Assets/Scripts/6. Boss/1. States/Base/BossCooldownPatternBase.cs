using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Boss
{
    // 내부쿨이 있는 보스 패턴의 베이스, 쿨이 없는 패턴은 BossStateBase를 직접 상속
    // Idle 같은 분기 상태는 CanUse로 이 패턴을 고를 수 있는지 판단

    // "고를 수 있는가"에 해당하는 조건(보스/플레이어 체력, 이 거리 밖이면 쓰지 않음 등)은
    // CanUse를 override하고 base.CanUse와 AND로 묶는다. 쿨 판정이 빠지지 않도록 base.CanUse를 반드시 포함
    // "고른 뒤 어떻게 실행할까"에 해당하는 조건(멀면 추격 후 공격 등)은 CanUse가 아니라 OnEnter에서 처리
    // 여러 패턴 사이의 우선순위나 선택 규칙은 패턴이 아니라 Idle에서 결정
    public abstract class BossCooldownPatternBase : BossStateBase
    {
        [Title("Cooldown")]
        [Tooltip("StartCooldown 호출 후 다시 사용할 수 있을 때까지의 시간(초)")]
        [SerializeField, Min(0f)] private float cooldown;

        // 처음에는 바로 사용 가능하도록 무한히 과거로 둠
        private float cooldownStartTime = float.NegativeInfinity;

        public float RemainingCooldown => Mathf.Max(0f, cooldownStartTime + cooldown - Clock.LocalTime);

        // 쿨 조건만 판단, 패턴 선택에는 IsReady 대신 CanUse 사용
        public bool IsReady => RemainingCooldown <= 0f;

        // 이 패턴을 지금 고를 수 있는지 판단, 쿨 외의 조건이 있는 패턴은 오버라이드
        public virtual bool CanUse => IsReady;

        // 패턴이 실제로 사용된 시점에 각 패턴에서 호출
        protected void StartCooldown() => cooldownStartTime = Clock.LocalTime;
    }
}
