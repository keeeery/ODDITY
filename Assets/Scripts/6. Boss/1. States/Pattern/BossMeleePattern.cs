using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    public interface IRangedPattern
    {
        public float RequiredRange { get; }
    }
    
    // 범위 예고: OnClawWarn 신호, 공격 시점: OnClawHit 신호, 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)
    // 예고는 보스 앞쪽 부채꼴로 표시하고 공격 시점까지 보스를 따라감(앞으로 내딛는 공격 대응)
    // 예고 신호가 없으면 공격 시점에만 범위를 잠깐 표시
    public abstract class BossMeleePattern : BossStateBase, IRangedPattern
    {
        [Title("Settings")]
        [SerializeField, ValueDropdown("GetTriggers")] private int trigger;
        [SerializeField] private float attackRange;
        [SerializeField] private float forwardAttackRange;

        [Title("Telegraph")]
        [Tooltip("공격 범위, 보스 위치에서 보스 정면 방향으로 펼쳐진다. 피격 판정을 붙일 때도 이 모양을 사용한다.")]
        [SerializeField] private TelegraphShape hitShape = TelegraphShape.Sector(8f, 120f);
        [Tooltip("예고 표시의 안쪽이 다 차오르는 데 걸리는 시간(초), OnClawWarn에서 OnClawHit까지의 시간에 맞춘다.")]
        [SerializeField, Min(0f)] private float warnDuration = 0.5f;
        [Tooltip("공격 후 범위 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float telegraphFadeDuration = 0.3f;

        private AttackTelegraph telegraph;

        public float RequiredRange => Mathf.Max(attackRange, forwardAttackRange);

        private Pose HitPose => new Pose(root.transform.position, root.transform.rotation);

        protected override void OnEnter()
        {
            if (root.IsTargetInRange(attackRange))
            {
                StartAttack();
                return;
            }

            if (root.IsTargetInRange(forwardAttackRange))
            {
                Animator.applyRootMotion = true;
                StartAttack();
                return;
            }

            StateMachine.ReserveState(this);
            StateMachine.ChangeState(typeof(BossChaseState), true);
        }

        protected override void OnExit()
        {
            Animator.applyRootMotion = false;
            root.AnimEventListener.OnOccursAnimStateEvent -= OnAnimEvent;

            if (telegraph != null) telegraph.Cancel();
            telegraph = null;
        }

        // 공격 시점까지 보스를 따라감
        private void Update()
        {
            if (telegraph != null) telegraph.SetPlacement(hitShape, HitPose);
        }

        private void StartAttack()
        {
            root.AnimEventListener.OnOccursAnimStateEvent += OnAnimEvent;
            Animator.SetTrigger(trigger);
        }

        private void OnAnimEvent(string eventName, string eventParam)
        {
            if (eventName == "OnClawWarn")
            {
                if (telegraph != null) telegraph.Cancel();
                telegraph = root.ShowTelegraph(hitShape, HitPose, warnDuration);
                return;
            }

            if (eventName != "OnClawHit") return;

            // TODO: 피격 판정은 표시와 같은 hitShape.Contains(HitPose, Target.position)로 처리
            AttackTelegraph resolved = telegraph != null ? telegraph : root.ShowTelegraph(hitShape, HitPose, 0f);
            telegraph = null;
            if (resolved != null) resolved.Resolve(telegraphFadeDuration);
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

            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);
            hitShape.DrawHandles(new Pose(position, center.rotation));

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