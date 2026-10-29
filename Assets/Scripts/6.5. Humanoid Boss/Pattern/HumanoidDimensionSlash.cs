using System.Collections;
using System.Collections.Generic;
using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Player;
using TeamOdd.TimeControl;
using TeamOdd.UI;
using UnityEngine;

namespace TeamOdd
{
    // 차원 가르기 (A15): 체력이 일정 비율 이하가 되면 한 번, 세상을 멈추고 플레이어 정면 멀리 나타나 천천히 걸어온 뒤
    // 검을 천천히 들어 잠깐 멈췄다가 빠르게 세로로 내려베고 화면을 가른 뒤 확정 피해
    // 다른 담당 코드를 고치지 않으려고 기존 기능을 씀
    // - 세상 정지: TimeFreezable.FreezeWorld(아주 작은 배율), 0이면 시간 정지 홀로그램 막이 덮이므로 / 보스만 SetWorldExempt
    // - 플레이어 정지: PlayerController.SetActionLocked (이동·구르기·염동력·스킬·마우스 시점 입력 차단)
    // - 회색·깨짐: ScreenGlitch(Desaturate, Glitch), 화면 가르기: HumanoidSlashScreen
    public class HumanoidDimensionSlash : BossMeleePattern
    {
        private static readonly int WALK_STATE = Animator.StringToHash("DimensionWalk");
        private static readonly int SLASH_STATE = Animator.StringToHash("DimensionSlash");
        private static readonly int IDLE_STATE = Animator.StringToHash("Idle");

        [Title("Condition")]
        [SerializeField, Range(0f, 1f)] private float hpRatio = 0.25f; // 보스 체력이 이 비율 이하가 되면 한 번만 발동

        [Title("World Freeze")]
        [SerializeField, Range(0.001f, 0.1f)] private float worldTimeScale = 0.01f;
        [SerializeField] private float grayDuration = 0.25f;

        // 화면이 다 회색이 된 뒤 사라졌다가 플레이어 정면 멀리 나타남
        [Title("Teleport")]
        [SerializeField] private float vanishTime = 0.3f; // 대시 출발 모션을 보여 준 뒤 사라지는 시점(발동부터 초), grayDuration 이상
        [SerializeField] private float appearTime = 0.5f;
        [SerializeField] private float appearDistance = 13f; // 플레이어 앞 나타나는 거리
        [SerializeField] private float minTeleportDistance = 3f;
        [SerializeField] private float bodyRadius = 1.5f;
        [SerializeField] private LayerMask blockMask; // 순간이동 자리를 막는 레이어(벽), 막히면 거리만 줄임
        [SerializeField] private LayerMask groundMask;

        // 플레이어를 바라보며 천천히 걸어와 strikeDistance에서 멈춤 (칼끝이 플레이어를 지나가는 거리)
        [Title("Approach")]
        [SerializeField] private float strikeDistance = 6f;
        [SerializeField] private float approachSpeed = 2.8f;
        [SerializeField] private float approachAnimSpeed = 0.7f; // 걷기 루프 재생 속도, 이동 속도에 맞춰 발이 미끄러지지 않게
        [SerializeField] private float maxApproachTime = 3f;
        [SerializeField] private float approachTurnSpeed = 180f;

        // 검을 천천히 들어 올려(DimensionSlash 앞부분을 느리게) raiseEndTime에서 잠깐 멈춘 뒤, 원래 속도로 내려벰
        [Title("Raise")]
        [SerializeField] private float raiseAnimSpeed = 0.25f;
        [SerializeField, Range(0f, 0.25f)] private float raiseEndTime = 0.22f; // DimensionSlash 정규화 시간, 베기(0.25) 직전
        [SerializeField] private Vector2 raiseHoldTime = new Vector2(0.5f, 0.75f);

        [Title("Split")]
        [SerializeField] private float cutLineDuration = 0.15f;
        [SerializeField] private float splitOffset = 40f; // 화면 높이 1080 기준 픽셀
        [SerializeField] private float splitDuration = 0.2f;
        [SerializeField] private float crackDuration = 0.4f;
        [SerializeField] private float holdAfterFreeze = 0.7f; // 베기 정지 프레임부터 깨질 때까지
        [SerializeField] private int screenSortingOrder = -50; // HUD 캔버스보다 아래

