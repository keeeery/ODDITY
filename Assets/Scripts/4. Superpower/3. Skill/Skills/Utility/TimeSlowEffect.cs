using System.Collections.Generic;
using Sirenix.OdinInspector;
using TeamOdd.TimeControl;
using TeamOdd.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 시간 감속 칩을 쓰는 동안의 화면 효과. 플레이어 프리팹에 TimeReverseEffect와 나란히 붙인다.
    /// 감속 기능(TimeSlowSkill, TimeFreezeManager, TimeFreezable)은 건드리지 않고 공개된 값만 읽어서 따라간다.
    /// </summary>
    public class TimeSlowEffect : MonoBehaviour
    {
        // 시간 역행(100)보다 아래에서 섞여야 역행이 겹쳤을 때 역행 화면이 우선한다
        private const float VOLUME_PRIORITY = 90f;

        // 펄스가 가장 세지는 지점 (펄스 길이에 대한 비율)
        private const float PULSE_PEAK = 0.2f;

        private const string DURATION_PARAM = "Duration";

        [Title("Hold Settings")]
        [SerializeField, Range(-100f, 0f)] private float saturation = -30f;
        [SerializeField] private Color colorFilter = new(0.8f, 0.9f, 1f);
        [SerializeField, Range(0f, 1f)] private float vignette = 0.3f;

        [Space]
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.3f;

        [Title("Pulse Settings")]
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.25f;

        // 시작 펄스
        [Space]
        [SerializeField, Range(-1f, 1f)] private float startLensDistortion = -0.3f;
        [SerializeField, Range(0f, 1f)] private float startChromaticAberration = 0.8f;
        [SerializeField, Range(0f, 1f)] private float startGlitch = 0.35f;

        // 끝 펄스
        [Space]
        [SerializeField, Range(-1f, 1f)] private float endLensDistortion = 0.15f;
        [SerializeField, Range(0f, 1f)] private float endGlitch = 0.2f;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private bool isSlowing = false; // 효과가 걷히는 동안은 false
        [ShowInInspector, ReadOnly] private bool isShowing = false; // 효과가 걷히는 동안도 true
        [ShowInInspector, ReadOnly] private float slowRemainTime = 0f;
        [ShowInInspector, ReadOnly] private float holdWeight = 0f;
        [ShowInInspector, ReadOnly] private float pulseTime = 0f;

        private AugmentationChipInventory inventory;
        private TimeReverseRecorder reverseRecorder;

        // 쿨타임은 칩을 따라다니므로 슬롯이 아닌 스킬을 키로 기억해야 슬롯 순서가 바뀌어도 맞는다
        private readonly Dictionary<SuperpowerSkill, float> prevCooldowns = new();

        private Volume holdVolume;
        private Volume pulseVolume;
        private LensDistortion pulseLens;
        private ChromaticAberration pulseChromatic;

        private float pulseGlitch = 0f;
        private float appliedGlitch = 0f;
        private bool wasPostProcessing = false;

        private bool IsPulsing => pulseTime < pulseDuration;

        #region UNITY METHODS

        private void Awake()
        {
            inventory = GetComponent<AugmentationChipInventory>();
            reverseRecorder = GetComponent<TimeReverseRecorder>();
            pulseTime = pulseDuration;

            holdVolume = CreateHoldVolume();
            pulseVolume = CreatePulseVolume();

            if (inventory == null)
                Debug.LogError($"[{nameof(TimeSlowEffect)}] AugmentationChipInventory is missing.", this);
        }

        private void OnDisable()
        {
            isSlowing = false;
            holdWeight = 0f;
            pulseTime = pulseDuration;

            HideEffect();
        }

        // HitStop 등으로 Time.timeScale이 바뀌어도 연출이 끊기지 않도록 unscaled로 진행
        private void Update()
        {
            if (isShowing == false) return;

            UpdateSlowTime();
            UpdateHoldWeight();
            UpdatePulseTime();

            ApplyEffect();

            bool isFinished =
                isSlowing == false &&
                holdWeight <= 0f &&
                IsPulsing == false;

            if (isFinished) HideEffect();
        }

        // SkillCaster와 인벤토리가 Update에서 쿨타임을 바꾼 뒤에 읽어야 하므로 LateUpdate
        private void LateUpdate()
        {
            if (inventory == null) return;

            IReadOnlyList<OwnedChip> slots = inventory.SkillSlots;

            for (int i = 0; i < slots.Count; i++)
            {
                OwnedChip chip = slots[i];

                if (chip == null) continue;
                if (chip.Def is not SkillChipDef def) continue;
                if (def.SkillType != SkillType.TimeSlow) continue;

                // SuperpowerSkill은 TryUse가 성공할 때만 쿨타임을 채우고, 나머지 경로는 줄이기만 한다
                // 그래서 쿨타임이 늘었으면 방금 쓴 것이다
                float currCooldown = chip.Skill.RemainingCooldown;
                bool isUsed =
                    prevCooldowns.TryGetValue(chip.Skill, out float prevCooldown) &&
                    currCooldown > prevCooldown;

                if (isUsed) BeginSlow(def.Params.GetFloat(DURATION_PARAM));

                prevCooldowns[chip.Skill] = currCooldown;
            }
        }

        #endregion

        #region STATE UPDATE METHODS

        // TimeFreezeManager가 WaitForSeconds로 기다리므로 같은 scaled 시간으로 센다
        private void UpdateSlowTime()
        {
            if (isSlowing == false) return;

            slowRemainTime -= Time.deltaTime;

            // 씬 전환 등으로 전체 정지가 먼저 풀렸으면 바로 끝낸다
            bool isSlowOver =
                slowRemainTime <= 0f ||
                TimeFreezable.IsWorldFrozen == false;

            if (isSlowOver) EndSlow();
        }

        private void UpdateHoldWeight()
        {
            float target = isSlowing ? 1f : 0f;
            float duration = isSlowing ? fadeInDuration : fadeOutDuration;

            holdWeight = Mathf.MoveTowards(holdWeight, target, Time.unscaledDeltaTime / duration);
        }

        private void UpdatePulseTime()
        {
            if (IsPulsing == false) return;

            pulseTime = Mathf.Min(pulseTime + Time.unscaledDeltaTime, pulseDuration);
        }

        #endregion

        #region PRIVATE METHODS

        // 감속 중에 또 쓰면 남은 시간만 새로 잡고 펄스를 다시 튀긴다
        private void BeginSlow(float duration)
        {
            if (isShowing == false)
            {
                wasPostProcessing = SetPostProcessing(isOn: true);
                isShowing = true;
            }

            isSlowing = true;
            slowRemainTime = duration;

            PlayPulse(startLensDistortion, startChromaticAberration, startGlitch);
        }

        private void EndSlow()
        {
            isSlowing = false;

            PlayPulse(endLensDistortion, 0f, endGlitch);
        }

        private void PlayPulse(float lensDistortion, float chromaticAberration, float glitch)
        {
            pulseLens.intensity.value = lensDistortion;

            // 0일 때 덮어쓰면 씬의 원래 색 번짐까지 지워지므로 덮어쓰지 않는다
            pulseChromatic.intensity.overrideState = chromaticAberration > 0f;
            pulseChromatic.intensity.value = chromaticAberration;

            pulseGlitch = glitch;
            pulseTime = 0f;
        }

        private void ApplyEffect()
        {
            float pulseWeight = GetPulseWeight(pulseTime / pulseDuration);

            holdVolume.weight = holdWeight;
            pulseVolume.weight = pulseWeight;
            ApplyGlitch(pulseGlitch * pulseWeight);
            SoundManager.Instance.SetLowPass(holdWeight); // 감속 화면과 같은 세기로 소리도 먹먹하게

            // 시간 역행 효과가 먼저 끝나며 Post Processing을 꺼도 다시 켜 둔다
            SetPostProcessing(isOn: true);
        }

        // ScreenGlitch는 차원 베기·사망 화면·씬 전환도 같이 쓰는 전역 값이다
        // 마지막으로 넣은 값과 다르면 다른 연출이 쓰는 중이므로 덮어쓰지 않고, 그쪽이 0으로 되돌리면 다시 넣는다
        private void ApplyGlitch(float glitch)
        {
            bool isUsedByOther = Mathf.Approximately(ScreenGlitch.Glitch, appliedGlitch) == false;

            if (isUsedByOther)
            {
                appliedGlitch = 0f;
                return;
            }

            ScreenGlitch.Glitch = glitch;
            appliedGlitch = glitch;
        }

        private void HideEffect()
        {
            holdVolume.weight = 0f;
            pulseVolume.weight = 0f;
            ApplyGlitch(0f);

            // 감속 중에 꺼져도(OnDisable) 소리가 먹먹한 채로 남지 않게 한다. 게임 종료 중에 SoundManager를 새로 만들지 않도록 있을 때만
            if (SoundManager.IsInitialized) SoundManager.Instance.SetLowPass(0f);

            if (isShowing == false) return;

            // 역행 화면이 켜져 있으면 역행 쪽이 끝나며 되돌리도록 둔다
            bool isRewinding = reverseRecorder != null && reverseRecorder.IsRewinding;
            if (isRewinding == false) SetPostProcessing(wasPostProcessing);

            isShowing = false;
        }

        // PULSE_PEAK까지 빠르게 올랐다가 끝까지 천천히 가라앉는다
        private static float GetPulseWeight(float progress)
        {
            if (progress >= 1f) return 0f;
            if (progress < PULSE_PEAK) return progress / PULSE_PEAK;

            float remain = 1f - (progress - PULSE_PEAK) / (1f - PULSE_PEAK);
            return remain * remain;
        }

        // 꺼져 있으면 Volume이 보이지 않으므로 켜고, 되돌릴 수 있게 원래 값을 반환한다
        private static bool SetPostProcessing(bool isOn)
        {
            Camera camera = Camera.main;
            if (camera == null) return isOn;

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            bool wasOn = cameraData.renderPostProcessing;

            cameraData.renderPostProcessing = isOn;
            return wasOn;
        }

        #endregion

        #region CREATE

        // 값은 고정하고 weight로만 섞어야 씬의 원래 보정과 자연스럽게 섞인다
        private Volume CreateHoldVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.saturation.Override(saturation);
            colorAdjustments.colorFilter.Override(colorFilter);

            profile.Add<Vignette>().intensity.Override(vignette);

            return CreateVolume("TimeSlowHoldVolume", profile);
        }

        // 시작·끝 펄스마다 값을 바꿔 쓰므로 컴포넌트를 기억해 둔다
        private Volume CreatePulseVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            pulseLens = profile.Add<LensDistortion>();
            pulseLens.intensity.Override(0f);

            pulseChromatic = profile.Add<ChromaticAberration>();
            pulseChromatic.intensity.Override(0f);

            return CreateVolume("TimeSlowPulseVolume", profile);
        }

        private Volume CreateVolume(string volumeName, VolumeProfile profile)
        {
            GameObject volumeObject = new(volumeName);
            volumeObject.transform.SetParent(transform, false);

            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = VOLUME_PRIORITY;
            volume.weight = 0f;
            volume.sharedProfile = profile;

            return volume;
        }

        #endregion
    }
}
