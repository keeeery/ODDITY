using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd
{
    public class HumanoidDeflectSlash : HumanoidReactionPattern
    {
        [Title("Cut")]
        [SerializeField] private float cutRadius = 7f; // OnDeflectCut 신호 순간 이 거리 안이면 베어 부숨
        [SerializeField] private float closeRadius = 4f; // 신호 전이라도 이만큼 붙으면 몸에 맞기 전에 부숨

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
            Vector3 toBoss = root.transform.position - center;
            toBoss.y = 0f;
            Projectile.ShatterAt(center + toBoss.normalized * 0.5f);
        }
    }
}
