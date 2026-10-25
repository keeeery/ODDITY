using UnityEngine;
using Sirenix.OdinInspector;

namespace TeamOdd.Boss
{
    // 측면의 대상 쪽으로 몸을 돌리며 내려찍는 패턴, 좌우는 사용 각도와 트리거만 다름
    public abstract class BossTurnSmash : BossMeleePattern
    {
        [Title("Turn")]
        [SerializeField] private float turnDuration = 0.75f; // 패턴 시작 ~ OnHit 사이 시간, 이 시간 동안 대상 쪽으로 회전
        [SerializeField] private float maxTurnSpeed = 240f;

        private float turnEndTime;

        protected override void OnPatternEnter()
        {
            turnEndTime = Time.time + turnDuration;
        }

        // 클립이 제자리 모션이라 회전은 직접 처리, 남은 시간 안에 대상 방향까지 다 돌도록 회전 속도를 맞춤
        protected override void Update()
        {
            base.Update();

            float remainTime = turnEndTime - Time.time;
            if (remainTime <= 0f) return;

            float angle = root.SignedAngleToTarget;
            float turnSpeed = Mathf.Min(Mathf.Abs(angle) / remainTime, maxTurnSpeed);

            root.RotateYaw(angle, turnSpeed * Time.deltaTime);
        }
    }
}
