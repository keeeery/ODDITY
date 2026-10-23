using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 일단 공격을 먼저 정함
// 해당 공격 상태의 진입 상태에서, 거리를 판단
// 거리가 충분하면 제자리에서 공격 모션, 그 공격 거리는 애매한데 forward 공격 모션이 닿을 거리면 그거로 모션
// 그 거리도 만약 안됀다면 추격 -> 공격으로 이어져야 함
// 그렇다면, StateMachine에 상태를 예약할 수 있게 한다면?

namespace TeamOdd.Boss
{
    public class BossLeftClawPattern : BossStateBase
    {
        [Title("Settings")]
        [SerializeField] private float attackDistance;
        [SerializeField] private float forwardAttackDistance;
        
        protected override void OnEnter()
        {
            if (root.IsTargetInRange(attackDistance))
            {
                root.Animator.SetTrigger(BossAnimParams.LEFT_CLAW_ATTACK);
                return;
            }

            if (root.IsTargetInRange(forwardAttackDistance))
            {
                root.Animator.applyRootMotion = true;
                root.Animator.SetTrigger(BossAnimParams.LEFT_CLAW_ATTACK);
                return;
            }
            
            // 만약 사정거리 밖에 있다면, 다음 상태를 현재 상태로 예약 걸고, 추격 상태로(예약 무시) 변경
            StateMachine.ReserveState(GetType());
            StateMachine.ChangeState(typeof(BossChaseState), true);
        }
        
        protected override void OnExit()
        {
            Vector3 delta = root.Root.transform.localPosition;
            root.Animator.applyRootMotion = false;
            root.transform.position += delta;
        }
        
        #region 범위 시각화

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 에디트 모드에서는 Initialize가 호출되지 않아 root가 비어있으므로 직접 찾아옴
            Transform center = root != null ? root.transform : GetComponentInParent<BossMonster>()?.transform;
            if (center == null) center = transform;

            // 거리 판정이 FlatDistanceTo(XZ 평면)이므로 바닥 원으로 표시
            Vector3 position = center.position;
            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.up, attackDistance);
            UnityEditor.Handles.Label(position + center.forward * attackDistance, $"Attack ({attackDistance})");

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.up, forwardAttackDistance);
            UnityEditor.Handles.Label(position + center.forward * forwardAttackDistance, $"Forward Attack ({forwardAttackDistance})");

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}