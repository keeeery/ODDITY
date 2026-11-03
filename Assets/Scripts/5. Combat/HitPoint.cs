using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 범위·지속 공격이 넘길 히트 지점을 맞은 대상의 몸 전체 기준으로 구함
    // 보스는 부위 콜라이더마다 따로 등록돼 있어 처음 걸린 부위(발 등)를 기준으로 재면 히트 이펙트가 발밑에 나옴
    // 받는 쪽(BossHitFeedback)이 이 지점을 가장 가까운 몸 표면으로 옮김
    public static class HitPoint
    {
        private static readonly List<Collider> colliders = new List<Collider>();

        // 켜진 비트리거 콜라이더를 모두 감싸는 바운드. 하나도 없으면 fallback의 바운드
        public static Bounds GetBodyBounds(ICombatable target, Collider fallback)
        {
            if (target == null || target.GameObject == null) return fallback.bounds;

            target.GameObject.GetComponentsInChildren(colliders);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Collider part in colliders)
            {
                if (part.isTrigger || part.enabled == false) continue;

                if (hasBounds) bounds.Encapsulate(part.bounds);
                else bounds = part.bounds;
                hasBounds = true;
            }
            colliders.Clear();

            return hasBounds ? bounds : fallback.bounds;
        }

        // from을 몸 가운데 높이로 올린 뒤 몸 바운드에서 가장 가까운 점
        public static Vector3 TowardBody(ICombatable target, Collider fallback, Vector3 from)
        {
            Bounds bounds = GetBodyBounds(target, fallback);
            from.y = bounds.center.y;
            return bounds.ClosestPoint(from);
        }

        // start에서 direction으로 length만큼 뻗은 선 위에서 몸 가운데에 가장 가까운 점
        public static Vector3 OnLineNearBody(ICombatable target, Collider fallback, Vector3 start, Vector3 direction, float length)
        {
            Bounds bounds = GetBodyBounds(target, fallback);
            float along = Mathf.Clamp(Vector3.Dot(bounds.center - start, direction), 0f, length);
            return start + direction * along;
        }
    }
}