        [Title("Shatter")]
        [SerializeField] private float flashDuration = 0.3f;
        [SerializeField] private float glitchDuration = 0.3f;
        [SerializeField] private float shakeTrauma = 0.8f;
        [SerializeField] private float releaseFadeDuration = 0.3f;
        [SerializeField] private float idleCrossFade = 0.4f;

        // 원하는 피해 = 최대 체력 × damageRatio, 단 플레이어 체력이 최대 체력 × minHpRatio 아래로는 거의 안 내려가게 깎음
        // (CombatEntity가 최소 1을 주므로 이미 그 아래면 1, HP 1이면 사망)
        [Title("Damage")]
        [SerializeField, Range(0f, 1f)] private float damageRatio = 0.35f;
        [SerializeField, Range(0f, 1f)] private float minHpRatio = 0.2f;
        [SerializeField] private HitPush push = HitPush.Create(3f, 0.5f, 0.5f);

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private bool hasUsed;

        private HumanoidBossCombatEntity combatEntity;
        private HumanoidSlashScreen screen;
        private Renderer[] bodyRenderers;
        private readonly List<Renderer> hiddenRenderers = new();
        private bool isWorldFrozen;
        private bool wasActionLocked;
        private float savedAnimatorSpeed = 1f;
        private bool hasCut;
        private bool hasFrozenPose;

        public override bool CanUse => hasUsed == false && base.CanUse && IsHpLow && IsTargetReady;

        protected override bool UsesMeleeHits => false;

        private bool IsHpLow => combatEntity != null && combatEntity.HpRatio <= hpRatio;

        // 이전 공격에 맞아 날아가는 중이거나 구르는 중이면 다음 판단까지 미룸
        private bool IsTargetReady
        {
            get
            {
                PlayerController player = Player.Player.Instance.PlayerController;
                return player.IsHit == false && player.IsDead == false;
            }
        }

        private PlayerController PlayerController => Player.Player.Instance.PlayerController;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            combatEntity = root.GetComponent<HumanoidBossCombatEntity>();
            bodyRenderers = root.GetComponentsInChildren<Renderer>(true);
        }

        // 애니메이션 트리거(dimensionSlash)는 BossMeleePattern이 켜서 DimensionDash(대시 출발 모션)부터 재생
        protected override void OnPatternEnter()
        {
            hasUsed = true;
            hasCut = false;
            hasFrozenPose = false;

            if (screen == null) screen = HumanoidSlashScreen.Create(screenSortingOrder);

            Begin();
            StartCoroutine(Run());
        }

        // 끊기더라도(사망 등) 세상·플레이어·화면이 멈춘 채로 남지 않게 모두 되돌림
        protected override void OnPatternExit()
        {
            StopAllCoroutines();
            Release();

            screen.HideAll();
            ScreenGlitch.Desaturate = 0f;
            ScreenGlitch.Glitch = 0f;
            SetBodyVisible(true);
            Animator.speed = savedAnimatorSpeed;
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (name == "OnDimensionCut") hasCut = true;
            else if (name == "OnDimensionFreeze") hasFrozenPose = true;
        }

        private IEnumerator Run()
        {
            float startTime = Clock.LocalTime;

            while (Clock.LocalTime - startTime < vanishTime)
            {
                ScreenGlitch.Desaturate = Mathf.Clamp01((Clock.LocalTime - startTime) / grayDuration);
                yield return null;
            }

            SetBodyVisible(false);
            while (Clock.LocalTime - startTime < appearTime)
            {
                ScreenGlitch.Desaturate = Mathf.Clamp01((Clock.LocalTime - startTime) / grayDuration);
                yield return null;
            }

            ScreenGlitch.Desaturate = 1f;
            TeleportInFront();
            SetBodyVisible(true);

            yield return Approach();
            yield return RaiseSword();

            while (hasCut == false) yield return null;
            float cutX = GetCutViewportX();
            yield return FadeCutLine(cutX);

            while (hasFrozenPose == false) yield return null;
            Animator.speed = 0f;
            yield return SplitScreen(cutX);

            Shatter();
            yield return FadeOut();

            Animator.speed = savedAnimatorSpeed;
            Animator.CrossFadeInFixedTime(IDLE_STATE, idleCrossFade);
            yield return Clock.Wait(idleCrossFade);

            root.ChangeBossState(typeof(BossIdleState));
        }

