using System.Collections.Generic;
using FantasyGarden.FSM;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 대상이 몸 가까이(다리 사이, 바로 앞) 머물면 포효해 음파로 밀쳐내는 근접 대응 패턴
    // 뒤로 뛰는 모션(Hop)이 없는 보스가 안쪽으로 파고든 대상을 떼어내는 용도, 밀어내는 거리는 정면 공격 사거리에 맞춤
    // - 진입하면 Roar를 재생하고 보스 중심에 원형 범위를 예고
    // - OnRoarDrop 신호(포효 정점)에서 음파를 터뜨리고 범위 안의 대상을 바깥으로 밀어냄
    // - 포효한 뒤 밀려난 대상 쪽으로 돌고, OnAttackEnded 신호로 끝나면 후딜 없이 연계 공격(followPatterns)으로 이어 감
    // - 애니메이션은 포효 클립을 일찍 끊는 전용 상태(roarPush 트리거)를 써서 밀어낸 뒤 빈 시간을 줄임
    public class BossRoarPattern : BossMeleePattern
    {
        [Title("Condition")]
        [Tooltip("사거리 안에 이 시간(초) 이상 머물러야 사용, 스쳐 지나가는 대상에게는 쓰지 않음")]
        [SerializeField, Min(0f)] private float minStayTime = 0.75f;

        [Title("Roar Hit")]
        [Tooltip("보스 중심 기준 원형 판정 반지름(m)")]
        [SerializeField, Min(0.1f)] private float radius = 6f;
        [Tooltip("예고 표시의 안쪽이 다 차오르는 데 걸리는 시간(초), 진입부터 OnRoarDrop까지의 시간에 맞춘다.")]
        [SerializeField, Min(0f)] private float warnDuration = 0.8f;
        [SerializeField, Min(0)] private int damage = 5;
        [Tooltip("피격 대상에게 줄 반응, 실제 넉백/넉다운 처리는 피격 대상 쪽에서 담당")]
        [SerializeField] private HitReaction reaction = HitReaction.Knockback;
        [Tooltip("피격 대상을 밀어내는 거리·높이·시간과 방향, 판정 중심은 보스 위치")]
        [SerializeField] private HitPush push = HitPush.Create(7f, 0.3f, 0.5f, HitPush.DirectionMode.FromHitCenter);
        [Tooltip("타격 후 범위 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float rangeFadeDuration = 0.3f;

        [Title("Follow Up")]
        [Tooltip("밀어낸 뒤 후딜 없이 바로 이어 갈 공격 후보, 밀려난 거리에서 닿는 Forward 공격을 넣는다.")]
        [SerializeField] private BossMeleePattern[] followPatterns;
        [Tooltip("포효한 뒤 밀려난 대상 쪽으로 도는 속도(도/초), 연계 공격의 각도 안에 대상을 두기 위함")]
        [SerializeField, Min(0f)] private float faceTurnSpeed = 300f;

        [Title("Effect")]
        [Tooltip("음파 연출, 비어 있으면 보스에서 찾고 없으면 생략")]
        [SerializeField] private BossRoarWave wave;

        private readonly List<BossMeleePattern> candidates = new();
        private readonly List<float> weights = new();

        private AttackTelegraph telegraph;
        private bool wasClose;
        private float closeSince;
        private bool hasRoared;
        private bool hasFollowUp;

        public override bool CanUse => base.CanUse && HasStayedClose();

        // 연계 공격을 남겼으면 밀려난 대상이 돌아오기 전에 바로 이어 가도록 후딜을 주지 않음
        public override float AppliedRecovery => hasFollowUp ? 0f : Recovery;

        protected override bool UsesMeleeHits => false;

        private TelegraphShape Shape => TelegraphShape.Circle(radius);

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            if (wave == null) wave = root.GetComponent<BossRoarWave>();
        }

        // 애니메이션 트리거는 인스펙터의 trigger(roarPush)로 BossMeleePattern이 재생
        protected override void OnPatternEnter()
        {
            hasRoared = false;
            hasFollowUp = false;
            telegraph = root.ShowTelegraph(Shape, GetPose(), warnDuration);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (hasRoared) root.RotateYaw(root.SignedAngleToTarget, faceTurnSpeed * Clock.LocalDeltaTime);
        }

        protected override void OnPatternExit()
        {
            // 포효 전에 패턴이 끊기면 예고만 남지 않도록 바로 지움
            if (telegraph != null) telegraph.Cancel();
            telegraph = null;

            // 밀어낸 경우에만 연계, Idle이 다음 판단에서 사거리를 다시 확인
            if (hasRoared && TryPickFollow(out BossMeleePattern follow))
            {
                hasFollowUp = true;
                root.SetFollowUp(follow);
            }
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (string.Equals(name, "OnRoarDrop")) Roar();
        }

        private void Roar()
        {
            Pose pose = GetPose();
            TryHit(Shape, pose, damage, reaction, push);

            if (telegraph != null) telegraph.Resolve(rangeFadeDuration);
            telegraph = null;

            if (wave != null) wave.Play(pose.position);
            hasRoared = true;
        }

        // 각도는 포효 뒤 대상 쪽으로 도는 중이라 거리만 보고 고름
        private bool TryPickFollow(out BossMeleePattern result)
        {
            result = null;
            if (followPatterns == null) return false;

            candidates.Clear();
            weights.Clear();

            foreach (BossMeleePattern pattern in followPatterns)
            {
                if (pattern == null || pattern.CanUse == false || pattern.IsDistanceInRange == false) continue;

                candidates.Add(pattern);
                weights.Add(pattern.Weight);
            }

            if (candidates.Count == 0) return false;

            result = candidates.GetWeightedRandom(weights);
            return true;
        }

        private Pose GetPose() => new Pose(root.transform.position, root.transform.rotation);

        // Idle이 생각할 때마다 묻는 값으로 사거리 안에 머문 시간을 잼
        // 다른 공격을 하는 동안은 묻지 않으므로, 그 사이 계속 붙어 있었다고 보고 멀어진 것을 본 때만 새로 잼
        // (붙어 있으면 다른 근접 대응 패턴이 먼저 나가 사이가 끊겨도 포효가 밀리지 않게 함)
        private bool HasStayedClose()
        {
            if (IsInRange == false)
            {
                wasClose = false;
                return false;
            }

            float now = Clock.LocalTime;
            if (wasClose == false)
            {
                wasClose = true;
                closeSince = now;
            }

            return now - closeSince >= minStayTime;
        }
    }
}
