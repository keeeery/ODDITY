using System.Collections.Generic;
using TeamOdd.Combat;
using TeamOdd.Superpower;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.Reward
{
    // 화면과 아이콘 슬롯은 씬에 배치하고, 획득 결과만 채운다.
    [DisallowMultipleComponent]
    public sealed class BossRewardPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RewardIconView[] icons;
        [SerializeField] private Button closeButton;
        [SerializeField] private TooltipView tooltip;

        private UIManager uiManager;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
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

            panelRoot.SetActive(true);
            if (IsOpen) return;

            IsOpen = true;
            // 보상창 뒤에서는 플레이어가 느린 채로 남지 않고 곧바로 대기 자세로 선다.
            if (SlowMotion.IsSlowed) SlowMotion.Resume();
            uiManager = UIManager.Instance;
            uiManager.LockCursor(false, true);
            if (Player.Player.Instance != null && Player.Player.Instance.PlayerController != null)
                Player.Player.Instance.PlayerController.EnterIdle();
        }

        public void Close()
        {
            if (tooltip != null) tooltip.Hide();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (IsOpen == false) return;

            IsOpen = false;
            if (SlowMotion.IsSlowed) SlowMotion.Resume();
            if (uiManager != null) uiManager.LockCursor(true);
        }

        private void OnDisable() => Close();
    }
}
