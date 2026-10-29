using UnityEngine;

namespace TeamOdd.Superpower
{
    // 적을 쫓아다니며 피해를 주는 염동력 폭풍을 만든다.
    // 매개변수 5개: Duration(float), DPS(int), TrackSpeed(float), Radius(float), TickInterval(float)
    // - 진행: 플레이어 앞 SpawnDistance m에 폭풍이 생겨 Duration초 동안 유지된다. 높이(y)는 항상 0으로 바닥에 붙어 다닌다
    // - 추적: 폭풍에서 SearchRadius m 안의 가장 가까운 적(MonsterRegistry에 등록된 것)을 향해 초당 TrackSpeed m로 수평으로 움직인다
    //         쫓던 적이 죽거나 사라지면 다시 가장 가까운 적을 찾고, 적이 없으면 제자리에 머문다
    // - 피해: TickInterval초마다 폭풍 반경 Radius m 안의 모든 적에게 DPS × TickInterval 피해 (1초 간격이면 초당 DPS)
    // - 적이 없어도 쓸 수 있다 (폭풍을 먼저 깔아 두는 용도)
    // 이 클래스는 값과 쿨타임을 들고 폭풍 프리팹을 띄우기만 한다. 쫓아다니며 피해를 주는 일은 프리팹의 PsychoTempest가 한다
    public class PsychoTempestSkill : SuperpowerSkill
    {
        private const float SpawnDistance = 2f;     // 폭풍이 생기는 플레이어 앞 거리 (m)

        private readonly float duration;
        private readonly int dps;
        private readonly float trackSpeed;
        private readonly float radius;       // 폭풍이 피해를 주는 반경 (m)
        private readonly float tickInterval; // 피해를 주는 간격 (초)
        private readonly PsychoTempest prefab; // SkillChip.tsv의 PrefabKey로 불러온 프리팹

        public PsychoTempestSkill(string name, float cooldown, float duration, int dps, float trackSpeed, float radius, float tickInterval, PsychoTempest prefab) : base(name, cooldown)
        {
            this.duration = duration;
            this.dps = dps;
            this.trackSpeed = trackSpeed;
            this.radius = radius;
            this.tickInterval = tickInterval;
            this.prefab = prefab;
        }

        public override string Describe() => $"{duration}초 · 초당 피해 {dps} · 추적 속도 {trackSpeed}m/s · 반경 {radius}m · 간격 {tickInterval}초";

        protected override void Execute(SkillContext context)
        {
            Vector3 spawn = context.Caster.position + context.AimDirection * SpawnDistance;
            spawn.y = 0f;

            PsychoTempest storm = Object.Instantiate(prefab, spawn, Quaternion.identity);
            storm.Init(context.Owner, duration, dps, trackSpeed, radius, tickInterval);
        }
    }
}
