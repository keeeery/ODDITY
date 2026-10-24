using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    public abstract class BossMeleePattern : BossStateBase
    {
        [Title("Attack Range")]
        [field: SerializeField] public float MinDistance { get; private set; }
        [field: SerializeField] public float MaxDistance { get; private set; }
        [SerializeField] private float minAngle;
        [SerializeField] private float maxAngle;
        
        [Title("Usage Condition")]
        [SerializeField] private float cooldown;
        [field: SerializeField] public float Weight { get; private set; }
        [field: SerializeField] public bool IsUrgent { get; private set; } // 우선 처리 여부
        [field: SerializeField] public bool CanApproach { get; private set; } // 범위 밖일 경우 근접하여 사용할지 여부

        [Title("Animation")]
        [SerializeField, ValueDropdown("GetTriggers")] private int trigger;
        [SerializeField] private bool useRootMotion;

        [Title("Tracking")]
        [SerializeField] private float trackDuration;
        [SerializeField] private float trackSpeed = 90f;

        [Title("Delay")]
        [field: SerializeField] public float Recovery { get; private set; } // 공격 후딜

        private float readyTime;
        private float trackEndTime;

        public virtual bool CanUse => Time.time >= readyTime;
        
        public bool IsTooClose => root.DistanceToTarget < MinDistance;
        public bool IsDistanceInRange => root.DistanceToTarget >= MinDistance && root.DistanceToTarget <= MaxDistance;
        public bool IsInRange => IsDistanceInRange && IsAngleInside(root.SignedAngleToTarget);
        
        
        private float AngleWidth => minAngle <= maxAngle ? maxAngle - minAngle : maxAngle - minAngle + 360f;
        
        
        protected override void OnEnter()
        {
            readyTime = Time.time + cooldown;
            trackEndTime = Time.time + trackDuration;

            root.AnimEventListener.OnOccursAnimStateEvent += OnOccursAnimEvent;
            
            Animator.applyRootMotion = useRootMotion;
            Animator.SetTrigger(trigger);

            OnPatternEnter();
        }

        protected virtual void Update()
        {
            if (Time.time >= trackEndTime) return;
            root.RotateYaw(root.SignedAngleToTarget, trackSpeed * Time.deltaTime);
        }

        protected override void OnExit()
        {
            root.AnimEventListener.OnOccursAnimStateEvent -= OnOccursAnimEvent;

            Animator.applyRootMotion = false;
            Animator.ResetTrigger(trigger);

            OnPatternExit();
        }

        protected virtual void OnPatternEnter() { }
        protected virtual void OnPatternExit() { }
        protected virtual void OnPatternAnimEventOccurs(string name, string param) { }

        private void OnOccursAnimEvent(string name, string param)
        {
            if (string.Equals(name, "OnTrackEnd"))
            {
                trackEndTime = 0f;
                return;
            }
            
            OnPatternAnimEventOccurs(name, param);
        }

        private bool IsAngleInside(float angle)
        {
            return minAngle <= maxAngle ? 
                angle >= minAngle && angle <= maxAngle : 
                angle >= minAngle || angle <= maxAngle;
        }

        public float ClampAngle(float angle, float margin)
        {
            if (IsAngleInside(angle)) return angle;

            float lower = Mathf.DeltaAngle(0f, minAngle + margin);
            float upper = Mathf.DeltaAngle(0f, maxAngle - margin);

            float lowerAngleDistance = Mathf.Abs(Mathf.DeltaAngle(angle, lower));
            float upperAngleDistance = Mathf.Abs(Mathf.DeltaAngle(angle, upper));
            
            return lowerAngleDistance <= upperAngleDistance ? lower : upper;
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

        // 거리 판정이 FlatDistanceTo(XZ 평면)이므로 바닥에 부채꼴로 표시
        private void DrawRange()
        {
            Transform center = root != null ? root.transform : GetComponentInParent<BossMonster>(true)?.transform;
            if (center == null) center = transform;

            Vector3 position = center.position;
            Vector3 forward = Quaternion.Euler(0f, center.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 from = Quaternion.AngleAxis(minAngle, Vector3.up) * forward;
            float width = AngleWidth;
            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
            UnityEditor.Handles.DrawSolidArc(position, Vector3.up, from, width, MaxDistance);

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireArc(position, Vector3.up, from, width, MaxDistance);
            if (MinDistance > 0f) UnityEditor.Handles.DrawWireArc(position, Vector3.up, from, width, MinDistance);

            if (width < 360f)
            {
                Vector3 to = Quaternion.AngleAxis(minAngle + width, Vector3.up) * forward;
                UnityEditor.Handles.DrawLine(position + from * MinDistance, position + from * MaxDistance);
                UnityEditor.Handles.DrawLine(position + to * MinDistance, position + to * MaxDistance);
            }

            Vector3 labelDirection = Quaternion.AngleAxis(minAngle + width * 0.5f, Vector3.up) * forward;
            UnityEditor.Handles.Label(position + labelDirection * MaxDistance,
                $"{GetType().Name} ({MinDistance}~{MaxDistance}m, {minAngle}°~{maxAngle}°)");

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