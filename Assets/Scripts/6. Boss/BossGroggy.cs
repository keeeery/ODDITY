using System;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 무력화 수치: 가득 찬 채로 시작해 받은 피해의 무력화 피해(DamageInfo.Groggy)만큼 깎이고, 0이 되면 무너진다(Broken).
    // 한동안 맞지 않으면 서서히 다시 차오르고, 무너진 동안에는 더 깎이지 않는다. 무너짐에서 회복할 때 쓰는 쪽(그로기 상태)이 Restore로 다시 채운다.
    // 무너졌을 때 무엇을 할지는 구독하는 쪽이 정하므로 이 컴포넌트는 상태머신을 모른다.
    // 회복 대기·차오름은 보스 시간(TimeFreezable)을 따라 시간 정지 중에는 멈춘다.
    [DisallowMultipleComponent]
    public sealed class BossGroggy : MonoBehaviour
    {
        [Title("Gauge")]
        [SerializeField, Min(1)] private int maxGroggy = 100;
        [Tooltip("마지막으로 깎인 뒤 이 시간(초) 동안 맞지 않으면 다시 차오르기 시작한다.")]
        [SerializeField, Min(0f)] private float regenDelay = 5f;
        [Tooltip("다시 차오르는 속도(초당 수치), 0이면 차오르지 않는다.")]
        [SerializeField, Min(0f)] private float regenRate = 8f;

        [Title("States")]
        [ShowInInspector, ReadOnly] public float Current { get; private set; }
        [ShowInInspector, ReadOnly] public bool IsBroken { get; private set; }

        private ICombatable self;
        private TimeFreezable clock;
        private CombatManager combatManager;
        private float lastHitTime = float.NegativeInfinity;

        public int Max => maxGroggy;
        public float Ratio => Current / maxGroggy;

        // 수치가 0이 된 순간 한 번
        public event Action Broken;

        private void Awake()
        {
            self = GetComponent<ICombatable>();
            Current = maxGroggy;
            if (self == null) Debug.LogWarning($"[{nameof(BossGroggy)}] {name}에 ICombatable이 없어 무력화 피해를 받지 않습니다.", this);
        }

        private void Start()
        {
            BossMonster boss = GetComponent<BossMonster>();
            clock = boss != null ? boss.Clock : TimeFreezable.GetOrAdd(gameObject);

            combatManager = CombatManager.Instance;
            combatManager.OnDamageEventExecuted += HandleDamage;
        }

        private void OnDestroy()
        {
            if (combatManager != null) combatManager.OnDamageEventExecuted -= HandleDamage;
        }

        private void Update()
        {
            if (IsBroken || regenRate <= 0f || Current >= maxGroggy) return;
            if (clock.LocalTime - lastHitTime < regenDelay) return;

            Current = Mathf.Min(maxGroggy, Current + regenRate * clock.LocalDeltaTime);
        }

        #region PUBLIC_METHODS

        // 무너짐에서 회복하며 가득 채움
        public void Restore()
        {
            IsBroken = false;
            Current = maxGroggy;
        }

        #endregion

        // 실제로 피해가 들어간 경우만 깎음, 죽어서 피해가 0이면 무너지지 않음
        private void HandleDamage(DamageEvent damageEvent, int receivedDamage)
        {
            if (ReferenceEquals(damageEvent.Receiver, self) == false) return;
            if (IsBroken || receivedDamage <= 0 || damageEvent.Info.Groggy <= 0) return;

            Current = Mathf.Max(0f, Current - damageEvent.Info.Groggy);
            lastHitTime = clock.LocalTime;

            if (Current > 0f) return;

            IsBroken = true;
            Broken?.Invoke();
        }
    }
}
