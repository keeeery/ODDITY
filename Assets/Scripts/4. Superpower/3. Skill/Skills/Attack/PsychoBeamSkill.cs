using TeamOdd.Player;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 발동 키를 누르고 있는 동안 조준점 방향으로 염동력 레이저를 쏜다. (채널링 스킬)
    // 매개변수 4개: Duration(float), Range(float), DPS(int), TickInterval(float)
    // - 진행: 키를 누르면 시작하고, 키를 떼거나 Duration초가 지나거나 플레이어가 죽거나 구르면 끝난다
    //         증강칩 패널을 열거나 ESC로 커서를 풀어도 SkillCaster가 Release를 불러 끝난다
    //         쏘는 동안은 걷거나 달릴 수 없다(PlayerController.IsMoveBlocked). 구르기는 되고, 구르면 레이저가 끝난다
    //         구르는 중에는 쓸 수 없다 (눌러도 쿨타임과 스태미나가 나가지 않는다)
    // - 조준: 매 프레임 화면 중앙 조준점을 다시 구하므로 쏘는 중에 카메라를 돌리면 레이저가 따라간다
    // - 사거리: 플레이어 가슴 높이에서 Range m까지 뻗고, 그 전에 무언가(벽, 적)에 닿으면 거기서 막힌다
    // - 피해: 레이저 끝에 닿은 적 하나에게 TickInterval초마다 DPS × TickInterval 피해. 처음 닿은 순간 바로 한 번 준다
    // - 쿨타임과 스태미나(Cost)는 다른 스킬처럼 누르는 순간 한 번만 처리한다
    // 이 클래스는 값과 쿨타임을 들고 레이저 프리팹을 띄우고 멈추기만 한다. 쏘며 피해를 주는 일은 프리팹의 PsychoBeam이 한다
    public class PsychoBeamSkill : SuperpowerSkill
    {
        private readonly float duration;
        private readonly float range;
        private readonly int dps;
        private readonly float tickInterval;
        private readonly PsychoBeam prefab; // SkillChip.tsv의 PrefabKey로 불러온 프리팹

        // 지금 나가고 있는 레이저. 끝나면 스스로 사라져 null이 된다. 쏘는 동안 다시 눌러 두 줄이 나가지 않게 막는다
        private PsychoBeam beam;

        public PsychoBeamSkill(string name, float cooldown, float duration, float range, int dps, float tickInterval, PsychoBeam prefab) : base(name, cooldown)
        {
            this.duration = duration;
            this.range = range;
            this.dps = dps;
            this.tickInterval = tickInterval;
            this.prefab = prefab;
        }

        public override string Describe() => $"최대 {duration}초 · 사거리 {range}m · 초당 피해 {dps} · 간격 {tickInterval}초";

        public override void Release()
        {
            if (beam != null)
                beam.Stop();
        }

        protected override bool CanExecute(SkillContext context)
        {
            return beam == null && context.Caster.GetComponent<PlayerController>().IsRolling == false;
        }

        protected override void Execute(SkillContext context)
        {
            beam = Object.Instantiate(prefab);
            beam.Init(context.Caster, context.Owner, duration, range, dps, tickInterval);
        }
    }
}
