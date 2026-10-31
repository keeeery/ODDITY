using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 무력화 수치(BossGroggy)가 0이 되면 지금 무엇을 하든 즉시 끊고 들어오는 그로기 상태, 보스가 바닥에 쓰러졌다가 일어난다.
    // 전용 클립이 없어 Death 클립을 Motion Time(groggyTime)으로 구간만 골라 재생한다.
    // - 무너짐: 히트스톱·윤곽 번쩍임·카메라 흔들림으로 무너지는 순간을 강조
    // - 쓰러짐: 클립 앞쪽의 뒷발로 일어서는 동작은 건너뛰고 쓰러지는 구간부터 재생, 땅에 닿을 무렵 몸이 닿은 자리에서 흙먼지와 흔들림
    // - 누움: 팔다리까지 바닥에 내려앉은 자세로 멈추고 BossDizzy Slump 겹(거친 숨에 맞춘 들썩임·떨림)을 덧붙임
    // - 일어남: 클립을 거꾸로 감아 몸을 일으킨 뒤 Idle로 블렌드
    // 클립 구간은 보스마다 다르므로 인스펙터 값으로 맞춘다.
    // 끝나면 이전 상태로 돌아가지 않고 상태머신을 처음부터 다시 시작하며, 무력화 수치를 다시 채운다.
    public class BossGroggyState : BossStateBase
    {
        private enum GroggyPhase
        {
            Fall,
            Lie,
            GetUp,
        }

        [Title("Duration")]
        [Tooltip("무너진 순간부터 일어나기를 마칠 때까지의 시간(초)")]
        [SerializeField, Min(0.5f)] private float duration = 6f;
        [Tooltip("지금 재생 중인 동작에서 쓰러지는 동작으로 넘어가는 블렌드 시간(초)")]
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.25f;

        [Title("Fall")]
        [Tooltip("쓰러지기 시작하는 Death 클립 위치(0~1), 뒷발로 일어서는 동작이 끝나고 무너지기 시작하는 지점")]
        [SerializeField, Range(0f, 1f)] private float fallStartTime = 0.58f;
        [Tooltip("바닥에 다 쓰러져 팔다리까지 가라앉은 Death 클립 위치(0~1), 덜 가라앉은 지점이면 손발이 공중에 뜬 채로 멈춘다.")]
        [SerializeField, Range(0f, 1f)] private float lieTime = 0.78f;
        [Tooltip("쓰러지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.05f)] private float fallDuration = 0.5f;
        [Tooltip("쓰러지는 동안 이 진행도(0~1)를 넘으면 땅에 닿은 것으로 보고 흙먼지와 흔들림을 일으킨다.")]
        [SerializeField, Range(0f, 1f)] private float impactAt = 0.85f;
        [Tooltip("땅에 닿을 때 카메라 흔들림 세기(0~1)")]
        [SerializeField, Range(0f, 1f)] private float impactShakeTrauma = 0.3f;
        [Tooltip("땅에 닿을 때 흙먼지·파편 수 배율, 충격파는 그 절반만큼 넓어짐")]
        [SerializeField, Min(0f)] private float impactDustAmount = 1f;

        [Title("Get Up")]
        [Tooltip("일어나는 데 걸리는 시간(초), duration 안에 포함")]
        [SerializeField, Min(0.05f)] private float getUpDuration = 1.6f;
        [Tooltip("거꾸로 감기를 멈추고 Idle로 블렌드하기 시작할 Death 클립 위치(0~1), 뒷발로 일어서기 전 서 있는 자세")]
        [SerializeField, Range(0f, 1f)] private float getUpEndTime = 0.2f;

        [Title("Break")]
        [Tooltip("무너지는 순간 월드 전체를 멈추는 시간(실제 초)")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.12f;
        [Tooltip("무너지는 순간 윤곽이 번쩍이는 시간(초)")]
        [SerializeField, Min(0f)] private float flashDuration = 0.3f;
        [Tooltip("무너지는 순간 카메라 흔들림 세기(0~1)와 사라지는 시간(초)")]
        [SerializeField, Range(0f, 1f)] private float shakeTrauma = 0.45f;
        [SerializeField, Min(0f)] private float shakeFadeTime = 0.5f;

        [Title("States")]
        [ShowInInspector, ReadOnly] private GroggyPhase phase;

        private BossGroggy groggy;
        private BossDizzy dizzy;
        private BossFootDust footDust;
        private BossLyingGroundFit groundFit;
        private HitFlash flash;

        private float enterTime;
        private float phaseStartTime;
        private bool hasImpacted;
        private bool isSlumping;

        public override void Initialize(FantasyGarden.FSM.MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);

            groggy = root.GetComponent<BossGroggy>();
            dizzy = root.GetComponent<BossDizzy>(); // 없으면 들썩임·떨림 없이 동작만 재생
            footDust = root.GetComponent<BossFootDust>();
            groundFit = root.GetComponent<BossLyingGroundFit>(); // 없으면 서 있던 바닥 높이에 그대로 누움
            flash = HitFlash.GetOrAdd(root.gameObject);

            // 상태는 꺼진 채로 기다리므로 Initialize에서 구독
            if (groggy != null) groggy.Broken += HandleBroken;
            else Debug.LogWarning($"[{nameof(BossGroggyState)}] {root.name}에 BossGroggy가 없어 무력화되지 않습니다.", this);
        }

        private void OnDestroy()
        {
            if (groggy != null) groggy.Broken -= HandleBroken;
        }

        protected override void OnEnter()
        {
            enterTime = Clock.LocalTime;
            hasImpacted = false;

            // 끊긴 상태가 켜 둔 트리거·플래그가 회복 뒤에 뒤늦게 발동하지 않도록 모두 지움
            ResetAnimatorParameters();
            Animator.applyRootMotion = false;
            Animator.SetBool(BossAnimParams.IS_GROGGY, true);
            Animator.SetFloat(BossAnimParams.GROGGY_TIME, fallStartTime);
            Animator.CrossFadeInFixedTime(BossAnimParams.GROGGY_STATE, crossFadeDuration, 0);

            isSlumping = true;
            if (dizzy != null) dizzy.Slump.Begin();
            if (groundFit != null) groundFit.Begin();

            PlayBreakFeedback();
            StartPhase(GroggyPhase.Fall);
        }

        protected override void OnUpdate()
        {
            float elapsed = Clock.LocalTime - phaseStartTime;

            switch (phase)
            {
                case GroggyPhase.Fall:
                    UpdateFall(elapsed);
                    break;

                case GroggyPhase.Lie:
                    UpdateLie();
                    break;

                case GroggyPhase.GetUp:
                    UpdateGetUp(elapsed);
                    break;
            }
        }

        // 일어나기 전에 끊겨도(죽음 등) 그로기 동작·들썩임이 남지 않도록 정리
        protected override void OnExit()
        {
            EndSlump();
            if (groundFit != null) groundFit.End();
            Animator.SetBool(BossAnimParams.IS_GROGGY, false);
            if (groggy != null) groggy.Restore();
        }

        // 지금 상태와 무관하게 즉시 끊음, 예약해 둔 다음 상태도 무시
        // 상태 맵은 실제 타입으로 찾으므로 하위 클래스(휴머노이드 그로기 등)도 찾히도록 GetType 사용
        private void HandleBroken()
        {
            StateMachine.ClearReservedState();
            StateMachine.ChangeState(GetType(), true);
        }

        // 쓰러지는 동안의 자세, 기본은 Death 클립을 Motion Time으로 감음. 전용 클립이 있는 보스는 재정의
        protected virtual void ApplyFallPose(float progress)
            => Animator.SetFloat(BossAnimParams.GROGGY_TIME, Mathf.Lerp(fallStartTime, lieTime, progress));

        // 일어나는 동안의 자세(progress는 이징이 걸린 0~1), 기본은 Death 클립을 거꾸로 감음
        protected virtual void ApplyGetUpPose(float progress)
            => Animator.SetFloat(BossAnimParams.GROGGY_TIME, Mathf.Lerp(lieTime, getUpEndTime, progress));

        #region PHASE_METHODS

        private void StartPhase(GroggyPhase next)
        {
            // 일어나는 동안 원래 바닥 높이로 내려옴
            if (next == GroggyPhase.GetUp && groundFit != null) groundFit.End();

            phase = next;
            phaseStartTime = Clock.LocalTime;
        }

        // 클립의 낙하 가속을 살리도록 시간은 고르게 진행
        private void UpdateFall(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / fallDuration);
            ApplyFallPose(progress);

            if (hasImpacted == false && progress >= impactAt) PlayImpact();
            if (progress >= 1f) StartPhase(GroggyPhase.Lie);
        }

        private void UpdateLie()
        {
            if (Clock.LocalTime < enterTime + duration - getUpDuration) return;

            EndSlump();
            StartPhase(GroggyPhase.GetUp);
        }

        // 클립을 거꾸로 감아 일으켜 세우고, 다 감으면 Idle로 블렌드하며 상태머신을 다시 시작
        private void UpdateGetUp(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / getUpDuration);
            float eased = progress * progress * (3f - 2f * progress);
            ApplyGetUpPose(eased);

            if (progress < 1f) return;
            StateMachine.Restart();
        }

        #endregion

        private void EndSlump()
        {
            if (isSlumping == false) return;
            isSlumping = false;
            if (dizzy != null) dizzy.Slump.End();
        }

        private void PlayBreakFeedback()
        {
            HitStop.Request(hitStopDuration);
            if (flash != null) flash.Play(flashDuration);
            ShakeCamera(shakeTrauma);
        }

        private void PlayImpact()
        {
            hasImpacted = true;
            if (footDust != null) footDust.PlayBodyLanding(impactDustAmount);
            ShakeCamera(impactShakeTrauma);
        }

        private void ShakeCamera(float trauma)
        {
            if (trauma <= 0f) return;

            Player.Player player = Player.Player.Instance;
            if (player != null && player.CameraController != null) player.CameraController.AddTrauma(trauma, shakeFadeTime);
        }
    }
}
