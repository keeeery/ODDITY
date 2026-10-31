using System.Collections.Generic;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd
{
    // 염동력 코드를 고치지 않으려고 피격 이벤트 대신 날아오는 물체를 직접 살펴 맞기 전에 반응함
    public class HumanoidProjectileWatcher : MonoBehaviour, IHumanoidPhaseListener
    {
        [Title("References")]
        [SerializeField] private BossMonster boss;
        [SerializeField] private BossStateBase[] interruptibleStates; // 이 상태일 때만 반응, 공격 중엔 그냥 맞음
        // 들어온 직후 earlyInterruptTime 동안만 반응, 들어올 때 순간이동·소환처럼 끊으면 정리가 안 되는 패턴은 넣지 않음
        [SerializeField] private BossStateBase[] earlyInterruptibleStates;
        [SerializeField] private HumanoidReactionPattern[] reactions;

        [Title("Detection")]
        [SerializeField] private float detectRadius = 25f;
        [SerializeField] private float minSpeed = 8f;
        [SerializeField] private float reactTime = 0.6f; // 도착까지 남은 시간이 이보다 짧아지면 반응
        [SerializeField] private float hitRadius = 4f; // 진행선이 보스 몸통에서 이 거리 안을 지나면 맞을 물체로 봄
        [SerializeField] private float centerHeight = 2.5f;
        [SerializeField] private float frontHalfAngle = 100f; // 등 뒤에서 오는 물체엔 반응하지 않음 (등 뒤 공략 여지)
        [SerializeField] private float earlyInterruptTime = 0.3f; // earlyInterruptibleStates에 들어온 뒤 반응할 수 있는 시간

        [Title("Cooldown")]
        [SerializeField] private float cooldown = 3f; // 반응마다의 쿨과 별개로, 연달아 던지면 가끔은 맞도록
        [SerializeField, Range(0f, 1f)] private float reactChance = 0.5f; // 쿨이 돌았어도 이 확률로만 반응, 던질 때마다 한 번 굴림

        [Title("Phase 2")]
        [SerializeField, Range(0f, 1f)] private float phase2ReactChance = 0.5f;

        // 반응 조건을 맞추는 동안 켜 둠, 맞을 물체에 반응하지 않은 이유를 물체마다 바뀔 때만 남김
        [Title("Debug")]
        [SerializeField] private bool logSkipReason = true;

        private readonly List<HumanoidReactionPattern> candidates = new();
        private readonly List<float> weights = new();

        // 플레이어가 들었던 물체, 놓은 뒤 느려질 때까지만 투척으로 봄
        // 방 기믹으로 떨어지는 물체나 다른 보스가 던진 물체는 들린 적이 없어 반응하지 않음 (던졌는지 플래그가 염동력 쪽에 비공개라 대신 씀)
        private readonly HashSet<PsychokinesisObject> playerObjects = new();

        private float readyTime;
        private PsychokinesisObject rolledObject;
        private bool isChancePassed;
        private bool isPhase2;
        private BossStateBase earlyState;
        private float earlyEnterTime;
        private PsychokinesisObject loggedObject;
        private string loggedReason;

        private float ReactChance => isPhase2 ? phase2ReactChance : reactChance;

        public void EnterPhase2() => isPhase2 = true;

        private void FixedUpdate()
        {
            // 시간 정지 중에 잡았다 던진 물체도 놓치지 않도록 정지 여부와 상관없이 기록
            TrackPlayerObjects();

            if (boss.Clock.IsFrozen) return;

            TrackEarlyState();

            if (TryFindThreat(out PsychokinesisObject threat, out PsychokinesisObject rejected, out string rejectReason) == false)
            {
                if (rejected != null) LogSkip(rejected, rejectReason);
                return;
            }

            if (boss.Clock.LocalTime < readyTime)
            {
                LogSkip(threat, $"감시자 쿨 중 (cooldown {cooldown}초)");
                return;
            }

            if (IsInterruptible() == false)
            {
                LogSkip(threat, $"반응할 수 없는 상태 ({GetActiveStateName()})");
                return;
            }

            if (TryPickReaction(out HumanoidReactionPattern reaction) == false)
            {
                LogSkip(threat, "모든 반응이 쿨 중");
                return;
            }

            if (PassChance(threat) == false)
            {
                LogSkip(threat, $"확률에서 빠짐 (reactChance {ReactChance})");
                return;
            }

            readyTime = boss.Clock.LocalTime + cooldown;
            Debug.Log($"[HumanoidProjectileWatcher] {threat.name}에 {reaction.GetType().Name}로 반응", this);
            reaction.React(threat);
        }

        private void LogSkip(PsychokinesisObject target, string reason)
        {
            if (logSkipReason == false) return;
            if (target == loggedObject && reason == loggedReason) return;

            loggedObject = target;
            loggedReason = reason;
            Debug.Log($"[HumanoidProjectileWatcher] {target.name}에 반응 안 함: {reason}", this);
        }

        // 풀로 돌아가거나 부서진 물체는 다시 꺼내질 때 다른 물체이므로 지움
        private void TrackPlayerObjects()
        {
            playerObjects.RemoveWhere(IsGone);

            foreach (PsychokinesisObject candidate in PsychokinesisObject.ActiveObjects)
            {
                if (candidate.IsHeld == false) continue;

                playerObjects.Add(candidate);

                // 다시 잡으면 새로 던지는 것이므로 확률도 다시 굴림
                if (candidate == rolledObject) rolledObject = null;
            }
        }

        // 날아오는 동안 매 물리 스텝 굴리면 결국 반응하게 되므로 던질 때마다 한 번만 굴림
        private bool PassChance(PsychokinesisObject threat)
        {
            if (threat != rolledObject)
            {
                rolledObject = threat;
                isChancePassed = Random.value < ReactChance;
            }

            return isChancePassed;
        }

        private static bool IsGone(PsychokinesisObject candidate) => candidate == null || candidate.isActiveAndEnabled == false;

        private string GetActiveStateName()
        {
            foreach (BossStateBase state in boss.GetComponentsInChildren<BossStateBase>())
                return state.GetType().Name;

            return "없음";
        }

        private bool IsInterruptible()
        {
            foreach (BossStateBase state in interruptibleStates)
                if (state.isActiveAndEnabled) return true;

            return earlyState != null && boss.Clock.LocalTime - earlyEnterTime <= earlyInterruptTime;
        }

        // 상태의 진입 시각을 상태 쪽에서 알려 주지 않으므로, 켜진 상태가 바뀌는 순간을 직접 기록함
        // 같은 상태로 다시 들어오면 같은 프레임에 껐다 켜져서 새 진입으로 보지 못함
        private void TrackEarlyState()
        {
            BossStateBase active = null;

            foreach (BossStateBase state in earlyInterruptibleStates)
            {
                if (state.isActiveAndEnabled == false) continue;

                active = state;
                break;
            }

            if (active == earlyState) return;

            earlyState = active;
            earlyEnterTime = boss.Clock.LocalTime;
        }

        // 맞을 물체 중 가장 먼저 도착하는 것, 곧 도착하는데 빗나가거나 등 뒤라 뺀 물체는 rejected로 알려 줌
        private bool TryFindThreat(out PsychokinesisObject threat, out PsychokinesisObject rejected, out string rejectReason)
        {
            threat = null;
            rejected = null;
            rejectReason = null;
            float soonest = float.MaxValue;
            Vector3 center = boss.transform.position + Vector3.up * centerHeight;

            foreach (PsychokinesisObject candidate in PsychokinesisObject.ActiveObjects)
            {
                // IsSpawnedByBoss는 보지 않음: 보스가 벽에 부딪혀 떨어뜨린 파편도 이 표시가 남은 채로 플레이어가 주워 던짐
                if (candidate.IsHeld) continue;
                if (playerObjects.Contains(candidate) == false) continue;

                Rigidbody body = candidate.Body;
                if (body.isKinematic) continue;

                Vector3 velocity = body.linearVelocity;
                float speed = velocity.magnitude;

                // 떨어져 멈췄거나 던지지 않고 놓은 물체는 더 이상 투척이 아님
                if (speed < minSpeed)
                {
                    playerObjects.Remove(candidate);
                    continue;
                }

                Vector3 offset = center - body.worldCenterOfMass;
                float distance = offset.magnitude;
                if (distance > detectRadius) continue;

                // 진행 방향으로 보스까지 남은 거리, 0 이하면 멀어지는 중
                float along = Vector3.Dot(offset, velocity / speed);
                if (along <= 0f) continue;

                float arriveTime = along / speed;
                if (arriveTime > reactTime || arriveTime >= soonest) continue;

                float miss = Mathf.Sqrt(Mathf.Max(0f, distance * distance - along * along));
                if (miss > hitRadius)
                {
                    rejected = candidate;
                    rejectReason = $"진행선이 몸통 중심에서 {miss:F0}m 떨어져 지나감 (hitRadius {hitRadius})";
                    continue;
                }

                if (IsInFront(body.worldCenterOfMass) == false)
                {
                    rejected = candidate;
                    rejectReason = "등 뒤에서 옴";
                    continue;
                }

                soonest = arriveTime;
                threat = candidate;
            }

            return threat != null;
        }

        private bool IsInFront(Vector3 point)
        {
            Vector3 direction = point - boss.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return true;

            return Vector3.Angle(boss.transform.forward, direction) <= frontHalfAngle;
        }

        private bool TryPickReaction(out HumanoidReactionPattern result)
        {
            result = null;
            candidates.Clear();
            weights.Clear();

            foreach (HumanoidReactionPattern reaction in reactions)
            {
                if (reaction.CanUse == false) continue;

                candidates.Add(reaction);
                weights.Add(reaction.ReactionWeight);
            }

            if (candidates.Count == 0) return false;

            result = candidates.GetWeightedRandom(weights);
            return true;
        }
    }
}
