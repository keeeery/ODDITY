using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using TeamOdd.UI;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 주변의 염동력 오브젝트를 머리 둘레로 끌어와 Interval초 간격으로 하나씩 조준점을 향해 던진다.
    // 매개변수 4개: Radius(float), Count(int), Interval(float), Speed(float)
    // - 대상: 플레이어에서 Radius m 안, 기본 염동력으로 들 수 있는(질량이 PsychoMaxMass 이하) 오브젝트 최대 Count개
    //         하나도 없으면 쓰지 않고 쿨타임도 돌지 않는다. 플레이어나 다른 스킬이 들고 있는 오브젝트는 제외
    // - 진행: PullTime초 동안 플레이어 머리 둘레에 반원으로 모은 뒤, 왼쪽(0번)부터 오른쪽 순서로 던진다
    //         카메라에서 보았을 때 0번이 왼쪽 귀 옆, 가운데가 머리 위, 마지막이 오른쪽 귀 옆이고 나머지는 그 사이에 같은 각도로 놓인다
    // - 던질 때마다 그 순간의 조준점을 다시 구하므로 연사 중에 카메라를 돌리면 따라간다. 차례를 기다리는 오브젝트는 제자리에 머문 채로 따라온다
    // - 피해는 일반 투척과 같다 (질량 × 충돌 속도, PsychokinesisObject가 처리)
    public class PsychoRapidFireSkill : SuperpowerSkill
    {
        private const float PullTime = 0.5f;            // 끌어오는 시간 (초)
        private const float PullGain = 12f;             // 끌어올 때 남은 거리에 곱할 속도 배율
        private const float MaxPullSpeed = 30f;         // 끌어오는 최대 속도 (m/s)
        private const float HeadHeight = 1.5f;          // 반원의 중심(머리)까지 발 기준 높이 (m)
        private const float BaseArcRadius = 1.3f;       // 머리에서 물체까지 기본 거리 (m), 머리에 닿지 않을 만큼 여유를 둔 값
        private const float ArcRadiusPerShot = 0.35f;   // 잡은 물체 하나마다 늘릴 거리 (m), 많이 잡아도 물체끼리 닿지 않게 (5개면 3.5m)
        private const float AimDistance = 100f;         // 조준점을 찾을 거리 (m), PsychokinesisController의 투척과 같음

        private readonly float radius;
        private readonly int count;
        private readonly float interval;
        private readonly float speed;

        private readonly List<PsychokinesisObject> targets = new List<PsychokinesisObject>(); // CanExecute에서 고른 대상, Execute에서 씀

        private bool isFiring; // 끌어와서 마지막 하나를 던질 때까지 true

        public override bool IsCasting => isFiring;

        public PsychoRapidFireSkill(string name, float cooldown, float radius, int count, float interval, float speed) : base(name, cooldown)
        {
            this.radius = radius;
            this.count = count;
            this.interval = interval;
            this.speed = speed;
        }

        public override string Describe() => $"반경 {radius}m · 최대 {count}개 · 간격 {interval}초 · 속도 {speed}m/s";

        protected override bool CanExecute(SkillContext context)
        {
            FindTargets(context);
            if (targets.Count > 0) return true;

            Debug.Log($"[{Name}] 반경 {radius}m 안에 들 수 있는 염동력 오브젝트가 없습니다.");
            SystemMessage.Show("주변에 들 수 있는 염동력 오브젝트가 없습니다.", SystemMessage.Tone.Warning);
            return false;
        }

        protected override void Execute(SkillContext context)
        {
            List<PsychokinesisObject> shots = new List<PsychokinesisObject>(targets);
            targets.Clear();
            context.Owner.StartCoroutine(Run(shots, context)); // 스킬은 MonoBehaviour가 아니므로 시전한 플레이어가 돌린다. 플레이어가 사라지면 같이 멈춤
        }

        // 범위 안에서 먼저 찾은 것부터 최대 count개 (거리 순서는 따지지 않음)
        private void FindTargets(SkillContext context)
        {
            targets.Clear();
            int maxMass = context.Owner.CurrentStats.Get(StatType.PsychoMaxMass);
            Vector3 center = context.Caster.position;

            foreach (PsychokinesisObject candidate in PsychokinesisObject.ActiveObjects)
            {
                if (candidate.IsHeld || candidate.IsAvailable(maxMass) == false) continue;
                if (Vector3.Distance(center, candidate.Body.worldCenterOfMass) > radius) continue;

                targets.Add(candidate);
                if (targets.Count == count) break;
            }
        }

        // 잡아서 끌어온 뒤 하나씩 던짐. 중간에 오브젝트가 부서지거나(OnDisable에서 Release) 꺼지면 그것만 빠진다
        // 중간에 멈추더라도 잡힌 채로 남지 않도록 finally에서 남은 것을 놓음
        private IEnumerator Run(List<PsychokinesisObject> shots, SkillContext context)
        {
            isFiring = true;

            Collider[] casterColliders = context.Caster.GetComponentsInChildren<Collider>();
            foreach (PsychokinesisObject shot in shots)
                shot.BeginHold(casterColliders, context.Owner);

            Sounds.Play("Pull");

            try
            {
                // 끌어오기
                for (float elapsed = 0f; elapsed < PullTime; elapsed += Time.fixedDeltaTime)
                {
                    LineUp(shots, context);
                    yield return new WaitForFixedUpdate();
                }

                // 연사
                for (int i = 0; i < shots.Count; i++)
                {
                    // 차례를 기다리는 동안 부서진 것은 기다리지 않고 건너뜀
                    if (IsStillHeld(shots[i]) == false) continue;

                    shots[i].Release(GetCrosshairDirection(shots[i], context) * speed, true, context.Owner);
                    Sounds.Play("Fire");

                    if (i == shots.Count - 1) yield break;

                    for (float elapsed = 0f; elapsed < interval; elapsed += Time.fixedDeltaTime)
                    {
                        LineUp(shots, context);
                        yield return new WaitForFixedUpdate();
                    }
                }
            }
            finally
            {
                isFiring = false;
                foreach (PsychokinesisObject shot in shots)
                    if (IsStillHeld(shot)) shot.Release(Vector3.zero);
            }
        }

        // 아직 던지지 않은 것을 머리 둘레 반원의 자기 자리로 당긴다. i번째는 항상 i번째 자리
        private static void LineUp(List<PsychokinesisObject> shots, SkillContext context)
        {
            Vector3 head = context.Caster.position + Vector3.up * HeadHeight;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : context.Caster.right; // 화면 기준 오른쪽
            float arcRadius = BaseArcRadius + ArcRadiusPerShot * shots.Count; // 던져서 빠져도 shots.Count는 그대로라 연사 중에 크기가 바뀌지 않음

            for (int i = 0; i < shots.Count; i++)
            {
                if (IsStillHeld(shots[i]) == false) continue;

                // 각도: 0번은 180도(왼쪽 귀 옆), 가운데는 90도(머리 위), 마지막은 0도(오른쪽 귀 옆)
                float t = shots.Count > 1 ? i / (float)(shots.Count - 1) : 0.5f;
                float angle = Mathf.Lerp(180f, 0f, t) * Mathf.Deg2Rad;
                Vector3 slot = head + (right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * arcRadius;

                Rigidbody body = shots[i].Body;
                body.linearVelocity = Vector3.ClampMagnitude((slot - body.worldCenterOfMass) * PullGain, MaxPullSpeed);
            }
        }

        private static bool IsStillHeld(PsychokinesisObject shot) => shot != null && shot.isActiveAndEnabled && shot.IsHeld;

        // 물체에서 화면 중앙 조준점으로 가는 방향. 물체가 플레이어 옆에 늘어서 있어도 조준점에 맞도록 카메라 방향 대신 사용
        // 플레이어와 아직 늘어서 있는 오브젝트는 조준점으로 치지 않음
        private static Vector3 GetCrosshairDirection(PsychokinesisObject shot, SkillContext context)
        {
            Camera camera = Camera.main;

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            float distance = AimDistance;

            foreach (RaycastHit hit in Physics.RaycastAll(ray, AimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(context.Caster)) continue;
                if (hit.rigidbody != null && hit.rigidbody.TryGetComponent(out PsychokinesisObject held) && held.IsHeld) continue;

                distance = Mathf.Min(distance, hit.distance);
            }

            Vector3 direction = ray.GetPoint(distance) - shot.Body.worldCenterOfMass;

            // 카메라와 물체 사이의 가까운 장애물을 조준하면 뒤로 날아가므로 그때는 카메라 방향
            if (Vector3.Dot(direction, ray.direction) <= 0f) direction = ray.direction;

            return direction.normalized;
        }
    }
}
