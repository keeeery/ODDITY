using System.Collections;
using TeamOdd.Combat;
using TeamOdd.Player;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 두 번째 심장이 발동했을 때의 부활 연출. SecondHeartSkill이 플레이어에 붙이고, 연출이 끝나면 스스로 사라진다
    // 1. 쓰러짐: 히트스탑 뒤 월드 전체가 느려지고, 플레이어는 사망 모션(Dead)으로 무너지기 시작한다
    //    체력은 1로 떨어뜨려 체력바가 거의 빈 것처럼 보이게 한다 (0이면 죽은 것으로 처리되므로 1)
    // 2. 부활: 슬로모션이 확 풀리면서 부활 모션(Wakeup Blend Tree의 Unarmed-Revive1)으로 일어서고,
    //    체력이 부활 체력으로 확 차오르며 심장 박동 이펙트와 카메라 흔들림이 같이 터진다
    // 연출 동안은 조작이 막히고 피해를 받지 않는다
    // 애니메이터와 PlayerController는 팀원 코드라 고치지 않고, 이미 있는 상태로 바로 넘어가는(CrossFade) 방식만 쓴다
    public class SecondHeartRevival : MonoBehaviour
    {
        private const float HitStopDuration = 0.12f; // 맞는 순간 멈칫하는 시간 (초)
        private const float FallSlowScale = 0.4f;    // 쓰러지는 동안의 슬로모션 배율
        private const float FallDuration = 1.5f;     // 쓰러지는 연출 시간 (실제 시간, 초)
        private const float ReviveDuration = 1.1f;   // 일어서는 모션이 끝날 때까지 기다리는 시간 (초)
        private const float CrossFadeTime = 0.15f;   // 모션을 바꿀 때 섞는 시간 (초)
        private const float ShakeTrauma = 0.5f;      // 일어설 때 카메라 흔들림 세기 (0~1)
        private const float ShakeFadeTime = 0.6f;    // 흔들림이 잦아드는 시간 (초)

        // 애니메이터 상태·매개변수 이름 (Player Animator.controller)
        private static readonly int DeadState = Animator.StringToHash("Dead");
        private static readonly int WakeupState = Animator.StringToHash("Wakeup Blend Tree");
        private static readonly int HitDir = Animator.StringToHash("HitDir"); // 기상 블렌드 트리에서 0이면 Revive1
        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int WakeUpTrigger = Animator.StringToHash("WakeUp");

        // reviveHp: 일어설 때 맞출 체력
        public void Play(GameObject heartbeatEffect, int reviveHp)
        {
            StartCoroutine(Revive(heartbeatEffect, reviveHp));
        }

        private IEnumerator Revive(GameObject heartbeatEffect, int reviveHp)
        {
            Animator animator = GetComponent<Animator>();
            PlayerController controller = GetComponent<PlayerController>();
            CombatEntity owner = GetComponent<CombatEntity>();
            Player.Player player = GetComponent<Player.Player>();

            controller.SetActionLocked(true);
            owner.IsInvincible = true;

            // 1. 쓰러짐
            owner.SetHp(1);
            HitStop.Request(HitStopDuration);
            SlowMotion.Play(FallSlowScale, FallDuration, 0f);
            animator.CrossFadeInFixedTime(DeadState, CrossFadeTime, 0);
            yield return new WaitForSecondsRealtime(FallDuration);

            // 2. 부활. 죽을 뻔한 그 한 방이 남긴 피격·기상 신호가 일어선 뒤에 터지지 않게 지운다
            //    (사망·기상 상태에는 Hit 전환이 없어서 신호가 남아 있다가 대기 상태로 돌아가는 순간 터진다)
            //    연출 동안은 무적이라 새 피격 신호는 들어오지 않는다
            SlowMotion.Resume();
            animator.ResetTrigger(HitTrigger);
            animator.ResetTrigger(WakeUpTrigger);
            animator.SetFloat(HitDir, 0f);
            animator.CrossFadeInFixedTime(WakeupState, CrossFadeTime, 0);

            owner.SetHp(reviveHp);
            Instantiate(heartbeatEffect, transform);
            player.CameraController.AddTrauma(ShakeTrauma, ShakeFadeTime);

            yield return new WaitForSeconds(ReviveDuration);

            controller.SetActionLocked(false);
            owner.IsInvincible = false;
            Destroy(this);
        }
    }
}
