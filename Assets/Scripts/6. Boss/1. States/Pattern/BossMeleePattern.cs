using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    public interface IBossPattern
    {
        public float RequiredRange { get; }
        public float RequiredHalfAngle { get; }
        public float Weight { get; }
        public bool CanUse { get; }
    }
    
    public abstract class BossMeleePattern : BossStateBase, IBossPattern
    {
        [Title("Settings")]
        [SerializeField, ValueDropdown("GetTriggers")] private int trigger;
        [SerializeField] private float attackRange;
        [SerializeField] private float forwardAttackRange;
        
        [field: SerializeField] public float RequiredHalfAngle { get; private set; }
        [field: SerializeField] public float Weight { get; private set; }

        public float RequiredRange => Mathf.Max(attackRange, forwardAttackRange);
        public virtual bool CanUse => true;

        protected override void OnEnter()
        {
            if (root.IsTargetInRange(attackRange))
            {
                Animator.SetTrigger(trigger);
                return;
            }

            if (root.IsTargetInRange(forwardAttackRange))
            {
                Animator.applyRootMotion = true;
                Animator.SetTrigger(trigger);
                return;
            }

            StateMachine.ReserveState(this);
            StateMachine.ChangeState(typeof(BossChaseState), true);
        }

        protected override void OnExit()
        {
            Animator.applyRootMotion = false;
        }
        
        #region RANGE_VISUALIZATION

#if UNITY_EDITOR
        [Title("Debug")]
        [SerializeField] private bool showRangeWhenInactive = true;

        private void OnDrawGizmos() => DrawRange();

        // OnDrawGizmos는 비활성화된 오브젝트에선 호출되지 않으므로 씬 뷰 콜백에서 직접 그려줌
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInactiveRangeDrawer()
        {
            UnityEditor.SceneView.duringSceneGui += sceneView =>
            {
                if (Event.current.type != EventType.Repaint || sceneView.drawGizmos == false) return;

                BossMeleePattern[] patterns = FindObjectsByType<BossMeleePattern>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (BossMeleePattern pattern in patterns)
                {
                    if (pattern.showRangeWhenInactive == false || pattern.isActiveAndEnabled) continue;
                    if (UnityEditor.Selection.Contains(pattern.gameObject) == false) continue;
                    pattern.DrawRange();
                }
            };
        }

        private void DrawRange()
        {
            // 에디트 모드에서는 Initialize가 호출되지 않아 root가 비어있으므로 직접 찾아옴
            Transform center = root != null ? root.transform : GetComponentInParent<BossMonster>(true)?.transform;
            if (center == null) center = transform;

            // 거리 판정이 FlatDistanceTo(XZ 평면)이므로 바닥 원으로 표시
            Vector3 position = center.position;
            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.up, attackRange);
            UnityEditor.Handles.Label(position + center.forward * attackRange, $"Attack ({attackRange})");

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.up, forwardAttackRange);
            UnityEditor.Handles.Label(position + center.forward * forwardAttackRange, $"Forward Attack ({forwardAttackRange})");

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion

        #region ODIN_INSPECTOR

#if UNITY_EDITOR

        private IEnumerable GetTriggers()
        {
            Dictionary<string, int> triggers = typeof(BossAnimParams).GetValues<int>();
            return triggers.Select(pair => new ValueDropdownItem<int>(pair.Key, pair.Value));
        }
        
#endif

        #endregion
    }
}