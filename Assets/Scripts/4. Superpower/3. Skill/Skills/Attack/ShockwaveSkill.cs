using System.Collections.Generic;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 주변을 밀어내는 충격파. 매개변수 3개: Radius(float), Force(float), Damage(int)
    // - 플레이어에서 Radius m 안의 적(MonsterRegistry에 등록된 것)에게 피해 Damage
    // - 같은 범위 안의 움직이는 Rigidbody(보스, 염동력 오브젝트 등)를 플레이어 반대쪽으로 Force m/s 속도만큼 밀어낸다
    //   무게와 상관없이 같은 속도로 밀리게 VelocityChange를 쓴다. 들고 있는 오브젝트와 시간이 멈춘 것(kinematic)은 밀리지 않는다
    public class ShockwaveSkill : SuperpowerSkill
    {
        private const float UpwardRatio = 0.3f; // 살짝 위로 띄워 바닥에 끌리지 않게 하는 비율

        private readonly float radius;
        private readonly float force;
        private readonly int damage;

        // 콜라이더가 여러 개인 대상이 여러 번 맞지 않도록 이번에 처리한 것을 담아 둔다
        private readonly List<ICombatable> hitTargets = new List<ICombatable>();
        private readonly List<Rigidbody> pushedBodies = new List<Rigidbody>();

        public ShockwaveSkill(string name, float cooldown, float radius, float force, int damage) : base(name, cooldown)
        {
            this.radius = radius;
            this.force = force;
            this.damage = damage;
        }

        public override string Describe() => $"반경 {radius}m · 미는 힘 {force} · 피해 {damage}";

        protected override void Execute(SkillContext context)
        {
            hitTargets.Clear();
            pushedBodies.Clear();

            Vector3 center = context.Caster.position;
            Collider[] colliders = Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider collider in colliders)
            {
                if (collider.transform.IsChildOf(context.Caster)) continue;

                Damage(collider, context.Owner);
                Push(collider, center);
            }
        }

        private void Damage(Collider collider, CombatEntity owner)
        {
            if (MonsterRegistry.TryGetMonster(collider, out ICombatable target) == false) return;
            if (hitTargets.Contains(target)) return;

            hitTargets.Add(target);
            DamageInfo info = new DamageInfo(damage, collider.ClosestPoint(owner.transform.position), collider, HitReaction.Knockback);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(owner, target, info));
        }

        private void Push(Collider collider, Vector3 center)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body == null || body.isKinematic || pushedBodies.Contains(body)) return;
            if (body.TryGetComponent(out PsychokinesisObject psychoObject) && psychoObject.IsHeld) return;

            pushedBodies.Add(body);

            Vector3 away = body.worldCenterOfMass - center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward; // 플레이어와 겹쳐 있으면 아무 쪽으로나

            Vector3 direction = (away.normalized + Vector3.up * UpwardRatio).normalized;
            body.AddForce(direction * force, ForceMode.VelocityChange);
        }
    }
}
