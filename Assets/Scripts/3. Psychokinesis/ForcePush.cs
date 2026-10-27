using DG.Tweening;
using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    // 염동력 기본 공격(염동파)에 맞은 물체를 물리 힘 대신 트윈으로 밀어냄
    // 가벼운 물체는 kinematic으로 밀다가 끝에서 남은 속도를 물리에 넘겨 미끄러지거나 떨어지게 하고, 무거운 물체는 제자리에서 흔들리기만 한다.
    // kinematic은 벽에 막히지 않으므로 미리 SweepTest로 막히는 곳까지만 밀고, 시간 정지 중에는 TimeFreezable이 트윈을 함께 멈춘다.
    public static class ForcePush
    {
        // 끝 기울기가 0이 아니어서 다 밀었을 때의 속도를 그대로 이어받을 수 있는 감속 곡선
        private const float EndSlope = 0.6f;
        private const float SkinWidth = 0.05f;
        private const float MinDistance = 0.08f;
        // 바닥에 걸려 바로 막히지 않도록 살짝 들어 올림
        private const float LiftRatio = 0.15f;
        private const float TumbleSpeed = 5f;

        private static readonly AnimationCurve PushCurve = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, EndSlope, 0f));

        // 밀거나 흔들었으면 true, 이미 다른 연출로 움직이는 중(kinematic·트윈)이거나 들고 있는 물체면 건드리지 않음
        public static bool Push(Rigidbody body, Vector3 direction, float distance, float duration, float heavyMass, float wobbleAngle)
        {
            if (body == null || body.isKinematic || DOTween.IsTweening(body) || DOTween.IsTweening(body.transform)) return false;
            if (body.TryGetComponent(out PsychokinesisObject psychokinesisObject) && psychokinesisObject.IsHeld) return false;

            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (body.mass >= heavyMass || flat.sqrMagnitude < 0.0001f)
            {
                Wobble(body, direction, duration, wobbleAngle);
                return true;
            }

            float lightness = 1f - 0.6f * (body.mass / heavyMass);
            Vector3 pushDirection = (flat.normalized + Vector3.up * LiftRatio).normalized;
            distance *= lightness;

            if (body.SweepTest(pushDirection, out RaycastHit hit, distance, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0f, hit.distance - SkinWidth);

            if (distance < MinDistance)
            {
                Wobble(body, direction, duration, wobbleAngle * lightness);
                return true;
            }

            Vector3 exitVelocity = pushDirection * (distance / duration * EndSlope);
            Vector3 tumble = Vector3.Cross(Vector3.up, pushDirection) * (TumbleSpeed * lightness);

            body.isKinematic = true;
            body.DOMove(body.position + pushDirection * distance, duration)
                .SetEase(PushCurve)
                .SetUpdate(UpdateType.Fixed)
                .SetLink(body.gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() =>
                {
                    body.isKinematic = false;
                    body.linearVelocity = exitVelocity;
                    body.angularVelocity = tumble;
                })
                .OnKill(() =>
                {
                    if (body != null) body.isKinematic = false;
                });
            return true;
        }

        // 밀리지 않는 물체는 맞은 방향으로 한 번 기울었다가 제자리로 돌아옴
        private static void Wobble(Rigidbody body, Vector3 direction, float duration, float angle)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, direction);
            if (axis.sqrMagnitude < 0.0001f) axis = body.transform.right;

            Transform target = body.transform;
            body.isKinematic = true;
            target.DOPunchRotation(target.InverseTransformDirection(axis.normalized) * angle, duration * 1.5f, 6, 0.6f)
                .SetLink(body.gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() =>
                {
                    if (body != null) body.isKinematic = false;
                });
        }
    }
}
