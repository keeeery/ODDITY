using System.Collections.Generic;
using DG.Tweening;
using TeamOdd.Combat;
using TeamOdd.Superpower;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    // 화면과 아이콘 슬롯은 씬에 배치하고, 획득 결과만 채운다.
    // 열면 잠시 뒤 가운데 1px 선이 좌우로 뻗고 위아래로 열리며 화면이 켜지고, 짧게 지직거린 뒤 슬롯이 하나씩 번쩍이며 켜진다.
    // 연출은 실제 시간 기준이다. 사망 화면(PlayerDeathScreen)과 같은 부품을 세기만 다르게 쓴다.
    [DisallowMultipleComponent]
    public sealed class BossRewardPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RewardIconView[] icons;
        [SerializeField] private Button closeButton;
        [SerializeField] private TooltipView tooltip;

        [Header("Reveal")]
        [Tooltip("배경과 내용을 선에서 펼쳐 여는 마스크")]
        [SerializeField] private UILineReveal lineReveal;
        [Tooltip("열린 직후 짧게 지직거릴 배경(제목 색 분리 포함)")]
        [SerializeField] private UIGlitch glitch;
        [Tooltip("아이콘 슬롯들의 부모, 열린 뒤 슬롯을 하나씩 켠다.")]
        [SerializeField] private UIStaggerReveal slots;
        [Tooltip("열기를 부른 뒤 화면이 나타나기까지 기다리는 시간(초)")]
        [SerializeField, Min(0f)] private float revealDelay = 1f;
        [SerializeField, Min(0.01f)] private float glitchDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float glitchStrength = 0.35f;

        private UIManager uiManager;
        private Tween reveal;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            reveal?.Kill();
        }

        public void Open(IReadOnlyList<OwnedChip> rewards)
        {
            if (tooltip != null) tooltip.Hide();
            if (panelRoot == null || icons == null || closeButton == null)
            {
                Debug.LogError("[BossRewardPanel] 씬의 Panel Root, Icons, Close Button을 연결해 주세요.", this);
                if (SlowMotion.IsSlowed) SlowMotion.Resume();
                return;
            }

            for (int i = 0; i < icons.Length; i++)
                icons[i].Show(i < rewards.Count ? rewards[i] : null);

            if (rewards.Count > icons.Length)
                Debug.LogWarning($"[BossRewardPanel] 지급된 {rewards.Count}종 중 아이콘 슬롯 {icons.Length}개만 표시합니다. 씬에 슬롯을 추가해 주세요.", this);

            // 이미 열려 있으면 내용만 바꾼다. 나타나기를 기다리는 중이면 그대로 기다린다.
            if (IsOpen)
            {
                if (reveal == null) panelRoot.SetActive(true);
                return;
            }

            IsOpen = true;
            // 보상창 뒤에서는 플레이어가 느린 채로 남지 않고, 하던 동작을 마친 뒤 대기 자세로 돌아간다.
            if (SlowMotion.IsSlowed) SlowMotion.Resume();
            uiManager = UIManager.Instance;
            uiManager.LockCursor(false, true);
            // 조작은 바로 막고, 커서는 화면이 나타날 때 보여 준다.
            Cursor.visible = false;
            if (Player.Player.Instance != null && Player.Player.Instance.PlayerController != null)
                Player.Player.Instance.PlayerController.EnterIdle();

            ResetReveal();
            panelRoot.SetActive(false);
            reveal = DOVirtual.DelayedCall(revealDelay, BeginReveal, true);
        }

        public void Close()
        {
            ResetReveal();
            if (tooltip != null) tooltip.Hide();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (IsOpen == false) return;

            IsOpen = false;
            if (SlowMotion.IsSlowed) SlowMotion.Resume();
            if (uiManager != null) uiManager.LockCursor(true);
            
            SceneLoadManager.Instance.LoadScene("02. Lobby");
        }

        private void OnDisable() => Close();

        private void BeginReveal()
        {
            panelRoot.SetActive(true);
            Cursor.visible = true;
            Canvas.ForceUpdateCanvases();

            if (slots != null) slots.HideAll();
            float opened = 0f;
            if (lineReveal != null)
            {
                lineReveal.Open();
                opened = lineReveal.OpenDuration;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            reveal = sequence;
            if (glitch != null) sequence.InsertCallback(opened, () => glitch.Play(glitchDuration, glitchStrength));
            if (slots != null) sequence.InsertCallback(opened, () => slots.Play());
            sequence.OnComplete(() => reveal = null);
        }

        // 연출을 멈추고 다 드러난 상태로 맞춘다. 닫을 때와 새로 열기 전에 부른다.
        private void ResetReveal()
        {
            reveal?.Kill();
            reveal = null;

            if (lineReveal != null) lineReveal.Snap(true);
            if (glitch != null) glitch.Stop();
            if (slots != null) slots.ShowAll();
        }
    }
}
