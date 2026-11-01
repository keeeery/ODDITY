namespace TeamOdd.Combat
{
    // CombatEntity가 피해를 체력에 적용하기 전에 물어보는 쪽. 플레이어는 Player가 구르기 무적과 패시브 칩(조건 반사, 두 번째 심장)을 묶어 답한다
    public interface IDamageGuard
    {
        // true면 이번 피해는 없던 일이 된다 (회피)
        bool BlockDamage(DamageEvent damageEvent);

        // 이번 피해로 죽게 될 때 부른다. true면 죽지 않는다 (부활). 남길 체력은 Guard가 직접 맞춘다
        bool PreventDeath();
    }
}
