using System;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 체력(CombatEntity)이 0이 되면 지금 무엇을 하든 즉시 끊고 들어오는 사망 상태, 보스가 쓰러진 채로 다시 일어나지 않는다.
    // 그로기와 같은 Groggy 애니메이터 상태(Death 클립 + Motion Time)를 쓰되, 클립을 거의 처음부터 끝까지 재생한다. 그로기 클립이 따로인 보스는 stateName으로 전용 상태를 쓴다.
    // - 마무리 일격: 히트스탑 뒤 월드 전체를 크게 느리게 했다가(SlowMotion) 서서히 원래 속도로 되돌리고, 윤곽 번쩍임·카메라 흔들림을 더함
    //   카메라 FOV도 순간 좁혔다가 슬로모션이 풀리는 동안 함께 되돌림(줌 펀치), 맞은 자리의 큰 충격·입자는 BossHitFeedback이 맡음
    // - 쓰러짐: 땅에 닿는 클립 위치를 지나면 그로기보다 많은 흙먼지와 큰 흔들림, 몸에서 염동 빛 입자(BossDeathResidue)가 피어오르고 카메라가 천천히 물러남
    //   잠시 뒤 같은 모습의 홀로그램(BossDeathShed)으로 바뀌어 제자리에서 흩어짐
    // - 누움: 다 쓰러진 자세로 멈추고 상태머신은 더 진행하지 않음, 홀로그램까지 다 사라지면 완료(Completed)를 알림(보상 등)
    // 클립 구간은 보스마다 다르므로 인스펙터 값으로 맞춘다. 사망한 보스는 CombatEntity가 바로 지우지 않도록 막는다.
    public class BossDeathState : BossStateBase
    {
        [Title("Animation")]
        [Tooltip("사망 동작을 재생할 애니메이터 상태 이름, Motion Time이 groggyTime이어야 한다. 그로기와 클립이 다른 보스(휴머노이드)는 Death")]
        [SerializeField] private string stateName = "Groggy";
        [Tooltip("지금 재생 중인 동작에서 사망 동작으로 넘어가는 블렌드 시간(초)")]
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.15f;
        [Tooltip("재생을 시작할 Death 클립 위치(0~1)")]
        [SerializeField, Range(0f, 1f)] private float startTime = 0f;
        [Tooltip("다 쓰러져 멈출 Death 클립 위치(0~1), 그로기의 Lie Time과 같은 자세")]
        [SerializeField, Range(0f, 1f)] private float endTime = 0.976f;
        [Tooltip("시작 위치에서 멈출 위치까지 재생하는 시간(초), 슬로모션 중에는 그만큼 늘어난다. Death 클립(약 2.83초) 구간 길이와 맞추면 원래 속도")]
        [SerializeField, Min(0.1f)] private float playDuration = 2.76f;
        [Tooltip("몸이 땅에 닿는 Death 클립 위치(0~1), 지나는 순간 흙먼지와 흔들림을 일으킨다.")]
        [SerializeField, Range(0f, 1f)] private float impactTime = 0.782f;

        [Title("Finishing Blow")]
        [Tooltip("죽는 순간 월드 전체를 멈추는 시간(실제 초)")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.15f;
        [Tooltip("히트스탑 뒤 느려지는 시간 배율(0~1)")]
        [SerializeField, Range(0.01f, 1f)] private float slowTimeScale = 0.1f;
        [Tooltip("느린 배율로 머무는 시간(실제 초)")]
        [SerializeField, Min(0f)] private float slowHoldDuration = 0.85f;
        [Tooltip("원래 속도로 돌아오는 시간(실제 초)")]
        [SerializeField, Min(0f)] private float slowRecoverDuration = 1.5f;
        [Tooltip("월드가 돌아오는 동안 플레이어·카메라 회전이 돌아오는 배율(0~1), 보상창이 뜰 때까지 이 배율로 남는다. 1이면 월드와 함께 원래 속도로 돌아온다.")]
        [SerializeField, Range(0.01f, 1f)] private float playerSlowScale = 0.35f;
        [Tooltip("죽는 순간 윤곽이 번쩍이는 시간(초)")]
        [SerializeField, Min(0f)] private float flashDuration = 0.5f;
        [Tooltip("죽는 순간 카메라 흔들림 세기(0~1)")]
        [SerializeField, Range(0f, 1f)] private float shakeTrauma = 0.55f;
        [Tooltip("죽는 순간 카메라 흔들림이 사라지는 시간(초)")]
        [SerializeField, Min(0f)] private float shakeFadeTime = 0.6f;
        [Tooltip("죽는 순간 카메라 FOV를 좁히는 각도(도), 0이면 줌 펀치를 하지 않는다. 되돌리는 시간은 슬로모션에 맞춘다.")]
        [SerializeField, Range(0f, 30f)] private float zoomFov = 7f;
        [Tooltip("FOV를 좁히는 데 걸리는 시간(실제 초)")]
        [SerializeField, Min(0f)] private float zoomInDuration = 0.1f;

        [Title("Impact")]
        [Tooltip("땅에 닿을 때 흙먼지 양 배율, 그로기(1)보다 크게")]
        [SerializeField, Min(1f)] private float dustAmount = 1.5f;
        [Tooltip("땅에 닿을 때 카메라 흔들림 세기(0~1), 실제 세기는 trauma²라 강한 피격(0.45)보다 확실히 크게")]
        [SerializeField, Range(0f, 1f)] private float impactShakeTrauma = 0.8f;
        [Tooltip("땅에 닿을 때 흔들림이 사라지는 시간(초), 무거운 몸이 쓰러진 여진이 남도록 길게")]
        [SerializeField, Min(0f)] private float impactShakeFadeTime = 1f;

        [Title("Aftermath")]
        [Tooltip("땅에 닿은 뒤 홀로그램으로 바뀌기 시작할 때까지의 시간(초), 흙먼지가 먼저 퍼지도록")]
        [SerializeField, Min(0f)] private float shedDelay = 0.35f;
        [Tooltip("땅에 닿은 뒤 카메라가 더 물러나는 거리(m), 0이면 물러나지 않는다.")]
        [SerializeField, Min(0f)] private float pullBackDistance = 1.5f;
        [Tooltip("물러나는 데 걸리는 시간, 머무는 시간, 되돌아오는 시간(실제 초)")]
        [SerializeField, Min(0f)] private float pullBackDuration = 2.5f;
        [SerializeField, Min(0f)] private float pullBackHold = 2f;
        [SerializeField, Min(0f)] private float pullBackRelease = 1.5f;

        [Title("Sound")]
        [Tooltip("몸이 땅에 닿을 때 소리, 마무리 일격 소리는 BossHitFeedback이 맡는다.")]
        [SerializeField] private SoundData impactSound;
        [Tooltip("홀로그램으로 바뀌어 흩어지기 시작할 때 소리, 기본 클립은 BossDeathShed 기본 시간(나타남 0.2 → 머묾 0.6 → 사라짐 1.8초)에 맞춰 만들어 그 값을 바꾸면 클립도 다시 맞춰야 한다.")]
        [SerializeField] private SoundData shedSound;

        private CombatEntity entity;
        private BossFootDust footDust;
        private BossDeathResidue residue;
        private BossDeathShed shed;
        private BossLyingGroundFit groundFit;
        private HitFlash flash;

        private float enterTime;
        private bool hasImpacted;
        private bool hasShed;
        private float impactClockTime;

        public bool HasCompleted { get; private set; }
        public event Action Completed;

        public override void Initialize(FantasyGarden.FSM.MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);

            entity = root.GetComponent<CombatEntity>();
            footDust = root.GetComponent<BossFootDust>();
            residue = root.GetComponent<BossDeathResidue>(); // 없으면 빛 입자 없이 쓰러짐
            shed = root.GetComponent<BossDeathShed>(); // 없으면 홀로그램 없이 쓰러진 채로 남음
            groundFit = root.GetComponent<BossLyingGroundFit>(); // 없으면 서 있던 바닥 높이에 그대로 쓰러짐
            flash = HitFlash.GetOrAdd(root.gameObject);

            // 상태는 꺼진 채로 기다리므로 Initialize에서 구독
            if (entity != null)
            {
                entity.DestroyOnDeath = false;
                entity.Died += HandleDied;
            }
            else Debug.LogWarning($"[{nameof(BossDeathState)}] {root.name}에 CombatEntity가 없어 사망 연출을 하지 않습니다.", this);
        }

        private void OnDestroy()
        {
            if (entity != null) entity.Died -= HandleDied;
        }

        protected override void OnEnter()
        {
            enterTime = Clock.LocalTime;
            hasImpacted = false;
            hasShed = false;

            ResetAnimatorParameters();
            Animator.applyRootMotion = false;
            Animator.SetBool(BossAnimParams.IS_GROGGY, true);
            Animator.SetFloat(BossAnimParams.GROGGY_TIME, startTime);
            Animator.CrossFadeInFixedTime(stateName, crossFadeDuration, 0);
            if (groundFit != null) groundFit.Begin();

            PlayFinishingBlow();
        }

        protected override void OnUpdate()
        {
            float progress = Mathf.Clamp01((Clock.LocalTime - enterTime) / playDuration);
            float clipTime = Mathf.Lerp(startTime, endTime, progress);
            Animator.SetFloat(BossAnimParams.GROGGY_TIME, clipTime);

            if (hasImpacted == false && (clipTime >= impactTime || progress >= 1f)) PlayImpact();
            if (hasImpacted && hasShed == false && Clock.LocalTime - impactClockTime >= shedDelay) PlayShed();
            if (hasShed && HasCompleted == false && (shed == null || shed.IsPlaying == false)) Complete();
        }

        private void Complete()
        {
            HasCompleted = true;
            // 받는 쪽(보상창)이 없으면 느린 채로 남은 플레이어를 여기서 풀어 줌
            if (Completed != null) Completed.Invoke();
            else SlowMotion.Resume();
        }

        protected override void OnExit() { }

        // 지금 상태와 무관하게 즉시 끊음, 예약해 둔 다음 상태도 무시
        private void HandleDied()
        {
            StateMachine.ClearReservedState();
            root.ChangeBossState(typeof(BossDeathState), true);
        }

        private void PlayFinishingBlow()
        {
            HitStop.Request(hitStopDuration);
            // 월드는 커밋 때처럼 원래 속도로 돌아오고, 플레이어·카메라 회전은 같은 곡선으로 일부만 돌아와 보상창이 뜰 때까지 남김
            SlowMotion.Play(slowTimeScale, slowHoldDuration, slowRecoverDuration, playerSlowScale);
            if (flash != null) flash.Play(flashDuration);
            ShakeCamera(shakeTrauma, shakeFadeTime);
            PunchZoom();
        }

        // 히트스탑부터 느린 배율이 끝날 때까지 좁힌 채로 머물고, 슬로모션이 풀리는 동안 되돌림
        private void PunchZoom()
        {
            if (zoomFov <= 0f) return;

            Player.Player player = Player.Player.Instance;
            if (player == null || player.CameraController == null) return;

            float hold = Mathf.Max(0f, hitStopDuration + slowHoldDuration - zoomInDuration);
            player.CameraController.PunchFov(zoomFov, zoomInDuration, hold, slowRecoverDuration);
        }

        private void PlayImpact()
        {
            hasImpacted = true;
            impactClockTime = Clock.LocalTime;
            if (footDust != null) footDust.PlayBodyLanding(dustAmount);
            if (residue != null) residue.Play();
            impactSound.Play();
            ShakeCamera(impactShakeTrauma, impactShakeFadeTime);

            Player.Player player = Player.Player.Instance;
            if (pullBackDistance > 0f && player != null && player.CameraController != null)
                player.CameraController.PullBack(pullBackDistance, pullBackDuration, pullBackHold, pullBackRelease);
        }

        private void PlayShed()
        {
            hasShed = true;
            if (shed == null) return;

            shed.Play();
            shedSound.Play();
        }

        private void ShakeCamera(float trauma, float fadeTime)
        {
            if (trauma <= 0f) return;

            Player.Player player = Player.Player.Instance;
            if (player != null && player.CameraController != null) player.CameraController.AddTrauma(trauma, fadeTime);
        }
    }
}
