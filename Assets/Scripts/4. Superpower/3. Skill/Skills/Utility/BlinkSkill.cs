using UnityEngine;

namespace TeamOdd.Superpower
{
    // 바라보는 방향으로 순간 이동. 매개변수 2개: Distance(float), PassThrough(bool)
    // - PassThrough가 false면 가는 길에 벽이 있을 때 벽 바로 앞에서 멈춘다
    // - PassThrough가 true면 벽을 무시하고 Distance만큼 간다. 도착 자리가 벽 안이면 벽 앞에서 멈춘다
    public class BlinkSkill : SuperpowerSkill
    {
        private const float WallGap = 0.1f;    // 벽과 띄워 둘 거리 (m)
        private const float GroundLift = 0.1f; // 바닥을 벽으로 치지 않도록 몸통 아래를 띄울 높이 (m)

        private readonly float distance;
        private readonly bool passThrough; // 벽을 통과할 수 있는지

        public BlinkSkill(string name, float cooldown, float distance, bool passThrough) : base(name, cooldown)
        {
            this.distance = distance;
            this.passThrough = passThrough;
        }

        public override string Describe() => $"거리 {distance}m · 벽 통과 {(passThrough ? "O" : "X")}";

        protected override void Execute(SkillContext context)
        {
            Transform caster = context.Caster;
            CharacterController controller = caster.GetComponent<CharacterController>();

            float moveDistance = distance;
            if (controller != null && (passThrough == false || IsBlocked(controller, caster.position + context.AimDirection * distance)))
                moveDistance = GetDistanceToWall(controller, context.AimDirection);

            // CharacterController가 켜져 있으면 position을 바꿔도 다음 프레임에 원래 자리로 돌아가므로 잠깐 끈다
            if (controller != null) controller.enabled = false;
            caster.position += context.AimDirection * moveDistance;
            if (controller != null) controller.enabled = true;
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
