using System;
using System.Collections;
using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    /// <summary>
    /// 시간 역행 스킬이 쓰는 플레이어 쪽 컴포넌트. 스킬 객체는 MonoBehaviour가 아니라 기록하거나 코루틴을 돌릴 수 없어서 이 일을 여기서 한다.
    /// 스킬을 쓰기 전부터 기록이 쌓여 있어야 하므로 플레이어 프리팹(Prefabs/1. Player/Player.prefab)에 미리 붙여 둔다.
    /// - 늘 최근 HistoryDuration초 동안의 모습(위치·회전·애니메이션·체력·스태미나)을 RecordInterval마다 기록한다. 더 오래된 기록은 버린다
    ///   애니메이션은 레이어마다 재생 중이던 상태와 진행률을 적어 두었다가, 되감을 때 Animator.Play로 그 장면을 그대로 보여준다 (거꾸로 재생되는 것처럼 보인다)
    /// - Rewind(seconds): 기록을 끝(지금)에서 seconds초 전까지 RewindMultiplier배속으로 거꾸로 틀며, 체력·스태미나도 그때 값으로 되돌린다
    ///   (영상 되감기처럼 기록한 시간을 배속으로 거슬러 간다. 기록이 seconds초보다 짧으면 가장 오래된 기록까지만 간다)
    /// - 되감는 동안에는 무적이고 이동 입력을 받지 않는다. 되감은 뒤에는 기록을 비우고 새로 쌓는다
    /// 화면 효과 같은 연출은 여기서 하지 않고 RewindStarted·RewindFinished 이벤트로 알리기만 한다 (TimeReverseEffect가 받아서 처리)
    /// </summary>
    public class TimeReverseRecorder : MonoBehaviour
    {
        private const float RecordInterval = 0.05f;  // 기록하는 간격 (초)
        private const float HistoryDuration = 20f;   // 기록을 남겨 두는 시간 (초). 스킬의 Duration이 이보다 길면 이만큼만 되돌아간다
        private const float RewindMultiplier = 3f;   // 되감기 배속 (3이면 3초 동안 한 일을 1초 만에 되감는다)

        // 한 순간의 기록
        private struct Snapshot
        {
            public float Time;                  // 기록한 시각
            public Vector3 Position;
            public Quaternion Rotation;
            public AnimationSample[] Animation; // 애니메이터 레이어마다 하나
            public int Hp;
            public float Stamina;
        }

        // 그 순간 애니메이터 레이어 하나가 재생하던 것
        private struct AnimationSample
        {
            public int StateHash;        // 재생 중이던 상태
            public float NormalizedTime; // 그 상태를 얼마나 재생했는지 (반복 상태는 1을 넘는다)
            public float Weight;         // 레이어 무게 (1번 레이어는 물체를 들고 있을 때만 1)
        }

        private readonly List<Snapshot> history = new(); // 0번이 가장 오래된 기록, 마지막이 가장 최근

        private CombatEntity owner;
        private Player.Player player;
        private CharacterController controller;
        private Animator animator;
        private float prevAnimatorSpeed; // 되감는 동안 애니메이터를 멈춰 두었다가 되돌릴 빠르기
        private float nextRecordTime;

        public bool IsRewinding { get; private set; }

        // 되돌아갈 기록이 있는지 (막 되감은 직후에는 비어 있다)
        public bool HasHistory => history.Count >= 2;

        // 되감기가 시작될 때와 끝날 때. 화면 효과·사운드 같은 연출을 붙일 때 사용
        public event Action RewindStarted;
        public event Action RewindFinished;

        private void Awake()
        {
            owner = GetComponent<CombatEntity>();
            player = GetComponent<Player.Player>();
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (IsRewinding || Time.time < nextRecordTime) return;

            Record();

            // HistoryDuration초보다 오래된 기록은 버린다
            while (history.Count > 0 && history[0].Time < Time.time - HistoryDuration)
                history.RemoveAt(0);
        }

        // seconds초 전으로 되돌아간다
        public void Rewind(float seconds)
        {
            if (IsRewinding || HasHistory == false) return;

            StartCoroutine(RewindRoutine(seconds));
        }

        private void Record()
        {
            history.Add(new Snapshot
            {
                Time = Time.time,
                Position = transform.position,
                Rotation = transform.rotation,
                Animation = CaptureAnimation(),
                Hp = owner.Hp,
                Stamina = player.Stamina,
            });
            nextRecordTime = Time.time + RecordInterval;
        }

        private IEnumerator RewindRoutine(float seconds)
        {
            IsRewinding = true;
            Record(); // 마지막 기록은 지금 모습

            SetRewinding(true);
            RewindStarted?.Invoke();

            // 되감기 시계. 마지막 기록 시각에서 출발해 seconds초 전(기록이 짧으면 가장 오래된 기록)까지 배속으로 거꾸로 흐른다
            float rewindTime = history[history.Count - 1].Time;
            float targetTime = Mathf.Max(history[0].Time, rewindTime - seconds);
            int index = history.Count - 1; // rewindTime이 index-1번 기록과 index번 기록 사이에 있다

            while (true)
            {
                rewindTime = Mathf.Max(targetTime, rewindTime - RewindMultiplier * Time.deltaTime);

                // 한 프레임에 기록을 여러 개 지나칠 수 있어서, rewindTime보다 늦은 기록은 모두 넘긴다
                while (index > 0 && history[index - 1].Time >= rewindTime)
                    index--;

                ShowRecordedAt(rewindTime, index);

                if (rewindTime <= targetTime) break;
                yield return null;
            }

            // 되감은 시간은 없던 일이 되므로 기록을 비우고 지금부터 새로 쌓는다
            history.Clear();
            SetRewinding(false);
            IsRewinding = false;
            RewindFinished?.Invoke();
        }

        // time 시각에 기록된 모습으로 맞춘다. time은 index-1번 기록과 index번 기록 사이
        // 위치·회전은 두 기록 사이를 섞어 부드럽게, 애니메이션·체력·스태미나는 더 가까운 기록의 값을 쓴다
        private void ShowRecordedAt(float time, int index)
        {
            if (index == 0)
            {
                transform.SetPositionAndRotation(history[0].Position, history[0].Rotation);
                ApplyValues(history[0]);
                return;
            }

            Snapshot before = history[index - 1];
            Snapshot after = history[index];
            float gap = after.Time - before.Time;
            float blend = gap > 0f ? (time - before.Time) / gap : 1f; // 0이면 before, 1이면 after

            transform.SetPositionAndRotation(
                Vector3.Lerp(before.Position, after.Position, blend),
                Quaternion.Slerp(before.Rotation, after.Rotation, blend));
            ApplyValues(blend < 0.5f ? before : after);
        }

        // 기록한 애니메이션·체력·스태미나로 맞춘다
        private void ApplyValues(Snapshot snapshot)
        {
            ApplyAnimation(snapshot.Animation);
            owner.SetHp(snapshot.Hp);
            player.SetStamina(snapshot.Stamina);
        }

        // 애니메이터 레이어마다 지금 재생 중인 상태와 진행률을 적는다
        private AnimationSample[] CaptureAnimation()
        {
            AnimationSample[] samples = new AnimationSample[animator.layerCount];
            for (int i = 0; i < samples.Length; i++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(i);
                samples[i] = new AnimationSample
                {
                    StateHash = state.fullPathHash,
                    NormalizedTime = state.normalizedTime,
                    Weight = animator.GetLayerWeight(i),
                };
            }

            return samples;
        }

        // 적어 둔 장면으로 애니메이터를 맞춘다. 되감는 동안 애니메이터 빠르기가 0이라 이 장면에서 멈춰 보인다
        private void ApplyAnimation(AnimationSample[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                animator.SetLayerWeight(i, samples[i].Weight);
                animator.Play(samples[i].StateHash, i, samples[i].NormalizedTime);
            }
        }

        // 되감는 동안 무적, 이동 입력 막기, CharacterController 끄기, 애니메이터 멈추기
        // (CharacterController가 켜져 있으면 position을 바꿔도 다음 프레임에 원래 자리로 돌아간다)
        // (애니메이터가 흐르면 Play로 맞춘 장면에서 앞으로 재생되어 버리므로 빠르기를 0으로 둔다)
        private void SetRewinding(bool rewinding)
        {
            owner.IsInvincible = rewinding;

            if (rewinding) prevAnimatorSpeed = animator.speed;
            animator.speed = rewinding ? 0f : prevAnimatorSpeed;

            controller.enabled = rewinding == false;

            player.IsMoveLocked = rewinding;
            player.ControllerActive(); // 다음 프레임을 기다리지 않고 바로 PlayerController를 끈다
        }
    }
}
