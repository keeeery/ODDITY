using System.Collections;
using System.Collections.Generic;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using Study.MiniDefence;
using TeamOdd.Mission;
using UnityEngine;

namespace FantasyGarden.Mission
{
    // 수락한 임무의 보스 홀로그램을 한 바퀴마다 순서를 섞어 하나씩 번갈아 보여줌
    public class BossHologramDisplay : MonoBehaviour
    {
        [Title("Settings")]
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float transitionDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float glitchPeakAmount = 0.6f;

        [Title("References")]
        [SerializeField] private BossHologram[] holograms;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private BossHologram currHologram;
        [ShowInInspector, ReadOnly] private float visibility = 0f;

        private readonly List<BossHologram> playList = new();
        private MaterialPropertyBlock propertyBlock;
        private WaitForSeconds waitForDisplay;
        private Coroutine playCoroutine;

        private void Awake()
        {
            propertyBlock = new();
            waitForDisplay = new WaitForSeconds(displayDuration);

            for (int i = 0; i < holograms.Length; i++)
            {
                BossHologram hologram = holograms[i];

                hologram.Initialize();
                hologram.ApplyVisibility(0f, glitchPeakAmount, propertyBlock);
                hologram.SetActive(false);
            }
        }

        private void OnEnable()
        {
            MissionListUI.OnAcceptedMissionsChanged += HandleAcceptedMissionsChanged;
            Play();
        }

        private void OnDisable()
        {
            MissionListUI.OnAcceptedMissionsChanged -= HandleAcceptedMissionsChanged;
            Stop();
        }

        private void HandleAcceptedMissionsChanged() => Play();

        private void Play()
        {
            Stop();
            playCoroutine = StartCoroutine(PlayCoroutine());
        }

        private void Stop()
        {
            if (playCoroutine == null) return;

            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        private IEnumerator PlayCoroutine()
        {
            // 1. 목록이 바뀌기 전에 보이던 홀로그램 정리
            if (currHologram != null) yield return HideCoroutine();

            // 2. 수락한 임무의 보스로 재생 목록 구성
            UpdatePlayList();
            if (playList.Count == 0) yield break;

            // 보스가 하나뿐이면 번갈아 보여줄 대상이 없으므로 계속 띄워 둠
            if (playList.Count == 1)
            {
                yield return ShowCoroutine(playList[0]);
                yield break;
            }

            // 3. 한 바퀴마다 순서를 섞어 하나씩 보여줌
            BossHologram prevHologram = null;

            while (true)
            {
                List<BossHologram> order = playList.Shuffle();
                int lastIdx = order.Count - 1;

                // 바퀴가 바뀔 때 같은 보스가 연달아 나오지 않도록 첫 보스를 맨 뒤로 보냄
                if (order[0] == prevHologram) (order[0], order[lastIdx]) = (order[lastIdx], order[0]);

                for (int i = 0; i < order.Count; i++)
                {
                    yield return ShowCoroutine(order[i]);
                    yield return waitForDisplay;
                    yield return HideCoroutine();
                }

                prevHologram = order[lastIdx];
            }
        }

        private IEnumerator ShowCoroutine(BossHologram hologram)
        {
            currHologram = hologram;
            currHologram.SetActive(true);

            yield return TransitionCoroutine(1f);
        }

        private IEnumerator HideCoroutine()
        {
            yield return TransitionCoroutine(0f);

            currHologram.SetActive(false);
            currHologram = null;
        }

        // 중간에 목록이 바뀌어 멈춰도 이어서 자연스럽게 사라지도록 현재 visibility 에서부터 보간
        private IEnumerator TransitionCoroutine(float targetVisibility)
        {
            while (Mathf.Approximately(visibility, targetVisibility) == false)
            {
                float step = Time.deltaTime / transitionDuration;

                visibility = Mathf.MoveTowards(visibility, targetVisibility, step);
                currHologram.ApplyVisibility(visibility, glitchPeakAmount, propertyBlock);

                yield return null;
            }

            visibility = targetVisibility;
            currHologram.ApplyVisibility(visibility, glitchPeakAmount, propertyBlock);
        }

        private void UpdatePlayList()
        {
            playList.Clear();

            IReadOnlyList<MissionDef> missions = MissionListUI.AcceptedMissionList;

            for (int i = 0; i < holograms.Length; i++)
            {
                BossHologram hologram = holograms[i];
                if (IsAccepted(hologram.BossName, missions) == false) continue;

                playList.Add(hologram);
            }
        }

        private bool IsAccepted(string bossName, IReadOnlyList<MissionDef> missions)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].BossName == bossName) return true;
            }

            return false;
        }
    }
}
