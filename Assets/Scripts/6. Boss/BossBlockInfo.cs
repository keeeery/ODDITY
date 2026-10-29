using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스가 이동 중 막힌 순간의 정보, 벽 충돌 그로기 같은 반응을 판단할 때 씀
    public readonly struct BossBlockInfo
    {
        public Collider Collider { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float Speed { get; } // 막히기 직전 수평 속도
        public float ImpactAngle { get; } // 진행 방향과 벽 사이 각, 0이면 정면으로 부딪힘

        public BossBlockInfo(RaycastHit hit, Vector3 moveDir, float speed)
        {
            Collider = hit.collider;
            Point = hit.point;
            Normal = hit.normal;
            Speed = speed;

            // 구의 위·아래쪽으로 닿으면 법선이 기울어 있으므로 수평 성분만으로 각도를 잼
            Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            ImpactAngle = Vector3.Angle(moveDir, -flatNormal);
        }
    }
}
