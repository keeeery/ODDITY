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
                SkillType.PsychoSlam => new PsychoSlamSkill(name, cooldown, p.GetFloat("Duration"), p.GetFloat("Height"), p.GetInt("Damage"), p.GetFloat("Range"), p.GetFloat("Angle")),
                SkillType.Barrage => new BarrageSkill(name, cooldown, p.GetString("Projectile"), p.GetInt("Count"), p.GetFloat("Spread")),

                // 방어
                SkillType.Heal => new HealSkill(name, cooldown, p.GetInt("Amount")),
                SkillType.Barrier => new BarrierSkill(name, cooldown, p.GetInt("Shield"), p.GetFloat("Duration")),

                // 유틸
                SkillType.Blink => new BlinkSkill(name, cooldown, p.GetFloat("Distance"), p.GetBool("PassThrough")),
                SkillType.TimeStop => new TimeStopSkill(name, cooldown, p.GetFloat("Duration")),
                SkillType.TimeSlow => new TimeSlowSkill(name, cooldown, p.GetFloat("Duration"), p.GetFloat("Multiplier")),
                SkillType.Resize => new ResizeSkill(name, cooldown, p.GetFloat("Multiplier")),

                _ => null,
            };

            if (skill == null)
                Debug.LogError($"[SkillFactory] {def.ID}번 칩의 스킬 {def.SkillType}을(를) 만드는 코드가 없습니다.");

            return skill;
        }
    }
}
