using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd
{
    public class HumanoidDeflectSlash : HumanoidReactionPattern
    {
        [Title("Cut")]
        [SerializeField] private float cutRadius = 7f; // OnDeflectCut 신호 순간 이 거리 안이면 베어 부숨
        [SerializeField] private float closeRadius = 4f; // 신호 전이라도 이만큼 붙으면 몸에 맞기 전에 부숨

        // 베어 부순 순간 물체 자리에서 터지는 연출
        [Title("Feedback")]
        [SerializeField, Min(0f)] private float cutHitStop = 0.08f; // 0이면 쓰지 않음
        [SerializeField] private HumanoidReflectBurst cutBurst = new();

        private bool hasCut;

        // 베기 전에 물체가 먼저 몸에 닿아도 피해를 받지 않음
        protected override void OnReactionEnter()
        {
            hasCut = false;
            SetGuarding(true);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (hasCut || IsProjectileFlying == false) return;
            if (ProjectileDistance <= closeRadius) Cut();
        }

        private void OnDestroy() => cutBurst.Dispose();

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (name != "OnDeflectCut" || hasCut) return;

            if (IsProjectileFlying && ProjectileDistance <= cutRadius) Cut();

            // 놓쳤으면 이후엔 그냥 맞음
            hasCut = true;
            SetGuarding(false);
        }

        private void Cut()
        {
            hasCut = true;
            SetGuarding(false);

            // 맞은 지점을 보스 쪽에 두어 파편이 보스 반대쪽으로 튐
            Vector3 center = Projectile.Body.worldCenterOfMass;
            HitStop.Request(cutHitStop);
            cutBurst.Play(center);

            Vector3 toBoss = root.transform.position - center;
            toBoss.y = 0f;
            Projectile.ShatterAt(center + toBoss.normalized * 0.5f);
        }
    }
}