        // 보스가 한순간도 느려지지 않도록 예외를 먼저 걸고 세상을 멈춤 (풀 때는 반대 순서)
        private void Begin()
        {
            Clock.SetWorldExempt(true);
            TimeFreezable.FreezeWorld(worldTimeScale);
            isWorldFrozen = true;

            wasActionLocked = PlayerController.IsActionLocked;
            PlayerController.SetActionLocked(true);

            savedAnimatorSpeed = Animator.speed;
            if (combatEntity != null) combatEntity.IsInvincible = true;
        }

        private void Release()
        {
            if (isWorldFrozen == false) return;

            isWorldFrozen = false;
            TimeFreezable.UnfreezeWorld(worldTimeScale);
            Clock.SetWorldExempt(false);
            PlayerController.SetActionLocked(wasActionLocked);
            if (combatEntity != null) combatEntity.IsInvincible = false;
        }

        // 카메라가 보는 쪽(플레이어 앞)에 나타나므로 카메라를 따로 돌리지 않아도 보스가 화면에 들어옴
        private void TeleportInFront()
        {
            Vector3 targetPosition = Target.position;
            Vector3 forward = GetViewForward();

            float distance = appearDistance;
            if (Physics.SphereCast(targetPosition + Vector3.up, bodyRadius, forward, out RaycastHit block, appearDistance, blockMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(minTeleportDistance, block.distance);

            Vector3 position = targetPosition + forward * distance;
            position.y = Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 20f, groundMask, QueryTriggerInteraction.Ignore)
                ? ground.point.y
                : targetPosition.y;

            Movement.Teleport(position, Quaternion.LookRotation(-forward));
        }

        // 벽에 막혀 이미 가까이 나타났으면 걷지 않고 바로 검을 듦
        private IEnumerator Approach()
        {
            if (root.DistanceToTarget <= strikeDistance) yield break;

            Animator.speed = approachAnimSpeed;
            Animator.CrossFadeInFixedTime(WALK_STATE, 0.15f);

            float startTime = Clock.LocalTime;
            while (root.DistanceToTarget > strikeDistance && Clock.LocalTime - startTime < maxApproachTime)
            {
                root.RotateYaw(root.SignedAngleToTarget, approachTurnSpeed * Clock.LocalDeltaTime);
                Movement.Move(root.transform.forward * (approachSpeed * Clock.LocalDeltaTime));
                yield return null;
            }
        }

        private IEnumerator RaiseSword()
        {
            Animator.speed = raiseAnimSpeed;
            Animator.CrossFadeInFixedTime(SLASH_STATE, 0.2f);

            while (GetSlashNormalizedTime() < raiseEndTime) yield return null;

            Animator.speed = 0f;
            yield return Clock.Wait(Random.Range(raiseHoldTime.x, raiseHoldTime.y));
            Animator.speed = savedAnimatorSpeed;
        }

        // 걷기에서 넘어오는 동안은 다음 상태(DimensionSlash)의 시간을 봄
        private float GetSlashNormalizedTime()
        {
            AnimatorStateInfo current = Animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == SLASH_STATE) return current.normalizedTime;

            AnimatorStateInfo next = Animator.GetNextAnimatorStateInfo(0);
            return next.shortNameHash == SLASH_STATE ? next.normalizedTime : 0f;
        }

        private Vector3 GetViewForward()
        {
            Camera view = Camera.main;
            Vector3 forward = view != null ? view.transform.forward : Target.position - root.transform.position;
            forward.y = 0f;

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : root.transform.forward;
        }

