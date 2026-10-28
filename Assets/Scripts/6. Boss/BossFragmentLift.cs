using System.Collections.Generic;
using DG.Tweening;
using TeamOdd.Arena;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 충격파, 점프 착지처럼 바닥을 강하게 치는 순간 범위 안의 염동력 오브젝트를 위로 살짝 띄우는 연출
    // 떠오르는 동안 물리와 부딪히지 않도록 kinematic으로 띄우고, 다 떠오르면 되돌려 중력으로 떨어뜨림
    public static class BossFragmentLift
    {
        // 크기별 풀(Large/Medium/Small)의 부모 아래에서 활성화된 파편과 맵에 배치된 염동력 오브젝트를 찾음
        // 풀 파편은 활성 목록에도 들어 있으므로 목록에서는 맵 배치 오브젝트만 골라 두 번 띄우지 않음
        // height, duration은 무작위 범위(x~y), 높이는 m, 시간은 초
        public static void LiftInRange(WallFragmentSpawner spawner, TelegraphShape shape, Pose pose, Vector2 height, Vector2 duration)
        {
            if (spawner != null)
            {
                foreach (Transform poolParent in spawner.FragmentPoolParents)
                {
                    if (poolParent == null) continue;
                    foreach (Rigidbody body in poolParent.GetComponentsInChildren<Rigidbody>())
                        Lift(body, shape, pose, height, duration);
                }
            }

            // 순회 중 오브젝트가 비활성화되어 목록에서 빠져도 건너뛰지 않도록 뒤에서부터 순회
            IReadOnlyList<PsychokinesisObject> activeObjects = PsychokinesisObject.ActiveObjects;
            for (int index = activeObjects.Count - 1; index >= 0; index--)
            {
                PsychokinesisObject placed = activeObjects[index];
                if (placed.IsPlacedInMap) Lift(placed.Body, shape, pose, height, duration);
            }
        }

        // 플레이어가 들고 있거나 보스가 띄워 둔(kinematic) 오브젝트는 건드리지 않음
        private static void Lift(Rigidbody body, TelegraphShape shape, Pose pose, Vector2 height, Vector2 duration)
        {
            if (body.isKinematic || shape.Contains(pose, body.position) == false) return;
            if (body.TryGetComponent(out PsychokinesisObject psychokinesisObject) && psychokinesisObject.IsHeld) return;

            body.isKinematic = true;
            body.DOMoveY(body.position.y + Random.Range(height.x, height.y), Random.Range(duration.x, duration.y))
                .SetEase(Ease.OutQuad)
                .SetLink(body.gameObject)
                .OnKill(() => body.isKinematic = false);
        }
    }
}
