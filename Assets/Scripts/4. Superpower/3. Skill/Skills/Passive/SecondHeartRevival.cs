using System.Collections;
using TeamOdd.Combat;
using TeamOdd.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TeamOdd.Superpower
{
    // 두 번째 심장이 발동했을 때의 부활 연출. SecondHeartSkill이 플레이어에 붙이고, 연출이 끝나면 스스로 사라진다
    // 1. 쓰러짐: 히트스탑 뒤 월드 전체가 느려지고, 플레이어는 사망 모션으로 무너지기 시작한다
    //    체력은 1로 떨어뜨려 체력바가 거의 빈 것처럼 보이게 한다 (0이면 죽은 것으로 처리되므로 1)
    //    화면은 쓰러지는 동안 서서히 흑백으로 바래며 어두워진다
    // 2. 부활: 슬로모션이 확 풀리면서 부활 모션으로 일어서고,
    //    체력이 부활 체력으로 확 차오르며 심장 박동 이펙트와 카메라 흔들림이 같이 터진다
    //    화면은 흑백이 한 번에 걷히고, 잠깐 밝고 진한 색으로 번쩍였다가 원래대로 돌아온다
    // 연출 동안은 조작이 막히고 피해를 받지 않는다
    // 애니메이션은 Player Animator의 전용 노드로 움직인다 (트리거 두 개만 쏜다)
    //   Any State --SecondHeart--> SecondHeart Fall --Revive--> SecondHeart Revive --(끝나면)--> Unarmed-Idle
    //   두 노드에는 Hit 전환을 두지 않아서 연출 도중에는 피격 모션으로 끊기지 않는다
    public class SecondHeartRevival : MonoBehaviour
    {
        private const float HitStopDuration = 0.12f; // 맞는 순간 멈칫하는 시간 (초)
        private const float FallSlowScale = 0.4f;    // 쓰러지는 동안의 슬로모션 배율
        private const float FallDuration = 1.5f;     // 쓰러지는 연출 시간 (실제 시간, 초)
        private const float ReviveDuration = 1.1f;   // 일어서는 모션이 끝날 때까지 기다리는 시간 (초)
        private const float ShakeTrauma = 0.5f;      // 일어설 때 카메라 흔들림 세기 (0~1)
        private const float ShakeFadeTime = 0.6f;    // 흔들림이 잦아드는 시간 (초)

        // 쓰러질 때 화면: 흑백 + 어둡게
        private const float FadeSaturation = -100f;  // 채도 (-100이면 완전 흑백)
        private const float FadeExposure = -2.5f;    // 밝기 (음수면 어둡게)
        private const float FadeVignette = 0.5f;     // 화면 가장자리 어둡게 (0~1)

        // 일어설 때 화면: 밝고 진한 색으로 번쩍였다가 사라진다
        private const float BurstSaturation = 50f;
        private const float BurstExposure = 1.2f;
        private const float BurstDuration = 0.5f;    // 번쩍임이 사라지는 시간 (실제 시간, 초)

        // Player Animator.controller의 트리거
        private static readonly int SecondHeartTrigger = Animator.StringToHash("SecondHeart");
        private static readonly int ReviveTrigger = Animator.StringToHash("Revive");
        private static readonly int HitTrigger = Animator.StringToHash("Hit");

        // reviveHp: 일어설 때 맞출 체력, sounds: 쓰러질 때 Heartbeat, 일어설 때 Revive를 튼다 (SkillChip.tsv)
        public void Play(GameObject heartbeatEffect, int reviveHp, SkillSounds sounds)
        {
            StartCoroutine(Revive(heartbeatEffect, reviveHp, sounds));
        }

        private IEnumerator Revive(GameObject heartbeatEffect, int reviveHp, SkillSounds sounds)
        {
            Animator animator = GetComponent<Animator>();
            PlayerController controller = GetComponent<PlayerController>();
            CombatEntity owner = GetComponent<CombatEntity>();
            Player.Player player = GetComponent<Player.Player>();

            CancelLethalHitReaction(controller, animator);
            controller.SetActionLocked(true);
            owner.IsInvincible = true;

            bool prevPostProcessing = SetPostProcessing(true);
            Volume fadeVolume = CreateVolume("SecondHeartFadeVolume", FadeSaturation, FadeExposure, FadeVignette);
            Volume burstVolume = CreateVolume("SecondHeartBurstVolume", BurstSaturation, BurstExposure, 0f);

            // 1. 쓰러짐
            owner.SetHp(1);
            HitStop.Request(HitStopDuration);
            SlowMotion.Play(FallSlowScale, FallDuration, 0f);
            animator.SetTrigger(SecondHeartTrigger);
            sounds.Play("Heartbeat"); // 쓰러지는 동안 느려지며 잦아드는 심장 소리
            yield return ChangeWeight(fadeVolume, 0f, 1f, FallDuration);

            // 2. 부활
            SlowMotion.Resume();
            animator.SetTrigger(ReviveTrigger);
            fadeVolume.weight = 0f;
            burstVolume.weight = 1f;

            owner.SetHp(reviveHp);
            Instantiate(heartbeatEffect, transform);
            sounds.Play("Revive");
            player.CameraController.AddTrauma(ShakeTrauma, ShakeFadeTime);

            yield return ChangeWeight(burstVolume, 1f, 0f, BurstDuration);
            yield return new WaitForSeconds(ReviveDuration - BurstDuration);

            SetPostProcessing(prevPostProcessing);
            Destroy(fadeVolume.gameObject);
            Destroy(burstVolume.gameObject);

            controller.SetActionLocked(false);
            owner.IsInvincible = false;
            Destroy(this);
        }

        // Volume의 세기를 from에서 to로 duration초 동안 바꾼다. 슬로모션·히트스탑과 상관없이 실제 시간으로 잰다
        private static IEnumerator ChangeWeight(Volume volume, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                volume.weight = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
        }

        // 카메라의 Post Processing을 켜거나 끄고 원래 값을 돌려준다. 꺼져 있으면 Volume이 보이지 않는다 (TimeReverseEffect와 같은 방식)
        private static bool SetPostProcessing(bool on)
        {
            UniversalAdditionalCameraData cameraData = Camera.main.GetUniversalAdditionalCameraData();
            bool prev = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = on;
            return prev;
        }

        // 화면 색을 바꾸는 Global Volume. weight 0으로 만들어 두고 연출에서 올리고 내린다
        private Volume CreateVolume(string volumeName, float saturation, float exposure, float vignette)
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.saturation.Override(saturation);
            colorAdjustments.postExposure.Override(exposure);
            profile.Add<Vignette>().intensity.Override(vignette);

            GameObject volumeObject = new GameObject(volumeName);
            volumeObject.transform.SetParent(transform, false);

            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f; // 씬의 다른 Volume보다 위에서 섞이도록
            volume.weight = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        // 죽을 뻔한 그 한 방은 체력을 깎기 전에 피격 반응(밀려남·넉다운)부터 시작한다 (CombatEntity.Hit → PlayerController.ApplyHitReaction)
        // 그대로 두면 넉다운 타이머가 연출보다 길어서, 일어선 뒤에도 한동안 못 움직이거나 늦게 온 기상 신호가 남는다
        // PlayerController는 꺼질 때(OnDisable) 진행 중인 피격 반응을 정리하므로 한 번 껐다 켜서 끊는다
        // (TimeReverseRecorder와 같은 방식. 팀원 코드는 고치지 않는다)
        // 피격 반응이 이미 쏜 Hit 신호도 지운다. 남겨 두면 일어서서 대기 자세로 돌아가는 순간 피격 모션이 나온다
        private static void CancelLethalHitReaction(PlayerController controller, Animator animator)
        {
            controller.enabled = false;
            controller.enabled = true;
            animator.ResetTrigger(HitTrigger);
        }
    }
}
