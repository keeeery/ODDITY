using System.Collections.Generic;
using DG.Tweening;
using TeamOdd.Combat;
using TeamOdd.Mission;
using TeamOdd.Superpower;
using TeamOdd.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    // 화면은 씬에 배치하고, 아이콘 슬롯은 씬에 둔 슬롯 하나를 본떠 받은 보상 수만큼 만든다.
    // 열면 잠시 뒤 가운데 1px 선이 좌우로 뻗고 위아래로 열리며 화면이 켜지고, 짧게 지직거린 뒤 슬롯이 하나씩 번쩍이며 켜진다.
    // 연출은 실제 시간 기준이다. 사망 화면(PlayerDeathScreen)과 같은 부품을 세기만 다르게 쓴다.
    [DisallowMultipleComponent]
    public sealed class BossRewardPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [Tooltip("씬에 둔 슬롯 하나, 보상이 더 많으면 같은 부모 아래에 복제해 늘린다.")]
        [SerializeField] private RewardIconView iconTemplate;
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

        [Header("Accent")]
        [Tooltip("윗줄·제목·장식 선 강조, Tools → Team Odd → Enhance Result Screens 가 만들어 연결한다")]
        [SerializeField] private ResultBanner banner;
        [Tooltip("보상 칸 위에 받은 칩 수를 보여 줄 글자, 비워 두면 쓰지 않음")]
        [SerializeField] private TMP_Text rewardCountText;
        [Tooltip("{0}에 받은 칩 수가 들어간다")]
        [SerializeField] private string rewardCountFormat = "증강 칩 {0}개를 획득했습니다.";

        private readonly List<RewardIconView> icons = new();
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
            if (panelRoot == null || iconTemplate == null || closeButton == null)
            {
                Debug.LogError("[BossRewardPanel] 씬의 Panel Root, Icon Template, Close Button을 연결해 주세요.", this);
                if (SlowMotion.IsSlowed) SlowMotion.Resume();
                return;
            }

            // 만든 슬롯은 지우지 않고 남겨 두었다가, 보상이 적으면 남는 슬롯만 끈다.
            if (icons.Count == 0) icons.Add(iconTemplate);
            while (icons.Count < rewards.Count)
                icons.Add(Instantiate(iconTemplate, iconTemplate.transform.parent));
            for (int i = 0; i < icons.Count; i++)
                icons[i].Show(i < rewards.Count ? rewards[i] : null);
            if (rewardCountText != null) rewardCountText.text = string.Format(rewardCountFormat, rewards.Count);

            // 이미 열려 있으면 내용만 바꾼다. 나타나기를 기다리는 중이면 그대로 기다린다.
            if (IsOpen)
            {
                if (reveal == null) panelRoot.SetActive(true);
                return;
            }

            IsOpen = true;
            closeButton.interactable = true;
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

        // 확인으로 닫으면 패널을 띄운 채 바로 씬 전환 연출을 시작해 화면과 함께 꺼지게 한다.
        public void Close()
        {
            ResetReveal();
            if (tooltip != null) tooltip.Hide();
            if (IsOpen == false)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                return;
            }
            uiManager.MissionListUpdate();

            IsOpen = false;
            // 화면이 꺼지는 동안 다시 누르지 않게 막는다
            if (closeButton != null) closeButton.interactable = false;
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
            if (banner != null) banner.Snap(false);
            float opened = 0f;
            if (lineReveal != null)
            {
                lineReveal.Open();
                opened = lineReveal.OpenDuration;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            reveal = sequence;
            if (banner != null) sequence.InsertCallback(opened, () => banner.Play());
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
            if (banner != null) banner.Snap(true);
        }
    }
}
