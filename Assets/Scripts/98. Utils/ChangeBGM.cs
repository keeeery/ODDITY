using System;
using System.Collections;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd
{
    public class ChangeBGM : MonoBehaviour
    {
        [SerializeField] private BGMPlayer[] bgms;
        // SoundManager.StopBGM 페이드(0.5초)가 끝나 Stop까지 마친 뒤 다음 곡을 켜야 새 곡이 같이 멈추지 않음
        [SerializeField, Min(0f)] private float switchDelay = 0.6f;

        private float currentBGMTime;
        private float playTime;
        private int currentBGM;
        private bool isSwitching;

        private void Start()
        {
            if(SoundManager.Instance.TryGetClip(bgms[currentBGM].BGM.ClipName, out AudioClip clip) == false) return;

            currentBGMTime = clip.length;
        }

        private void Update()
        {
            if (isSwitching) return;

            playTime += Time.deltaTime;

            if (SimpleInput.GetKeyDown(Key.Digit9) || playTime > currentBGMTime)
                StartCoroutine(SwitchRoutine());
        }

        private IEnumerator SwitchRoutine()
        {
            isSwitching = true;

            int nextBGM = currentBGM >= bgms.Length - 1 ? 0 : currentBGM + 1;

            bgms[currentBGM].gameObject.SetActive(false);

            yield return new WaitForSecondsRealtime(switchDelay);

            bgms[nextBGM].gameObject.SetActive(true);
            playTime = 0;

            if (currentBGM >= bgms.Length - 1) currentBGM = 0;
            else currentBGM++;
            Debug.Log($"currentBGM: {currentBGM}");

            SoundManager.Instance.TryGetClip(bgms[currentBGM].BGM.ClipName, out AudioClip clip);
            currentBGMTime = clip.length;

            isSwitching = false;
        }
    }
}
