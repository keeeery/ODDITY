using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 들고 있는 염동력 오브젝트, 없으면 조준 중인 오브젝트의 크기와 질량에 (Multiplier)를 곱한다. 매개변수 1개: Multiplier(float)
    // - 대상이 없으면 쓰지 않고 쿨타임도 돌지 않는다
    // - 커진 질량이 들 수 있는 최대 질량을 넘으면 PsychokinesisController가 다음 프레임에 놓는다
    public class ResizeSkill : SuperpowerSkill
    {
        private readonly float multiplier;

        private PsychokinesisObject target; // CanExecute에서 고른 대상, Execute에서 씀

        public ResizeSkill(string name, float cooldown, float multiplier) : base(name, cooldown)
        {
            this.multiplier = multiplier;
        }

        public override string Describe() => $"크기 · 무게 ×{multiplier}";

        protected override bool CanExecute(SkillContext context)
        {
            target = null;
            if (context.Caster.TryGetComponent(out PsychokinesisController controller))
                target = controller.HeldObject != null ? controller.HeldObject : controller.FocusedObject;

            if (target != null) return true;

            Debug.Log($"[{Name}] 들고 있거나 조준한 염동력 오브젝트가 없습니다.");
            return false;
        }

        protected override void Execute(SkillContext context)
        {
            target.Resize(multiplier);
            target = null;
        }
    }
}
