using System.Collections;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Player;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 조준 방향의 보스 하나의 시간을 멈추고 염동력으로 들어 올렸다가 바닥에 내려찍어 피해를 준다.
    // 매개변수 5개: Duration(float), Height(float), Damage(int), Range(float), Angle(float)
    // - 대상: 플레이어에서 Range m 안, 조준 방향에서 좌우 Angle도 안에 있는 가장 가까운 보스. 없으면 쓰지 않고 쿨타임도 돌지 않는다
    // - 진행: Duration초 동안 보스를 멈춘 채 Height m까지 들어 올리고, 마지막 slamTime초에 원래 바닥으로 내려찍은 뒤 정지를 푼다
    // - 그동안 플레이어는 염동력 홀딩 자세로 보스 몸통 쪽에 팔을 뻗어, 보스를 따라 팔이 올라갔다 내려찍힌다
    public class PsychoSlamSkill : SuperpowerSkill
    {
        private const float SlamTime = 0.15f;          // 내려찍는 데 걸리는 시간 (초)
        private const float GroundProbeHeight = 50f;   // 보스 아래 바닥을 찾을 거리 (m)

        private readonly float duration;
        private readonly float height;
        private readonly int damage;
        private readonly float range;
        private readonly float angle;

        private BossMonster target; // CanExecute에서 고른 대상, Execute에서 씀

        public PsychoSlamSkill(string name, float cooldown, float duration, float height, int damage, float range, float angle) : base(name, cooldown)
        {
            this.duration = duration;
            this.height = height;
            this.damage = damage;
            this.range = range;
            this.angle = angle;
        }

        public override string Describe() => $"보스 정지 {duration}초 · 높이 {height}m · 피해 {damage} · 사거리 {range}m";

        protected override bool CanExecute(SkillContext context)
        {
            target = FindTarget(context);
            if (target != null) return true;

            Debug.Log($"[{Name}] 사거리 {range}m · 각도 ±{angle}도 안에 보스가 없습니다.");
            return false;
        }

        protected override void Execute(SkillContext context)
        {
            context.Caster.TryGetComponent(out PlayerController holder);
            TimeFreezeManager.Instance.StartCoroutine(Slam(target, context.Owner, holder));
            target = null;
        }

        private BossMonster FindTarget(SkillContext context)
        {
            BossMonster closest = null;
            float closestDistance = range;

            foreach (BossMonster boss in Object.FindObjectsByType<BossMonster>(FindObjectsSortMode.None))
            {
                if (boss.TryGetComponent(out CombatEntity entity) && entity.IsDead) continue;

                Vector3 offset = boss.transform.position - context.Caster.position;
                offset.y = 0f;

                float distance = offset.magnitude;
                if (distance > closestDistance) continue;
                if (distance > 0.01f && Vector3.Angle(context.AimDirection, offset) > angle) continue;

                closest = boss;
                closestDistance = distance;
            }

            return closest;
        }

        // 보스는 멈춰서 kinematic이므로 물리 스텝마다 MovePosition으로 옮긴다
        // 보스가 중간에 사라져도 플레이어 홀딩 자세는 풀리도록 finally에서 정리
        private IEnumerator Slam(BossMonster boss, CombatEntity owner, PlayerController holder)
        {
            TimeFreezable clock = boss.Clock;
            Rigidbody body = boss.Body;
            Transform holdPoint = CreateHoldPoint(body);
            clock.Freeze();
            if (holder != null) holder.BeginExternalHold(holdPoint);

            try
            {
                yield return Lift(boss, body);
                if (boss == null) yield break;

                SendDamage(boss, owner, body.position);
                clock.DiscardSavedMotion();
                clock.Unfreeze();
            }
            finally
            {
                if (holder != null) holder.EndExternalHold(holdPoint);
                if (holdPoint != null) Object.Destroy(holdPoint.gameObject);
            }
        }

        // 팔을 뻗을 보스 몸통 중심, 보스 몸체에 붙여 들어 올리고 내려찍는 동안 함께 움직이게 함
        private static Transform CreateHoldPoint(Rigidbody body)
        {
            Transform point = new GameObject("PsychoSlam Hold Point").transform;
            point.SetParent(body.transform, false);
            point.position = body.worldCenterOfMass;
            return point;
        }

        // 들어 올렸다가 바닥까지 내려찍음, 보스가 중간에 사라지면 그대로 끝
        private IEnumerator Lift(BossMonster boss, Rigidbody body)
        {
            Vector3 start = body.position;
            Vector3 ground = FindGround(start);
            Vector3 top = start + Vector3.up * height;

            float slamTime = Mathf.Min(SlamTime, duration);
            float liftTime = duration - slamTime;

            // 들어 올리기: 빠르게 떠올라 꼭대기에서 잠깐 머무는 느낌
            for (float elapsed = 0f; elapsed < liftTime; elapsed += Time.fixedDeltaTime)
            {
                if (boss == null) yield break;

                float t = 1f - Mathf.Pow(1f - elapsed / liftTime, 3f);
                body.MovePosition(Vector3.Lerp(start, top, t));
                yield return new WaitForFixedUpdate();
            }

            // 내려찍기: 갈수록 빨라짐
            for (float elapsed = 0f; elapsed < slamTime; elapsed += Time.fixedDeltaTime)
            {
                if (boss == null) yield break;

                float t = elapsed / slamTime;
                body.MovePosition(Vector3.Lerp(top, ground, t * t));
                yield return new WaitForFixedUpdate();
            }

            if (boss == null) yield break;

            body.MovePosition(ground);
            yield return new WaitForFixedUpdate();
        }

        // 공중(점프 중 등)에서 잡혔으면 그 아래 바닥으로, 바닥을 못 찾으면 잡힌 높이로 내려찍음
        private static Vector3 FindGround(Vector3 start)
        {
            int groundLayers = LayerMask.GetMask("Ground");
            Vector3 origin = start + Vector3.up * 0.5f;

            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeHeight, groundLayers, QueryTriggerInteraction.Ignore)
                ? new Vector3(start.x, hit.point.y, start.z)
                : start;
        }

        private void SendDamage(BossMonster boss, CombatEntity owner, Vector3 hitPoint)
        {
            ICombatable receiver = boss.GetComponent<ICombatable>();
            if (receiver == null)
            {
                Debug.LogWarning($"[{Name}] {boss.name}에 ICombatable이 없어 피해를 주지 못했습니다.", boss);
                return;
            }

            DamageInfo info = new DamageInfo(damage, hitPoint, receiver.Collider, HitReaction.Knockdown);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, receiver, info));
        }
    }
}
