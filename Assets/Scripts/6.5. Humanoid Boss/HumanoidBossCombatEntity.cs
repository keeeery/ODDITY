using System;
using TeamOdd.Combat;

namespace TeamOdd
{
    // 휴머노이드 보스의 피격 대상, 받은 피해로 체력이 바뀌면 알림 (페이즈 전환·체력 조건 패턴이 구독)
    // 몬스터 레지스트리 등록은 SampleCombatMonster와 같음
    public class HumanoidBossCombatEntity : CombatEntity
    {
        // (현재 체력, 최대 체력)
        public event Action<int, int> OnHpChanged;

        public float HpRatio => CurrentStats.MaxHp > 0 ? (float)Hp / CurrentStats.MaxHp : 0f;

        private void OnEnable() => MonsterRegistry.RegisterMonster(Collider, this);

        private void OnDisable() => MonsterRegistry.UnregisterMonster(Collider);

        public override int TakeDamage(DamageEvent @event)
        {
            int damage = base.TakeDamage(@event);
            if (damage > 0) OnHpChanged?.Invoke(Hp, CurrentStats.MaxHp);

            return damage;
        }
    }
}
