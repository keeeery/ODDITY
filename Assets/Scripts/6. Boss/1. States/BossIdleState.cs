using System;
using System.Collections.Generic;
using FantasyGarden.Utility;
using NUnit.Framework;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

namespace TeamOdd.Boss
{
    public class BossIdleState : BossStateBase
    {
        [Title("Settings")]
        [SerializeField] private float intentTimeout;
        [SerializeField] private float retryDelay = 0.3f;
        [SerializeField] private float faceHalfAngle = 45f;

        [Title("Patterns")]
        [SerializeField] private BossMeleePattern[] melees;
        [SerializeField] private BossHopState hop; // 비어 있으면 점프로 자리 잡지 않음

        private readonly List<BossMeleePattern> candidates = new();
        private readonly List<float> weights = new();
        
        private float thinkTime;
        private BossMeleePattern executed;
        
        private bool isChasing; // 추적 중 재판단으로 Idle에 들어온 경우 true

        protected override void OnEnter()
        {
            // 공격을 끝내고 Idle로 진입 시에만 후딜 적용
            float recovery = executed != null ? GetRecovery(executed) : 0f;
            thinkTime = Clock.LocalTime + recovery;
            executed = null;

            if (recovery > 0f) OnRecoveryStarted(recovery);
        }

        protected override void OnUpdate()
        {
            if (Clock.LocalTime < thinkTime) return;
            if (root.IsTargetDead) return;
            Think();
        }

        protected override void OnExit() { }

        // 끊긴 패턴의 후딜을 다시 시작한 Idle에 적용하지 않음
        public override void ResetMemory() => executed = null;

        // 공격 후딜을 기다리기 시작할 때 불림, 후딜 동안 다른 행동(스트레이프 등)을 하는 보스가 재정의
        protected virtual void OnRecoveryStarted(float recovery) { }

        // 페이즈에 따라 후딜을 줄이는 보스가 재정의
        protected virtual float GetRecovery(BossMeleePattern pattern) => pattern.AppliedRecovery;

        // 페이즈가 바뀌어 고를 패턴 후보가 늘어날 때 사용
        protected void AddMelees(BossMeleePattern[] added)
        {
            BossMeleePattern[] merged = new BossMeleePattern[melees.Length + added.Length];
            melees.CopyTo(merged, 0);
            added.CopyTo(merged, melees.Length);
            melees = merged;
        }

        private void Think()
        {
            if (TryFollowUp()) return;

            if (IsIntentValid() == false) root.ClearIntent();

            if (TryHop()) return;

            if (TrySetInterrupt() == false)
            {
                if (TryPickWeighted(GetCandidates(), out BossMeleePattern melee))
                    root.SetIntent(melee);
            }

            BossMeleePattern intent = root.Intent;

            if (intent == null)
            {
                // 시야가 필요한 패턴이 모두 빠져 고를 게 없으면 제자리에 서 있지 않고 장애물을 돌아가 대상을 찾음
                if (root.HasLineOfSight == false)
                {
                    ChangeState(CanMove() ? typeof(BossChaseState) : typeof(BossTurnState));
                    return;
                }

                if (Mathf.Abs(root.SignedAngleToTarget) > faceHalfAngle)
                {
                    ChangeState(typeof(BossTurnState));
                    return;
                }

                thinkTime = Clock.LocalTime + retryDelay;
                return;
            }

            if (intent.IsInRange)
            {
                root.ClearIntent();
                
                executed = intent;
                ChangeState(intent.GetType());
                return;
            }

            if (intent.IsDistanceInRange || CanMove() == false)
            {
                ChangeState(typeof(BossTurnState));
                return;
            }
            
            ChangeState(typeof(BossChaseState));
        }

        // 이미 추적 중이었다면 더 넓은 각도까지 멈추지 않고 계속 추적
        private bool CanMove() => isChasing ? root.CanKeepMovingToTarget : root.CanMoveToTarget;

        // 점프로 자리를 잡고 돌아왔거나(Hop) 앞 패턴이 연계 공격을 남겼으면(포효 등) 골라 둔 공격을 바로 실행
        // 사거리를 벗어났으면 평소처럼 다시 판단, 장애물에 가려졌으면 시야가 필요한 공격은 쓰지 않음
        private bool TryFollowUp()
        {
            BossMeleePattern follow = hop != null ? hop.TakeFollow() : null;
            if (follow == null) follow = root.TakeFollowUp();
            if (follow == null || follow.IsInRange == false) return false;
            if (follow.IsSightClear == false) return false;

            executed = follow;
            ChangeState(follow.GetType());
            return true;
        }

        private bool TryHop()
        {
            if (hop == null || hop.ShouldHop() == false) return false;

            ChangeState(typeof(BossHopState));
            return true;
        }

        private bool IsIntentValid()
        {
            BossMeleePattern intent = root.Intent;

            if (intent == null) return false;
            if (Clock.LocalTime - root.IntentStartTime > intentTimeout) return false;
            if (intent.CanUse == false) return false;
            if (intent.IsTooClose) return false;
            if (intent.CanApproach == false && intent.IsDistanceInRange == false) return false;

            return true;
        }
        
        private bool TrySetInterrupt()
        {
            if (root.Intent == null) return false;
            if (root.Intent.IsUrgent) return false;

            foreach (BossMeleePattern melee in melees)
            {
                if (melee.IsUrgent == false) continue;
                if (melee.CanUse == false) continue;
                if (melee.IsInRange == false) continue;
                
                root.SetIntent(melee);
                return true;
            }

            return false;
        }

        private List<BossMeleePattern> GetCandidates()
        {
            candidates.Clear();

            bool hasUrgent = false;
            bool hasInRange = false;

            foreach (BossMeleePattern melee in melees)
            {
                if (melee.CanUse == false) continue;
                if (melee.IsTooClose) continue;
                if (hasUrgent && melee.IsUrgent == false) continue;

                if (melee.IsUrgent && hasUrgent == false) // 이 부분 설명 필요
                {
                    hasUrgent = true;
                    hasInRange = false;
                    candidates.Clear();
                }

                bool inRange = melee.IsInRange;

                if (hasInRange && inRange == false) continue;
                if (inRange && hasInRange == false) // 이 부분 설명 필요
                {
                    hasInRange = true;
                    candidates.Clear();
                }

                candidates.Add(melee);
            }

            return candidates;
        }

        private bool TryPickWeighted(List<BossMeleePattern> candidates, out BossMeleePattern result)
        {
            result = null;
            if (candidates.IsNullOrEmpty()) return false;

            weights.Clear();
            foreach (BossMeleePattern item in candidates)
                weights.Add(item.Weight);

            result = candidates.GetWeightedRandom(weights);
            return true;
        }

        // 다음 상태를 바꿔 들어가는 보스(돌면서 바로 출발 등)가 재정의
        protected virtual void ChangeState(Type nextState)
        {
            isChasing = nextState == typeof(BossChaseState);

            if (isChasing == false)
                Animator.SetBool(BossAnimParams.IS_CHASING, false);
            
            root.ChangeBossState(nextState);
        }
    }
}