using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 스킬 칩의 테이블 값으로 스킬 객체를 만든다.
    /// (팩토리 패턴 중 switch 하나로 고르는 단순 팩토리. 99. Study A_MonsterSpawner에서 SelectMonsterSpawner의 switch와 같은 자리)
    /// 어떤 스킬 클래스가 있는지, 각 스킬이 매개변수를 몇 개, 어떤 타입으로 받는지는 여기만 안다.
    /// 부르는 쪽(AugmentationChipInventory, AugmentationChipTable)은 SuperpowerSkill로만 받으므로 스킬이 늘어나도 바뀌지 않는다.
    /// 공격/방어/유틸 분류는 표시·정렬용 꼬리표라 만드는 방법에는 영향이 없다. switch도 읽기 쉽게 분류별로 묶어 두었을 뿐이다.
    /// 스킬을 추가할 때: SkillType에 이름 추가 → Skills/분류 폴더에 SuperpowerSkill 자식 클래스 작성 → 아래 switch의 같은 분류 묶음에 한 줄 추가 → 테이블에 칩 추가(Category 칸 포함)
    /// 프리팹을 띄우는 스킬은 표의 PrefabKey 칸에 Resources 경로를 적고, 여기서 def.Prefab에 붙은 스크립트를 꺼내 넘긴다.
    /// PrefabKey를 비웠거나 경로가 틀리면 def.Prefab이 null이라 테이블을 읽을 때 바로 예외가 난다.
    /// </summary>
    public static class SkillFactory
    {
        public static SuperpowerSkill Create(SkillChipDef def)
        {
            string name = def.Name;
            float cooldown = def.Cooldown;
            SkillParams p = def.Params;

            SuperpowerSkill skill = def.SkillType switch
            {
                // 공격
                SkillType.Shockwave => new ShockwaveSkill(name, cooldown, p.GetFloat("Radius"), p.GetFloat("Force"), p.GetInt("Damage")),
                SkillType.PsychoSlam => new PsychoSlamSkill(name, cooldown, p.GetFloat("Duration"), p.GetFloat("Height"), p.GetInt("Damage"), p.GetInt("Groggy"), p.GetFloat("Range"), p.GetFloat("Angle")),
                SkillType.PsychoRapidFire => new PsychoRapidFireSkill(name, cooldown, p.GetFloat("Radius"), p.GetInt("Count"), p.GetFloat("Interval"), p.GetFloat("Speed")),
                SkillType.PsychoTempest => new PsychoTempestSkill(name, cooldown, p.GetFloat("Duration"), p.GetInt("DPS"), p.GetFloat("TrackSpeed"), p.GetFloat("Radius"), p.GetFloat("TickInterval"), def.Prefab.GetComponent<PsychoTempest>()),
                SkillType.PsychoOrb => new PsychoOrbSkill(name, cooldown, p.GetFloat("Speed"), p.GetFloat("SlowSpeed"), p.GetFloat("Range"), p.GetFloat("Radius"), p.GetInt("DPS"), p.GetFloat("TickInterval"), def.Prefab.GetComponent<PsychoOrb>()),
                SkillType.PsychoBeam => new PsychoBeamSkill(name, cooldown, p.GetFloat("Duration"), p.GetFloat("Range"), p.GetInt("DPS"), p.GetFloat("TickInterval"), def.Prefab.GetComponent<PsychoBeam>()),

                // 방어
                SkillType.Heal => new HealSkill(name, cooldown, p.GetInt("Amount")),
                SkillType.Barrier => new BarrierSkill(name, cooldown, p.GetInt("Shield"), p.GetFloat("Duration")),
                SkillType.TimeReverse => new TimeReverseSkill(name, cooldown, p.GetFloat("Duration")),

                // 유틸
                SkillType.Blink => new BlinkSkill(name, cooldown, p.GetFloat("Distance"), p.GetBool("PassThrough")),
                SkillType.TimeStop => new TimeStopSkill(name, cooldown, p.GetFloat("Duration")),
                SkillType.TimeSlow => new TimeSlowSkill(name, cooldown, p.GetFloat("Duration"), p.GetFloat("Multiplier")),
                SkillType.Resize => new ResizeSkill(name, cooldown, p.GetFloat("Multiplier")),

                // 패시브. 쿨타임은 내부 쿨타임으로 쓴다
                SkillType.SecondHeart => new SecondHeartSkill(name, cooldown, p.GetInt("Amount")),
                SkillType.LifeSteal => new LifeStealSkill(name, cooldown, p.GetInt("Amount")),
                SkillType.ConditionedReflex => new ConditionedReflexSkill(name, cooldown),
                SkillType.RightfulRevenge => new RightfulRevengeSkill(name, cooldown, p.GetInt("Amount")),
                SkillType.AdrenalineHypersecretion => new AdrenalineHypersecretionSkill(name, cooldown, p.GetFloat("Amount")),

                _ => null,
            };

            if (skill == null)
                Debug.LogError($"[SkillFactory] {def.ID}번 칩의 스킬 {def.SkillType}을(를) 만드는 코드가 없습니다.");

            return skill;
        }
    }
}
