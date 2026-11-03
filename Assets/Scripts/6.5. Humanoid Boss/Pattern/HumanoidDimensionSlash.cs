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
    // 검을 천천히 들어 잠깐 멈췄다가 빠르게 내려베고(세로 또는 사선) 화면을 가른 뒤 확정 피해
    // 다른 담당 코드를 고치지 않으려고 기존 기능을 씀
    // - 세상 정지: TimeFreezable.FreezeWorld(아주 작은 배율), 0이면 시간 정지 홀로그램 막이 덮이므로 / 보스만 SetWorldExempt
    // - 플레이어 감속: SlowMotion.AddPlayerOnly(아주 작은 배율)로 플레이어만 느리게 해 사실상 움직이지 못하게 함, 염동력·스킬은 SetSuperpowerLocked로 막음
    // - 순간이동: 사망 연출과 같은 홀로그램(HumanoidHologramWarp)으로 떠오르며 사라졌다가 플레이어 정면에서 내려오며 나타남
    // - 회색·깨짐: ScreenGlitch(Desaturate, Glitch), 화면 가르기: HumanoidSlashScreen
    public class HumanoidDimensionSlash : BossMeleePattern
    {
        private static readonly int WALK_STATE = Animator.StringToHash("DimensionWalk");
        private static readonly int SLASH_STATE = Animator.StringToHash("DimensionSlash");
        private static readonly int DIAGONAL_SLASH_STATE = Animator.StringToHash("DimensionSlashDiagonal");
        private static readonly int IDLE_STATE = Animator.StringToHash("Idle");

        [Title("Condition")]
        [SerializeField, Range(0f, 1f)] private float hpRatio = 0.25f; // 보스 체력이 이 비율 이하가 되면 한 번만 발동

        [Title("World Freeze")]
        [SerializeField, Range(0.001f, 0.1f)] private float worldTimeScale = 0.01f;
        [SerializeField, Range(0.001f, 0.2f)] private float playerTimeScale = 0.02f; // 플레이어만 이 배율로 느려져 이동·구르기가 거의 안 됨
        [SerializeField] private float grayDuration = 0.25f;

        // 화면이 다 회색이 된 뒤 홀로그램으로 떠오르며 사라졌다가 플레이어 정면 멀리 홀로그램으로 내려오며 나타남
        [Title("Teleport")]
        [SerializeField] private float vanishTime = 0.3f; // 대시 출발 모션을 보여 준 뒤 사라지기 시작하는 시점(발동부터 초)
        [SerializeField] private float appearTime = 0.5f; // 나타나기 시작하는 가장 이른 시점(발동부터 초), 사라짐이 늦게 끝나면 그 뒤
        [SerializeField] private float vanishIdleCrossFade = 0.3f; // 사라지기 전 대시 출발 모션에서 대기 자세로 돌아오는 시간
        [SerializeField] private HumanoidHologramWarp.Settings hologram = HumanoidHologramWarp.Settings.Default;
        [SerializeField] private float appearDistance = 13f; // 플레이어 앞 나타나는 거리
        [SerializeField] private float minTeleportDistance = 3f;
        [SerializeField] private float bodyRadius = 1.5f;
        [SerializeField] private LayerMask blockMask; // 순간이동 자리를 막는 레이어(벽), 막히면 거리만 줄임
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float navMeshSnapDistance = 10f; // 나타날 자리가 NavMesh 밖이면 이 거리 안의 가장 가까운 NavMesh 위로 들임
        [SerializeField] private float navMeshEdgePadding = 0.6f; // 들인 자리를 NavMesh 경계에서 이만큼 안쪽으로

        // 플레이어를 바라보며 천천히 걸어와 strikeDistance에서 멈춤 (칼끝이 플레이어를 지나가는 거리)
        [Title("Approach")]
        [SerializeField] private float strikeDistance = 6f;
        [SerializeField] private float approachSpeed = 2.8f;
        [SerializeField] private float approachAnimSpeed = 0.7f; // 걷기 루프 재생 속도, 이동 속도에 맞춰 발이 미끄러지지 않게
        [SerializeField] private float approachStartDelay = 0.15f; // 나타난 뒤 대기 자세로 잠깐 서 있다가 걷기 시작
        [SerializeField] private float approachBlendTime = 0.45f; // 대기에서 걷기로 섞이는 시간, 그동안 이동 속도도 0에서 함께 올림
        [SerializeField] private float maxApproachTime = 3f;
        [SerializeField] private float approachTurnSpeed = 180f;

        // 검을 천천히 들어 올려(베기 앞부분을 느리게) raiseEndTime에서 잠깐 멈춘 뒤, 원래 속도로 내려벰, 사선 베기면 diagonalRaiseEndTime
        [Title("Raise")]
        [SerializeField] private float raiseAnimSpeed = 0.25f;
        [SerializeField, Range(0f, 0.25f)] private float raiseEndTime = 0.22f; // DimensionSlash 정규화 시간, 베기(0.25) 직전
        [SerializeField] private Vector2 raiseHoldTime = new Vector2(0.5f, 0.75f);

        // 사선 베기: DimensionSlashDiagonal(Skill_A)의 화면 왼쪽 위에서 오른쪽 아래로 내려베는 부분을 써서 화면도 기울여 가름
        // 앞쪽의 낮게 휘두르는 동작은 건너뛰고 칼을 들기 시작하는 곳부터 재생, 끄면 원래 수직 내려베기로 돌아감
        [Title("Diagonal Slash")]
        [SerializeField] private bool useDiagonalSlash = true;
        [SerializeField] private float diagonalStartOffset = 0.4f; // 재생을 시작하는 클립 시간(초)
        [SerializeField, Range(0f, 1f)] private float diagonalRaiseEndTime = 0.355f; // 칼이 가장 높이 올라간 정규화 시간, 베기(0.385) 직전
        [SerializeField, Range(-80f, 80f)] private float diagonalCutAngle = 40f; // 가르는 선이 세로에서 기운 각도, 양수면 화면에서 위가 왼쪽(\)

        // 위에서 아래로 빠르게 그어진 흰 심이 번쩍이며 화면이 몇 px 어긋나고, 잦아들면 검은 틈(흰 테두리)으로 남았다가
        // 화면이 갈라지는 동안 틈이 끝까지 벌어짐
        [Title("Cut Line")]
        [SerializeField] private float cutDrawDuration = 0.06f;
        [SerializeField] private float cutLineDuration = 0.25f; // 번쩍임이 잦아드는 시간
        [SerializeField, Range(0f, 1f)] private float cutRiftOpen = 0.5f; // 잦아든 뒤 검은 틈이 벌어진 정도, 갈라지며 1까지
        [SerializeField] private float slipOffset = 4f; // 그어지는 순간 어긋나는 거리(화면 높이 1080 기준 픽셀), 갈라질 때 여기서 이어짐
        [SerializeField, Range(0f, 1f)] private float cutFlashAlpha = 0.25f;
        [SerializeField] private float cutShakeTrauma = 0.3f;

        [Title("Split")]
        [SerializeField] private float splitOffset = 40f; // 화면 높이 1080 기준 픽셀
        [SerializeField] private float splitPause = 0.2f; // 베기 정지 프레임에서 흔들림·떨림 없이 멈춰 있다가 한 번에 갈라짐
        [SerializeField] private float splitDuration = 0.2f;
        [SerializeField] private float crackDuration = 0.4f;
        [SerializeField] private float holdAfterFreeze = 0.7f; // 갈라지기 시작부터 깨질 때까지
        [SerializeField] private int screenSortingOrder = -50; // HUD 캔버스보다 아래

        [Title("Shatter")]
        [SerializeField] private float flashDuration = 0.3f;
        [SerializeField] private float glitchDuration = 0.3f;
        [SerializeField] private float shakeTrauma = 0.8f;
        [SerializeField] private float releaseFadeDuration = 0.3f;
        [SerializeField] private float idleCrossFade = 0.4f;
        [SerializeField] private SoundData shatterSound;
        [SerializeField] private float shatterSoundLead = 0.09f; // 클립 앞부분 무음만큼 깨지기 전에 미리 재생

        // 원하는 피해 = 최대 체력 × damageRatio, 단 플레이어 체력이 최대 체력 × minHpRatio 아래로는 거의 안 내려가게 깎음
        // (CombatEntity가 최소 1을 주므로 이미 그 아래면 1, HP 1이면 사망)
        [Title("Damage")]
        [SerializeField, Range(0f, 1f)] private float damageRatio = 0.35f;
        [SerializeField, Range(0f, 1f)] private float minHpRatio = 0.2f;
        [SerializeField] private HitPush push = HitPush.Create(3f, 0.5f, 0.5f);

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private bool hasUsed;

        private HumanoidBossCombatEntity combatEntity;
        private HumanoidCloneSummon cloneSummon;
        private HumanoidSlashScreen screen;
        private HumanoidHologramWarp warp;
        private Renderer[] bodyRenderers;
        private readonly List<Renderer> hiddenRenderers = new();
        private bool isWorldFrozen;
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

        private int SlashState => useDiagonalSlash ? DIAGONAL_SLASH_STATE : SLASH_STATE;

        private float RaiseEndTime => useDiagonalSlash ? diagonalRaiseEndTime : raiseEndTime;

        private float CutAngle => useDiagonalSlash ? diagonalCutAngle : 0f;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            combatEntity = root.GetComponent<HumanoidBossCombatEntity>();
            cloneSummon = root.GetComponentInChildren<HumanoidCloneSummon>(true);
            bodyRenderers = root.GetComponentsInChildren<Renderer>(true);
            warp = HumanoidHologramWarp.GetOrAdd(root.gameObject);
        }

        // 애니메이션 트리거(dimensionSlash)는 BossMeleePattern이 켜서 DimensionDash(대시 출발 모션)부터 재생
        protected override void OnPatternEnter()
        {
            hasUsed = true;
            hasCut = false;
            hasFrozenPose = false;

            if (screen == null) screen = HumanoidSlashScreen.Create(screenSortingOrder);

            // 세상이 멈춘 화면에 분신이 멈춘 채 남지 않도록 숨김, 깎인 체력은 그대로 두고 깨지는 순간 다시 나타남
            if (cloneSummon != null) cloneSummon.SuspendClone();

            Begin();
            StartCoroutine(Run());
        }

        // 끊기더라도(사망 등) 세상·플레이어·화면이 멈춘 채로 남지 않게 모두 되돌림
        protected override void OnPatternExit()
        {
            StopAllCoroutines();
            Release();
            if (cloneSummon != null) cloneSummon.ResumeClone();

            screen.HideAll();
            if (warp != null) warp.Clear();
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
            StartCoroutine(FadeToGray());

            while (Clock.LocalTime - startTime < vanishTime) yield return null;
            yield return Vanish();

            while (Clock.LocalTime - startTime < appearTime) yield return null;
            TeleportInFront();
            yield return Appear();

            yield return Approach();
            yield return RaiseSword();

            while (hasCut == false) yield return null;

            // 그어지는 순간부터 찍은 화면을 두 반쪽으로 덮어 어긋나게 보여 줌, 보스가 멈출 때까지는 매 프레임 다시 찍음
            CaptureView();
            screen.ShowSplit(GetCutViewportPoint(), CutAngle);
            yield return DrawCutLine();

            while (hasFrozenPose == false)
            {
                CaptureView();
                yield return null;
            }

            Animator.speed = 0f;
            CaptureView();
            yield return SplitScreen();

            Shatter();
            yield return FadeOut();

            Animator.speed = savedAnimatorSpeed;
            Animator.CrossFadeInFixedTime(IDLE_STATE, idleCrossFade);
            yield return Clock.Wait(idleCrossFade);

            root.ChangeBossState(typeof(BossIdleState));
        }

        private IEnumerator FadeToGray()
        {
            float startTime = Clock.LocalTime;
            while (Clock.LocalTime - startTime < grayDuration)
            {
                ScreenGlitch.Desaturate = Mathf.Clamp01((Clock.LocalTime - startTime) / grayDuration);
                yield return null;
            }

            ScreenGlitch.Desaturate = 1f;
        }

        // 대시 출발 모션에서 대기 자세로 돌아온 뒤 홀로그램으로 바뀌어 떠오르며 사라짐, 홀로그램을 못 만들면 바로 숨김
        // 덮이는 동안 원본이 움직여 홀로그램과 어긋나지 않도록 동작을 멈춤 (나타날 때 되돌림)
        private IEnumerator Vanish()
        {
            Animator.CrossFadeInFixedTime(IDLE_STATE, vanishIdleCrossFade);
            yield return Clock.Wait(vanishIdleCrossFade);

            Animator.speed = 0f;
            if (warp == null || warp.Bake() == false)
            {
                SetBodyVisible(false);
                yield break;
            }

            yield return warp.Vanish(hologram, () => SetBodyVisible(false));
        }

        // 대기 자세로 세운 채 위에서 내려오며 그려지고, 다 그려지면 원본으로 바뀌어 걷기 시작함
        // 숨긴 렌더러는 크기·자세를 구울 수 없으므로 같은 프레임에 잠깐 보이게 해 구움(화면에는 그려지지 않음)
        private IEnumerator Appear()
        {
            Animator.speed = savedAnimatorSpeed;
            Animator.Play(IDLE_STATE, 0, 0f);
            Animator.Update(0f);
            Animator.speed = 0f;

            bool hasBaked = false;
            if (warp != null)
            {
                SetBodyVisible(true);
                hasBaked = warp.Bake();
                SetBodyVisible(false);
            }

            if (hasBaked) yield return warp.Appear(hologram, ShowAfterAppear);
            else ShowAfterAppear();
        }

        private void ShowAfterAppear()
        {
            SetBodyVisible(true);
            Animator.speed = savedAnimatorSpeed;
        }

        // 보스가 한순간도 느려지지 않도록 예외를 먼저 걸고 세상을 멈춤 (풀 때는 반대 순서)
        // 플레이어는 조작을 막지 않고 플레이어만 아주 느리게 해 입력은 받되 거의 움직이지 못하게 함
        private void Begin()
        {
            Clock.SetWorldExempt(true);
            TimeFreezable.FreezeWorld(worldTimeScale);
            isWorldFrozen = true;

            SlowMotion.AddPlayerOnly(playerTimeScale);
            PlayerController.SetSuperpowerLocked(true);

            savedAnimatorSpeed = Animator.speed;
            if (combatEntity != null) combatEntity.IsInvincible = true;
        }

        private void Release()
        {
            if (isWorldFrozen == false) return;

            isWorldFrozen = false;
            TimeFreezable.UnfreezeWorld(worldTimeScale);
            Clock.SetWorldExempt(false);
            SlowMotion.RemovePlayerOnly(playerTimeScale);
            PlayerController.SetSuperpowerLocked(false);
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

            // 플레이어가 벽 없는 맵 가장자리에서 바깥을 보고 있으면 NavMesh 밖(맵 바깥)에 나타나므로 가장 가까운 NavMesh 위로 들임
            BossPathfinder pathfinder = root.Pathfinder;
            if (pathfinder != null && pathfinder.TryGetNearestOnNavMesh(position, navMeshSnapDistance, navMeshEdgePadding, out Vector3 navMeshPos))
                position = navMeshPos;

            Movement.Teleport(position, Quaternion.LookRotation(-forward));
        }

        // 벽에 막혀 이미 가까이 나타났으면 걷지 않고 바로 검을 듦
        private IEnumerator Approach()
        {
            if (root.DistanceToTarget <= strikeDistance) yield break;

            yield return Clock.Wait(approachStartDelay);

            // 대기에서 바로 걷기 속도로 나가면 발이 미끄러지므로, 걷기가 섞이는 동안 이동 속도를 0에서 함께 올림
            // 섞이는 시간도 재생 속도를 따르므로 실제 걸리는 시간에 맞춰 올림
            Animator.speed = approachAnimSpeed;
            Animator.CrossFadeInFixedTime(WALK_STATE, approachBlendTime);
            float rampDuration = approachBlendTime / Mathf.Max(0.01f, approachAnimSpeed);

            float startTime = Clock.LocalTime;
            while (root.DistanceToTarget > strikeDistance && Clock.LocalTime - startTime < maxApproachTime)
            {
                float ramp = rampDuration > 0f ? Mathf.SmoothStep(0f, 1f, (Clock.LocalTime - startTime) / rampDuration) : 1f;
                root.RotateYaw(root.SignedAngleToTarget, approachTurnSpeed * Clock.LocalDeltaTime);
                Movement.Move(root.transform.forward * (approachSpeed * ramp * Clock.LocalDeltaTime));
                yield return null;
            }
        }

        private IEnumerator RaiseSword()
        {
            Animator.speed = raiseAnimSpeed;
            Animator.CrossFadeInFixedTime(SlashState, 0.2f, 0, useDiagonalSlash ? diagonalStartOffset : 0f);

            while (GetSlashNormalizedTime() < RaiseEndTime) yield return null;

            Animator.speed = 0f;
            yield return Clock.Wait(Random.Range(raiseHoldTime.x, raiseHoldTime.y));
            Animator.speed = savedAnimatorSpeed;
        }

        // 걷기에서 넘어오는 동안은 다음 상태(베기)의 시간을 봄
        private float GetSlashNormalizedTime()
        {
            AnimatorStateInfo current = Animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == SlashState) return current.normalizedTime;

            AnimatorStateInfo next = Animator.GetNextAnimatorStateInfo(0);
            return next.shortNameHash == SlashState ? next.normalizedTime : 0f;
        }

        private Vector3 GetViewForward()
        {
            Camera view = Camera.main;
            Vector3 forward = view != null ? view.transform.forward : Target.position - root.transform.position;
            forward.y = 0f;

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : root.transform.forward;
        }

        // 칼이 보스 몸 앞을 지나가므로 가르는 선은 몸 중심을 지남, 수직이면 높이는 상관없어 화면 가운데를 확대 기준으로 둠
        private Vector2 GetCutViewportPoint()
        {
            Camera view = Camera.main;
            if (view == null) return new Vector2(0.5f, 0.5f);

            Vector3 point = view.WorldToViewportPoint(root.transform.position + Vector3.up * 2f);
            return new Vector2(point.x, useDiagonalSlash ? point.y : 0.5f);
        }

        private void CaptureView()
        {
            Camera view = Camera.main;
            if (view != null) screen.Capture(view);
        }

        // 칼이 지나가는 순간 흰 심이 위에서 아래로 빠르게 그어지며 화면이 살짝 번쩍이고 흔들리면서 몇 px 어긋나고,
        // 번쩍임이 잦아들며 검은 틈이 드러남
        private IEnumerator DrawCutLine()
        {
            CameraMove.CameraController cameraController = Player.Player.Instance.CameraController;
            if (cameraController != null) cameraController.AddTrauma(cutShakeTrauma, cutDrawDuration + cutLineDuration);

            float slip = slipOffset * Screen.height / 1080f;
            float startTime = Clock.LocalTime;
            while (Clock.LocalTime - startTime < cutDrawDuration)
            {
                float draw = Mathf.Clamp01((Clock.LocalTime - startTime) / cutDrawDuration);
                float eased = 1f - (1f - draw) * (1f - draw);
                screen.SetCut(eased, 1f, 0f);
                screen.SetSplit(slip * eased, 0f);
                screen.SetFlash(cutFlashAlpha * draw);
                CaptureView();
                yield return null;
            }

            startTime = Clock.LocalTime;
            while (Clock.LocalTime - startTime < cutLineDuration)
            {
                float fade = Mathf.Clamp01((Clock.LocalTime - startTime) / cutLineDuration);
                float eased = 1f - (1f - fade) * (1f - fade);
                screen.SetCut(1f, 1f - eased, cutRiftOpen * eased);
                screen.SetSplit(slip, 0f);
                screen.SetFlash(cutFlashAlpha * (1f - eased));
                CaptureView();
                yield return null;
            }

            screen.SetCut(1f, 0f, cutRiftOpen);
            screen.SetFlash(0f);
        }

        // 벤 채로 잠깐 정적이 흐른 뒤 어긋남이 한 번에 크게 벌어지고, 찍은 화면이 그만큼 확대되어 위아래 끝이 비지 않음
        private IEnumerator SplitScreen()
        {
            float slip = slipOffset * Screen.height / 1080f;
            float offset = splitOffset * Screen.height / 1080f;

            screen.SetSplit(slip, 0f);
            screen.SetCut(1f, 0f, cutRiftOpen);
            yield return Clock.Wait(splitPause);

            bool hasPlayedShatter = false;
            float startTime = Clock.LocalTime;
            while (Clock.LocalTime - startTime < holdAfterFreeze)
            {
                float elapsed = Clock.LocalTime - startTime;
                if (hasPlayedShatter == false && elapsed >= holdAfterFreeze - shatterSoundLead)
                {
                    shatterSound.Play();
                    hasPlayedShatter = true;
                }

                float progress = Mathf.Clamp01(elapsed / splitDuration);
                float split = 1f - Mathf.Pow(1f - progress, 3f); // 정적을 깨고 단숨에 벌어지도록 ease-out
                screen.SetSplit(Mathf.Lerp(slip, offset, split), Mathf.Clamp01(elapsed / crackDuration));

                // 틈이 끝까지 벌어지고, 깨지기 직전까지 테두리가 조금씩 떨림
                float flicker = 1f + 0.1f * Mathf.Sin(elapsed * 60f);
                screen.SetCut(1f, 0f, Mathf.Lerp(cutRiftOpen, 1f, split) * flicker);
                yield return null;
            }

            // 프레임이 길어 미리 재생할 구간을 건너뛰었으면 깨지는 순간에 바로 재생
            if (hasPlayedShatter == false) shatterSound.Play();
        }

        // 깨지는 순간 피해·넉다운을 주고, 세상·플레이어를 풀어 넉다운이 바로 보이게 함, 숨겼던 분신도 이때 다시 나타남
        private void Shatter()
        {
            screen.HideSplit();
            screen.SetFlash(1f);
            ScreenGlitch.Glitch = 1f;

            CameraMove.CameraController cameraController = Player.Player.Instance.CameraController;
            if (cameraController != null) cameraController.AddTrauma(shakeTrauma, flashDuration);

            Release();
            if (cloneSummon != null) cloneSummon.ResumeClone();
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

        // 확정 피격: 구르기·회피 칩으로 피할 수 없는 피해(unavoidable)로 보냄, 넉다운 처리는 피격 대상이 담당
        private void HitTarget()
        {
            PlayerController player = PlayerController;
            if (player.IsRolling) player.EndRoll();

            ApplyHit(root.transform.position, GetDamage(), HitReaction.Knockdown, push, Target.position + Vector3.up, unavoidable: true);
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
