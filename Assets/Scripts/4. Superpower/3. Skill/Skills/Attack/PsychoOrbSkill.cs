using UnityEngine;

namespace TeamOdd.Superpower
{
    // 바라보는 방향으로 염동력 구체를 던진다. 적에 닿으면 크게 느려져 천천히 통과하며 피해를 준다.
    // 매개변수 6개: Speed(float), SlowSpeed(float), Range(float), Radius(float), PowerPerSecond(int), TickInterval(float)
    // - 진행: 플레이어 가슴 높이에서 화면 중앙 조준점을 향해 날아가고, 날아간 거리가 Range m가 되면 사라진다
    //         방향은 던지는 순간 한 번만 정하므로 날아가는 중에 카메라를 돌려도 바뀌지 않는다
    // - 속도: 구체 반경 Radius m 안에 적이 하나라도 있으면 초당 SlowSpeed m, 없으면 초당 Speed m
    // - 피해: 적과 닿아 있는 동안 TickInterval초마다 반경 안의 모든 적에게 공격력 × PowerPerSecond% × TickInterval 피해. 처음 닿은 순간 바로 한 번 준다
    // - 벽이나 바닥은 무시하고 지나간다
    // 이 클래스는 값과 쿨타임을 들고 구체 프리팹을 띄우기만 한다. 날아가며 피해를 주는 일은 프리팹의 PsychoOrb가 한다
    public class PsychoOrbSkill : SuperpowerSkill
    {
        private const float SpawnHeight = 1.2f;   // 구체가 생기는 발 기준 높이 (m)
        private const float SpawnDistance = 1f;   // 구체가 생기는 플레이어 앞 거리 (m)
        private const float AimDistance = 100f;   // 조준점을 찾을 거리 (m), PsychokinesisController의 투척과 같음

        private readonly float speed;
        private readonly float slowSpeed;
        private readonly float range;
        private readonly float radius;
        private readonly int powerPerSecond; // 초당 피해 배율 (공격력의 %)
        private readonly float tickInterval;
        private readonly PsychoOrb prefab; // SkillChip.tsv의 PrefabKey로 불러온 프리팹

        public PsychoOrbSkill(string name, float cooldown, float speed, float slowSpeed, float range, float radius, int powerPerSecond, float tickInterval, PsychoOrb prefab) : base(name, cooldown)
        {
            this.speed = speed;
            this.slowSpeed = slowSpeed;
            this.range = range;
            this.radius = radius;
            this.powerPerSecond = powerPerSecond;
            this.tickInterval = tickInterval;
            this.prefab = prefab;
        }

        public override string Describe() => $"속도 {speed}m/s (적에 닿으면 {slowSpeed}m/s) · 사거리 {range}m · 반경 {radius}m · 초당 피해 공격력의 {powerPerSecond}% · 간격 {tickInterval}초";

        protected override void Execute(SkillContext context)
        {
            Vector3 spawn = context.Caster.position + Vector3.up * SpawnHeight + context.AimDirection * SpawnDistance;
            Vector3 direction = GetCrosshairDirection(spawn, context.Caster);

            PsychoOrb orb = Object.Instantiate(prefab, spawn, Quaternion.identity);
            orb.Init(direction, context.Owner, speed, slowSpeed, range, radius, powerPerSecond, tickInterval, Sounds);
        }

        // 구체가 생기는 곳에서 화면 중앙 조준점으로 가는 방향. 구체가 카메라와 다른 곳에서 출발해도 조준점에 맞도록 카메라 방향 대신 사용
        // 플레이어 자신은 조준점으로 치지 않음 (PsychoRapidFireSkill의 같은 이름 메서드와 같은 방식)
        private static Vector3 GetCrosshairDirection(Vector3 spawn, Transform caster)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = AimDistance;

            foreach (RaycastHit hit in Physics.RaycastAll(ray, AimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(caster)) continue;

                distance = Mathf.Min(distance, hit.distance);
            }

            Vector3 direction = ray.GetPoint(distance) - spawn;

            // 카메라와 구체 사이의 가까운 장애물을 조준하면 뒤로 날아가므로 그때는 카메라 방향
            if (Vector3.Dot(direction, ray.direction) <= 0f) direction = ray.direction;

            return direction.normalized;
        }
    }
}
