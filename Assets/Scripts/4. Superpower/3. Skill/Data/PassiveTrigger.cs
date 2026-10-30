namespace TeamOdd.Superpower
{
    // 패시브 스킬이 발동하는 때. PassiveSkillRunner가 이 값으로 어떤 패시브를 부를지 고른다
    public enum PassiveTrigger
    {
        DealtDamage,      // 적에게 피해를 준 뒤
        TookDamage,       // 피해를 받은 뒤
        BeforeTakeDamage, // 피해를 받기 직전. 발동하면 그 피해는 없던 일이 된다
        BeforeDeath,      // 죽을 피해를 받기 직전. 발동하면 죽지 않는다
    }
}
