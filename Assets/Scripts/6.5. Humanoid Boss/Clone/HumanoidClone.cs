using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd
{
    // 분신(본체의 열화판): 씬에 꺼 둔 원본을 HumanoidCloneSummon이 복제해 켜고, 죽거나 물러나면 홀로그램이 흩어지며 사라진 뒤 삭제
    // 매번 새로 복제하므로 체력·상태·쿨다운을 되돌릴 필요가 없음 (CombatEntity는 죽은 뒤 체력을 되돌릴 수 없음)
    public class HumanoidClone : MonoBehaviour
    {
        private static readonly int ALPHA = Shader.PropertyToID("_Alpha");
        private static readonly int GLITCH = Shader.PropertyToID("_RandomGlitchAmount");
        private static readonly int IDLE_STATE = Animator.StringToHash("Idle");

        [Title("References")]
        [SerializeField] private BossMonster boss;
        [SerializeField] private HumanoidBossCombatEntity combatEntity;

        // 등장하는 동안 무적, HumanoidCloneAppearState가 이 시간만큼 서서 기다린 뒤 Idle로
        [Title("Appear")]
        [SerializeField] private float appearDuration = 0.8f;
        [SerializeField, Range(0f, 1f)] private float appearGlitch = 1f; // 등장 시작 순간의 흔들림, 원래 값으로 줄어듦

        // 본체 예고와 구분되도록 홀로그램(Hologram Earth 1, 청보라 HDR) 색조에 맞춘 파란색
        [Title("Telegraph")]
        [SerializeField] private Color telegraphColor = new(0.25f, 0.4f, 1f, 1f);

        [Title("Vanish")]
        [SerializeField] private float vanishDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float vanishGlitch = 1f;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] public bool IsVanishing { get; private set; }
        [ShowInInspector, ReadOnly] public bool IsSuspended { get; private set; }

        // 사라지는 연출이 끝나 삭제되기 직전 한 번
        public event Action<HumanoidClone> Vanished;

        // 맵에 나와 있는 분신 (한 번에 하나), 꺼 둔 원본·대기 복제본은 켜지기 전이라 들어오지 않음, 보스 HUD가 체력바에 씀
        public static HumanoidClone Active { get; private set; }

        public float AppearDuration => appearDuration;
        public CombatEntity CombatEntity => combatEntity;

        private readonly List<Renderer> holograms = new();
        private readonly List<float> baseAlphas = new();
        private readonly List<float> baseGlitches = new();
        private MaterialPropertyBlock block;
        private bool hasFinished;

        // 공유 머티리얼 에셋을 바꾸지 않도록 렌더러별 프로퍼티 블록으로 조절, 켜진 첫 프레임에 보이지 않게 여기서 투명하게 둠
        private void Awake()
        {
            block = new MaterialPropertyBlock();
            combatEntity.DestroyOnDeath = false;
            combatEntity.Died += HandleDied;
            boss.TelegraphColor = telegraphColor;

            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                Material material = target.sharedMaterial;
                if (material == null || material.HasProperty(ALPHA) == false) continue;

                holograms.Add(target);
                baseAlphas.Add(material.GetFloat(ALPHA));
                baseGlitches.Add(material.HasProperty(GLITCH) ? material.GetFloat(GLITCH) : 0f);
            }

            SetHologram(0f, appearGlitch);
        }

        private void OnEnable()
        {
            if (hasFinished == false) Active = this;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        // 분신은 플레이어를 막거나 밀어내지 않고 통과시킴, 뼈 히트박스가 Awake에서 붙은 뒤라 Start에서 등록
        private void Start()
        {
            Player.Player player = Player.Player.Instance;
            if (player != null && player.PlayerController != null) player.PlayerController.AddPassThroughBody(transform);

            StartCoroutine(Appear());
        }

        private void OnDestroy()
        {
            if (combatEntity != null) combatEntity.Died -= HandleDied;

            // 씬을 내리는 중이면 플레이어가 먼저 파괴됐을 수 있다. 살아 있을 때만 정리한다
            if (Player.Player.IsInitialized == false) return;
            Player.Player player = Player.Player.Instance;
            if (player != null && player.PlayerController != null) player.PlayerController.RemovePassThroughBody(transform);
        }

        // instant: 기다릴 수 없을 때 연출 없이 바로 삭제, 숨겨 둔 동안은 흩어지며 다시 보이지 않게 항상 바로 삭제
        public void Vanish(bool instant = false)
        {
            if (hasFinished) return;

            if (instant || IsSuspended)
            {
                StopAllCoroutines();
                Finish();
                return;
            }

            if (IsVanishing) return;

            IsVanishing = true;
            StopAllCoroutines();
            StopActing();
            StartCoroutine(FadeOut());
        }

        // 세상이 멈추는 연출(A15) 동안 체력·쿨다운은 그대로 둔 채 숨김, 멈추는 건 그 연출의 세상 감속에 맡김
        // 하던 공격의 예고·판정이 남지 않도록 상태를 꺼서 정리(OnExit)하고, 보이지도 맞지도 않게 함
        public void Suspend()
        {
            if (hasFinished || IsSuspended) return;

            if (IsVanishing)
            {
                Vanish(true);
                return;
            }

            IsSuspended = true;
            StopAllCoroutines();

            foreach (BossStateBase state in GetComponentsInChildren<BossStateBase>())
                state.gameObject.SetActive(false);

            combatEntity.IsInvincible = true;
            combatEntity.Collider.enabled = false;
            SetHologram(0f, 0f);
        }

        // 대기 자세로 세워 등장 연출부터 다시 시작, 등장 상태가 연출 동안 서 있다가 Idle로 넘김
        public void Resume()
        {
            if (hasFinished || IsSuspended == false) return;

            IsSuspended = false;
            combatEntity.Collider.enabled = true;
            if (boss.Animator.HasState(0, IDLE_STATE)) boss.Animator.Play(IDLE_STATE, 0, 0f);
            boss.ChangeBossState(typeof(HumanoidCloneAppearState), true);
            StartCoroutine(Appear());
        }

        private void HandleDied() => Vanish();

        private IEnumerator Appear()
        {
            combatEntity.IsInvincible = true;

            for (float elapsed = 0f; elapsed < appearDuration; elapsed += boss.Clock.LocalDeltaTime)
            {
                float progress = elapsed / appearDuration;
                SetHologram(progress, appearGlitch * (1f - progress));
                yield return null;
            }

            SetHologram(1f, 0f);
            combatEntity.IsInvincible = false;
        }

        private IEnumerator FadeOut()
        {
            for (float elapsed = 0f; elapsed < vanishDuration; elapsed += boss.Clock.LocalDeltaTime)
            {
                float progress = elapsed / vanishDuration;
                SetHologram(1f - progress, vanishGlitch * progress);
                yield return null;
            }

            Finish();
        }

        // 하던 공격의 예고가 남지 않도록 상태를 꺼서 정리(OnExit)하고, 멈춘 자세로 흩어지게 함
        private void StopActing()
        {
            combatEntity.IsInvincible = true;
            combatEntity.Collider.enabled = false;

            foreach (BossStateBase state in GetComponentsInChildren<BossStateBase>())
                state.gameObject.SetActive(false);

            boss.Animator.speed = 0f;
        }

        private void Finish()
        {
            hasFinished = true;
            Vanished?.Invoke(this);
            Destroy(gameObject);
        }

        // visibility: 원래 투명도에 곱함 / glitch: 원래 흔들림에서 1까지 더함
        private void SetHologram(float visibility, float glitch)
        {
            for (int index = 0; index < holograms.Count; index++)
            {
                holograms[index].GetPropertyBlock(block);
                block.SetFloat(ALPHA, baseAlphas[index] * visibility);
                block.SetFloat(GLITCH, Mathf.Lerp(baseGlitches[index], 1f, glitch));
                holograms[index].SetPropertyBlock(block);
            }
        }
    }
}
