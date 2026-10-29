using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 스킬을 쓰는 순간의 정보. 어떤 스킬이든 같은 모양으로 받는다
    public readonly struct SkillContext
    {
        public Transform Caster { get; }     // 스킬을 쓴 플레이어
        public CombatEntity Owner { get; }   // 플레이어의 전투 정보 (체력, 스탯)
        public Vector3 AimDirection { get; } // 카메라가 보는 방향 (수평)

        public SkillContext(Transform caster, CombatEntity owner, Vector3 aimDirection)
        {
            Caster = caster;
            Owner = owner;
            AimDirection = aimDirection;
        }
    }

    /// <summary>
    /// 스킬 칩 하나가 가진 스킬. (전략 패턴의 전략. 99. Study A_Coupon의 ICoupon 자리)
    /// SkillCaster는 슬롯에 어떤 스킬이 들어 있는지 모르고 TryUse만 부른다. 무엇을 할지는 자식 클래스의 Execute가 정한다.
    /// 쿨타임은 모든 스킬에 공통이라 여기서 처리하고(TryUse), 스킬마다 다른 매개변수는 자식 클래스가 생성자로 받는다.
    /// 칩 한 장마다 하나씩 만들어 OwnedChip에 넣으므로 쿨타임은 슬롯이 아니라 칩을 따라다닌다.
    /// 쿨타임은 남은 시간을 Tick으로 줄여 간다. 누가 Tick을 부르느냐가 곧 언제 쿨타임이 도느냐다 (지금은 인벤토리가 장착된 칩만 부른다).
    /// </summary>
    public abstract class SuperpowerSkill
    {
        private float remainingCooldown; // 0이 되면 다시 쓸 수 있다

        public string Name { get; }
        public float Cooldown { get; }

        public bool IsReady => remainingCooldown <= 0f;
        public float RemainingCooldown => remainingCooldown;

        // 모든 스킬에 공통인 값. 자식 클래스는 자기 매개변수와 함께 이 둘을 받아 넘긴다
        protected SuperpowerSkill(string name, float cooldown)
        {
            Name = name;
            Cooldown = cooldown;
        }

        // 쿨타임이 남았거나 지금 쓸 수 없으면(대상이 없는 등) 쓰지 않고 false
        public bool TryUse(SkillContext context)
        {
            if (IsReady == false || CanExecute(context) == false)
                return false;

            remainingCooldown = Cooldown;
            Execute(context);
            return true;
        }

        // 쿨타임을 deltaTime만큼 흘린다. 매 프레임 부른다
        public void Tick(float deltaTime)
        {
            remainingCooldown = Mathf.Max(0f, remainingCooldown - deltaTime);
        }

        // 쿨타임을 바로 끝낸다. 정비를 마칠 때 인벤토리가 부른다
        public void ResetCooldown()
        {
            remainingCooldown = 0f;
        }

        // 발동 키를 뗐을 때 SkillCaster가 부른다. 누르고 있는 동안 계속되는 스킬(채널링)만 오버라이드한다
        public virtual void Release() { }

        // 이 스킬이 받은 매개변수를 사람이 읽을 수 있게. 패널 정보 텍스트에 쓴다
        public abstract string Describe();

        // 대상이 있어야 하는 스킬처럼 쓰기 전에 확인할 것이 있으면 오버라이드. false면 쿨타임도 돌지 않는다
        protected virtual bool CanExecute(SkillContext context) => true;

        protected abstract void Execute(SkillContext context);
    }
}