        // 수직 내려베기라 칼이 지나간 화면 x는 보스 몸 중심과 같음
        private float GetCutViewportX()
        {
            Camera view = Camera.main;
            if (view == null) return 0.5f;

            return view.WorldToViewportPoint(root.transform.position + Vector3.up * 2f).x;
        }

        private IEnumerator FadeCutLine(float cutX)
        {
            float startTime = Clock.LocalTime;

            while (Clock.LocalTime - startTime < cutLineDuration)
            {
                screen.SetCutLine(cutX, 1f - (Clock.LocalTime - startTime) / cutLineDuration);
                yield return null;
            }

            screen.SetCutLine(cutX, 0f);
        }

        private IEnumerator SplitScreen(float cutX)
        {
            Camera view = Camera.main;
            if (view != null) screen.Capture(view);
            screen.ShowSplit(cutX);

            float offset = splitOffset * Screen.height / 1080f;
            float startTime = Clock.LocalTime;

            while (Clock.LocalTime - startTime < holdAfterFreeze)
            {
                float elapsed = Clock.LocalTime - startTime;
                float split = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / splitDuration));
                screen.SetSplit(offset * split, Mathf.Clamp01(elapsed / crackDuration));
                yield return null;
            }
        }

        // 깨지는 순간 피해·넉다운을 주고, 세상·플레이어를 풀어 넉다운이 바로 보이게 함
        private void Shatter()
        {
            screen.HideSplit();
            screen.SetFlash(1f);
            ScreenGlitch.Glitch = 1f;

            CameraMove.CameraController cameraController = Player.Player.Instance.CameraController;
            if (cameraController != null) cameraController.AddTrauma(shakeTrauma, flashDuration);

            Release();
            HitTarget();
        }

        private IEnumerator FadeOut()
        {
            float startTime = Clock.LocalTime;
            float duration = Mathf.Max(flashDuration, glitchDuration, releaseFadeDuration);

            while (Clock.LocalTime - startTime < duration)
            {
                float elapsed = Clock.LocalTime - startTime;
                screen.SetFlash(1f - Mathf.Clamp01(elapsed / flashDuration));
                ScreenGlitch.Glitch = 1f - Mathf.Clamp01(elapsed / glitchDuration);
                ScreenGlitch.Desaturate = 1f - Mathf.Clamp01(elapsed / releaseFadeDuration);
                yield return null;
            }

            screen.SetFlash(0f);
            ScreenGlitch.Glitch = 0f;
            ScreenGlitch.Desaturate = 0f;
        }

        // 확정 피격: BossMeleePattern.ApplyHit의 무적 검사를 거치지 않고 반응과 피해를 직접 보냄
        private void HitTarget()
        {
            PlayerController player = PlayerController;
            if (player.IsRolling) player.EndRoll();

            Vector3 bossPosition = root.transform.position;
            player.ApplyHitReaction(HitReaction.Knockdown, push.GetDirection(root.transform, bossPosition, Target.position), push, bossPosition);
            SendDamage(GetDamage(), HitReaction.Knockdown, Target.position + Vector3.up);
        }

        private int GetDamage()
        {
            CombatEntity target = Target.GetComponentInParent<CombatEntity>();
            if (target == null) return 0;

            int maxHp = target.CurrentStats.MaxHp;
            int wanted = Mathf.RoundToInt(maxHp * damageRatio);
            int limit = target.Hp - Mathf.CeilToInt(maxHp * minHpRatio);
            return Mathf.Max(0, Mathf.Min(wanted, limit));
        }

        // 원래 꺼져 있던 렌더러(어지러움 별 등)는 숨겼다 보일 때 켜지 않음
        private void SetBodyVisible(bool isVisible)
        {
            if (isVisible == false)
            {
                hiddenRenderers.Clear();
                foreach (Renderer bodyRenderer in bodyRenderers)
                {
                    if (bodyRenderer == null || bodyRenderer.enabled == false) continue;

                    bodyRenderer.enabled = false;
                    hiddenRenderers.Add(bodyRenderer);
                }

                return;
            }

            foreach (Renderer bodyRenderer in hiddenRenderers)
                if (bodyRenderer != null) bodyRenderer.enabled = true;

            hiddenRenderers.Clear();
        }
    }
}
