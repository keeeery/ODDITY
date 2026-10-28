using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 시간 역행으로 되감는 동안의 화면 효과. TimeReverseRecorder의 RewindStarted·RewindFinished를 받아 켜고 끈다.
    /// 되감기 기능(기록·이동·체력)은 전혀 모르고, 이 컴포넌트를 빼도 되감기는 그대로 동작한다.
    /// 플레이어 프리팹(Prefabs/1. Player/Player.prefab)에 Recorder와 함께 붙어 있다.
    /// - 되감는 동안: 흑백 + 색 번짐 + 오목 렌즈 + 가장자리 어둡게 (코드로 만든 Global Volume)
    /// - 되감기가 끝나면 FadeOutDuration초 동안 서서히 사라진다
    /// </summary>
    public class TimeReverseEffect : MonoBehaviour
    {
        private const float FadeOutDuration = 0.3f; // 되감기가 끝난 뒤 화면 효과가 사라지는 시간 (초)

        // 되감는 동안의 화면 효과 세기
        private const float Saturation = -80f;         // 채도 (-100이면 완전 흑백)
        private const float ChromaticAberration = 1f;  // 색 번짐 (0~1)
        private const float LensDistortion = -0.4f;    // 렌즈 왜곡 (음수면 가운데가 오목)
        private const float Vignette = 0.45f;          // 화면 가장자리 어둡게 (0~1)

        private TimeReverseRecorder recorder;
        private Volume volume;
        private Coroutine fadeRoutine;
        private bool isShowing;              // 화면 효과가 켜져 있는지 (사라지는 중 포함)
        private bool prevPostProcessing;     // 효과를 켜기 전 카메라의 Post Processing 설정

        private void Awake()
        {
            recorder = GetComponent<TimeReverseRecorder>();
            volume = CreateVolume();
        }

        private void OnEnable()
        {
            recorder.RewindStarted += OnRewindStarted;
            recorder.RewindFinished += OnRewindFinished;
        }

        private void OnDisable()
        {
            recorder.RewindStarted -= OnRewindStarted;
            recorder.RewindFinished -= OnRewindFinished;
            StopFade();
            Hide();
        }

        private void OnRewindStarted()
        {
            // 앞 효과가 사라지는 중이었으면 멈추고 다시 꽉 채운다
            StopFade();

            if (isShowing == false)
            {
                prevPostProcessing = SetPostProcessing(true);
                isShowing = true;
            }

            volume.weight = 1f;
        }

        private void OnRewindFinished()
        {
            fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private void StopFade()
        {
            if (fadeRoutine == null) return;

            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        private IEnumerator FadeOutRoutine()
        {
            float elapsed = 0f;
            while (elapsed < FadeOutDuration)
            {
                elapsed += Time.deltaTime;
                volume.weight = 1f - Mathf.Clamp01(elapsed / FadeOutDuration);
                yield return null;
            }

            fadeRoutine = null;
            Hide();
        }

        private void Hide()
        {
            volume.weight = 0f;
            if (isShowing == false) return;

            SetPostProcessing(prevPostProcessing);
            isShowing = false;
        }

        // 카메라의 Post Processing을 켜거나 끄고 원래 값을 돌려준다. 꺼져 있으면 Volume이 보이지 않는다
        private static bool SetPostProcessing(bool on)
        {
            UniversalAdditionalCameraData cameraData = Camera.main.GetUniversalAdditionalCameraData();
            bool prev = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = on;
            return prev;
        }

        // 되감기 화면 효과를 담은 Global Volume. weight 0으로 두었다가 되감을 때만 올린다
        // 인스펙터에서 효과를 고치고 싶어지면 이 대신 씬에 Volume을 두고 SerializeField로 받으면 된다
        private Volume CreateVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<ColorAdjustments>().saturation.Override(Saturation);
            profile.Add<ChromaticAberration>().intensity.Override(ChromaticAberration);
            profile.Add<LensDistortion>().intensity.Override(LensDistortion);
            profile.Add<Vignette>().intensity.Override(Vignette);

            GameObject volumeObject = new GameObject("TimeReverseVolume");
            volumeObject.transform.SetParent(transform, false);

            Volume created = volumeObject.AddComponent<Volume>();
            created.isGlobal = true;
            created.priority = 100f; // 씬의 다른 Volume보다 위에서 섞이도록
            created.weight = 0f;
            created.sharedProfile = profile;
            return created;
        }
    }
}
