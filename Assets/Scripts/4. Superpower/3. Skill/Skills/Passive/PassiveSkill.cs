namespace TeamOdd.Superpower
{
    /// <summary>
    /// 키 대신 트리거(전투 중에 일어난 일)로 발동하는 스킬. 스킬 슬롯은 똑같이 차지하지만 숫자 키로는 쓰지 않는다.
    /// PassiveSkillRunner가 트리거가 일어날 때마다 같은 트리거의 패시브에 TryUse를 부른다. (SkillCaster가 키를 누를 때 부르는 것과 같은 자리)
    /// 쿨타임은 내부 쿨타임으로 쓴다. 한 번 발동하면 쿨타임 동안은 트리거가 와도 발동하지 않는다.
    /// TryUse가 true면 발동했다는 뜻이라, 피해를 받기 전 트리거에서는 그 피해(또는 죽음)를 막았다는 뜻이 된다.
    /// </summary>
    public abstract class PassiveSkill : SuperpowerSkill
    {
        public abstract PassiveTrigger Trigger { get; }

        protected PassiveSkill(string name, float cooldown) : base(name, cooldown) { }

        // 아레나에서 쌓은 효과를 지운다. 마을과 아레나 사이를 오갈 때 인벤토리가 부른다. 쌓는 효과가 있는 패시브만 오버라이드한다
        public virtual void ResetStacks() { }
    }
}
