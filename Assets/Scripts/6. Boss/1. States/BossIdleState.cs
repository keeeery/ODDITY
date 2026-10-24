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

        private readonly List<BossMeleePattern> candidates = new();
        private readonly List<float> weights = new();
        
        private float thinkTime;
        private BossMeleePattern executed;
        
        protected override void OnEnter()
        {
            // 공격을 끝내고 Idle로 진입 시에만 후딜 적용
            thinkTime = Time.time + (executed != null ? executed.Recovery : 0f);
            executed = null;
        }

        private void Update()
        {
            if (Time.time < thinkTime) return;
            Think();
        }

        protected override void OnExit() { }

        private void Think()
        {
            if (IsIntentValid() == false) root.ClearIntent();

            if (TrySetInterrupt() == false)
            {
                if (TryPickWeighted(GetCandidates(), out BossMeleePattern melee))
                    root.SetIntent(melee);
            }

            BossMeleePattern intent = root.Intent;

            if (intent == null)
            {
                if (Mathf.Abs(root.SignedAngleToTarget) > faceHalfAngle)
                {
                    ChangeState(typeof(BossTurnState));
                    return;
                }

                thinkTime = Time.time + retryDelay;
                return;
            }

            if (intent.IsInRange)
            {
                root.ClearIntent();
                
                executed = intent;
                ChangeState(intent.GetType());
                return;
            }

            if (intent.IsDistanceInRange || root.CanMoveToTarget == false)
            {
                ChangeState(typeof(BossTurnState));
                return;
            }
            
            ChangeState(typeof(BossChaseState));
        }

        private bool IsIntentValid()
        {
            BossMeleePattern intent = root.Intent;

            if (intent == null) return false;
            if (Time.time - root.IntentStartTime > intentTimeout) return false;
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

        private void ChangeState(Type nextState)
        {
            if (nextState != typeof(BossChaseState))
                Animator.SetBool(BossAnimParams.IS_CHASING, false);
            
            StateMachine.ChangeState(nextState);
        }
    }
}