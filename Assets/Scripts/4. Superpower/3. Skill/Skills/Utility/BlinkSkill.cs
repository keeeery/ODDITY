using UnityEngine;

namespace TeamOdd.Superpower
{
    // 바라보는 방향으로 순간 이동. 매개변수 2개: Distance(float), PassThrough(bool)
    // - PassThrough가 false면 가는 길에 벽이 있을 때 벽 바로 앞에서 멈춘다
    // - PassThrough가 true면 벽을 무시하고 Distance만큼 간다. 도착 자리가 벽 안이면 벽 앞에서 멈춘다
    // - 웜홀 이펙트: 출발한 자리에는 작아지는 구체(Enter), 도착한 자리에는 커지는 구체(Exit)를 몸통 높이에 띄운다
    //   SkillChip.tsv의 PrefabKey로 불러온 프리팹 하나에 Enter, Exit 자식이 들어 있고, 자식을 하나씩 꺼내 띄운다
    //   플레이어를 따라가지 않도록 그 자리에 두고, 지우는 건 프리팹이 한다 (파티클 Stop Action = Destroy)
    public class BlinkSkill : SuperpowerSkill
    {
        private const float WallGap = 0.1f;    // 벽과 띄워 둘 거리 (m)
        private const float GroundLift = 0.1f; // 바닥을 벽으로 치지 않도록 몸통 아래를 띄울 높이 (m)
        private const float EffectHeight = 1f; // 이펙트를 띄울 높이. 발밑에서 몸통까지 (m)

        private readonly float distance;
        private readonly bool passThrough; // 벽을 통과할 수 있는지
        private readonly GameObject enterEffect; // 출발 자리 이펙트 (프리팹의 Enter 자식)
        private readonly GameObject exitEffect;  // 도착 자리 이펙트 (프리팹의 Exit 자식)

        // 자식 이름이 틀리면 Find가 null이라 테이블을 읽을 때(SkillFactory로 한 번 만들어 볼 때) 바로 예외가 난다
        public BlinkSkill(string name, float cooldown, float distance, bool passThrough, GameObject effect) : base(name, cooldown)
        {
            this.distance = distance;
            this.passThrough = passThrough;
            enterEffect = effect.transform.Find("Enter").gameObject;
            exitEffect = effect.transform.Find("Exit").gameObject;
        }

        public override string Describe() => $"거리 {distance}m · 벽 통과 {(passThrough ? "O" : "X")}";

        protected override void Execute(SkillContext context)
        {
            Transform caster = context.Caster;
            CharacterController controller = caster.GetComponent<CharacterController>();

            float moveDistance = distance;
            if (controller != null && (passThrough == false || IsBlocked(controller, caster.position + context.AimDirection * distance)))
                moveDistance = GetDistanceToWall(controller, context.AimDirection);

            Object.Instantiate(enterEffect, caster.position + Vector3.up * EffectHeight, Quaternion.identity);

            // CharacterController가 켜져 있으면 position을 바꿔도 다음 프레임에 원래 자리로 돌아가므로 잠깐 끈다
            if (controller != null) controller.enabled = false;
            caster.position += context.AimDirection * moveDistance;
            if (controller != null) controller.enabled = true;

            Object.Instantiate(exitEffect, caster.position + Vector3.up * EffectHeight, Quaternion.identity);
        }

        // 플레이어 몸통(캡슐)을 앞으로 밀어 보아 처음 닿는 벽까지의 거리
        private float GetDistanceToWall(CharacterController controller, Vector3 direction)
        {
            GetCapsule(controller, controller.transform.position, out Vector3 bottom, out Vector3 top);

            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, controller.radius, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(controller.transform)) continue;
                if (hit.distance < distance) return Mathf.Max(0f, hit.distance - WallGap);
            }

            return distance;
        }

        // 도착 자리에 플레이어 몸통이 들어갈 수 없는지
        private static bool IsBlocked(CharacterController controller, Vector3 position)
        {
            GetCapsule(controller, position, out Vector3 bottom, out Vector3 top);

            foreach (Collider collider in Physics.OverlapCapsule(bottom, top, controller.radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(controller.transform) == false) return true;
            }

            return false;
        }

        // CharacterController 캡슐의 아래·위 구 중심. 바닥에 닿지 않게 아래 구만 GroundLift만큼 띄운다
        private static void GetCapsule(CharacterController controller, Vector3 position, out Vector3 bottom, out Vector3 top)
        {
            Vector3 center = position + controller.center;
            float half = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
            bottom = center - Vector3.up * half + Vector3.up * GroundLift;
            top = center + Vector3.up * half;
        }
    }
}
