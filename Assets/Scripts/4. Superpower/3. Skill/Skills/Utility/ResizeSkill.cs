using DG.Tweening;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 들고 있는 염동력 오브젝트, 없으면 조준 중인 오브젝트의 크기와 질량에 (Multiplier)를 곱한다. 매개변수 1개: Multiplier(float)
    // - 대상이 없거나, 커진 질량이 들 수 있는 최대 질량(PsychoMaxMass)을 넘으면 쓰지 않고 쿨타임도 돌지 않는다
    // - 성공: 움찔움찔하다가 뿅 커짐 / 실패(더 커지면 들 수 없음): 움찔움찔하다가 부르르 부풀려다 피식 쪼그라듦
    public class ResizeSkill : SuperpowerSkill
    {
        // 연출 수치
        private const int TwitchCount = 3;              // 커지기 전에 움찔거리는 횟수
        private const float TwitchDuration = 0.12f;     // 움찔 한 번에 걸리는 시간(초)
        private const float TwitchSqueeze = 0.1f;       // 마지막 움찔에서 줄어드는 비율, 앞의 움찔은 이보다 약하게
        private const float PopDuration = 0.25f;        // 뿅 커지는(실패면 피식 쪼그라드는) 데 걸리는 시간(초)
        private const float PopOvershoot = 0.15f;       // 목표 크기보다 더 커졌다가 돌아오는 비율
        private const float RefuseStrain = 0.08f;       // 실패할 때 부풀어 오른 채로 떠는 크기 비율
        private const int RefuseShakeCount = 4;         // 실패할 때 떠는 횟수
        private const float RefuseShakeDuration = 0.05f;

        private readonly float multiplier;

        private PsychokinesisObject target; // CanExecute에서 고른 대상, Execute에서 씀

        public ResizeSkill(string name, float cooldown, float multiplier) : base(name, cooldown)
        {
            this.multiplier = multiplier;
        }

        public override string Describe() => $"크기 · 무게 ×{multiplier}";

        protected override bool CanExecute(SkillContext context)
        {
            target = null;
            if (context.Caster.TryGetComponent(out PsychokinesisController controller))
                target = controller.HeldObject != null ? controller.HeldObject : controller.FocusedObject;

            if (target == null)
            {
                Debug.Log($"[{Name}] 들고 있거나 조준한 염동력 오브젝트가 없습니다.");
                return false;
            }

            // 앞 연출이 남아 있으면 끝 크기로 마무리
            target.transform.DOComplete();

            // 커진 뒤 들 수 없게 되면 키우지 않고, 대상이 커지려다 실패하는 연출로 알려 줌
            int maxMass = context.Owner.CurrentStats.Get(StatType.PsychoMaxMass);
            if (target.Body.mass * multiplier > maxMass)
            {
                Debug.Log($"[{Name}] 더 커지면 들 수 없습니다. (질량 {target.Body.mass:F1} × {multiplier} > {maxMass})");
                PlayRefuse(target);
                target = null;
                return false;
            }

            return true;
        }

        protected override void Execute(SkillContext context)
        {
            PlayResize(target);
            target = null;
        }

        // 실제 크기·질량은 바로 바꾸고, 보이는 크기만 처음 크기에서 연출로 키움
        private void PlayResize(PsychokinesisObject resized)
        {
            Transform resizedTransform = resized.transform;
            Vector3 startScale = resizedTransform.localScale;
            resized.Resize(multiplier);
            Vector3 endScale = resizedTransform.localScale;
            resizedTransform.localScale = startScale;

            Sequence sequence = CreateSequence(resized);
            AppendTwitch(sequence, resizedTransform, startScale);

            // 뿅: 목표보다 조금 더 커졌다가 목표 크기로 돌아옴
            sequence.Append(resizedTransform.DOScale(endScale * (1f + PopOvershoot), PopDuration * 0.4f).SetEase(Ease.OutQuad));
            sequence.Append(resizedTransform.DOScale(endScale, PopDuration * 0.6f).SetEase(Ease.InOutQuad));
        }

        private static void PlayRefuse(PsychokinesisObject refused)
        {
            Transform refusedTransform = refused.transform;
            Vector3 startScale = refusedTransform.localScale;
            Vector3 strainedScale = startScale * (1f + RefuseStrain);
            Vector3 shakenScale = startScale * (1f + RefuseStrain * 0.5f);
            Vector3 deflatedScale = startScale * (1f - TwitchSqueeze);

            Sequence sequence = CreateSequence(refused);
            AppendTwitch(sequence, refusedTransform, startScale);

            // 부르르: 살짝 부풀어 오른 채로 떨다가
            sequence.Append(refusedTransform.DOScale(strainedScale, RefuseShakeDuration));
            for (int i = 0; i < RefuseShakeCount; i++)
            {
                sequence.Append(refusedTransform.DOScale(shakenScale, RefuseShakeDuration * 0.5f));
                sequence.Append(refusedTransform.DOScale(strainedScale, RefuseShakeDuration * 0.5f));
            }

            // 피식: 원래보다 쪼그라들었다가 원래 크기로 돌아옴
            sequence.Append(refusedTransform.DOScale(deflatedScale, PopDuration * 0.4f).SetEase(Ease.OutQuad));
            sequence.Append(refusedTransform.DOScale(startScale, PopDuration * 0.6f).SetEase(Ease.InOutQuad));

            // 연출 동안 외곽선을 "잡을 수 없음" 색(빨간색)으로 보여 줌
            refused.FlashUnavailable(sequence.Duration());
        }

        // 움찔: 살짝 줄었다 돌아오기를 반복하며 점점 세게
        private static void AppendTwitch(Sequence sequence, Transform scaled, Vector3 startScale)
        {
            for (int i = 0; i < TwitchCount; i++)
            {
                float squeeze = TwitchSqueeze * (i + 1) / TwitchCount;
                sequence.Append(scaled.DOScale(startScale * (1f - squeeze), TwitchDuration * 0.5f));
                sequence.Append(scaled.DOScale(startScale, TwitchDuration * 0.5f));
            }
        }

        // - SetTarget: 다음 연출이 DOComplete로 이 연출을 찾아 마무리할 수 있게 함
        // - SetLink: 연출 도중 대상이 꺼지면(파편화, 풀로 돌아감) 연출도 멈춤
        // - OnUpdate: 잠든 Rigidbody는 크기가 바뀌어도 바닥과 겹친 채로 있으므로, 매 프레임 0의 힘으로 밀어 물리 계산을 다시 하게 함
        //   (body를 기억해야 해서 람다로 넘김)
        private static Sequence CreateSequence(PsychokinesisObject scaled)
        {
            Rigidbody body = scaled.Body;
            return DOTween.Sequence()
                .SetTarget(scaled.transform)
                .SetLink(scaled.gameObject, LinkBehaviour.KillOnDisable)
                .OnUpdate(() => body.AddForce(Vector3.zero, ForceMode.VelocityChange));
        }
    }
}
