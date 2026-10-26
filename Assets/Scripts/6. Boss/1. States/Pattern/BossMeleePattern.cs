using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyGarden.FSM;
using FantasyGarden.Utility;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 공격 패턴(근접, 투척, 투하, 충격파)의 공통 베이스
    // - 사용 조건(사거리/각도/내부쿨/가중치), 애니메이션 트리거, 회전 추적, 후딜, 피해 전달을 담당
    // - 하위 클래스는 OnPatternEnter/OnPatternExit/OnPatternAnimEventOccurs로 패턴별 동작을 구현

    // 회전 추적: trackDuration 동안 대상 쪽으로 회전, OnTrackEnd 신호나 고정 예고가 뜨면 멈춤
    // 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)

    // 근접 타격(hits): 범위 예고는 OnHitWarn 신호, 타격 시점은 OnHit 신호(둘 다 매개변수는 hits 인덱스, 비어 있으면 0)
    // - OnClawWarn/OnClawHit 신호는 인덱스 0의 OnHitWarn/OnHit으로 취급
    // - 예고는 보스 기준 위치에 표시하고 타격 시점까지 보스를 따라감
    // - lockOnWarn을 켠 타격은 예고 순간 이동 후 위치를 예측해 고정(루트 모션으로 앞으로 내딛는 공격 대응)
    // - 예고 신호가 없으면 타격 시점에만 범위를 잠깐 표시, 두 신호를 쓰지 않는 패턴은 표시하지 않음
    // - 피격 판정은 타격 신호 순간 예고와 같은 모양으로 한 번만 검사, 무적 같은 피격 예외는 피격 대상 쪽에서 판단
    public abstract class BossMeleePattern : BossStateBase
    {
        [Serializable]
        protected class MeleeHit
        {
            [Tooltip("공격 범위, 피격 판정과 예고 표시가 함께 사용한다.")]
            public TelegraphShape shape = TelegraphShape.Sector(8f, 120f);
            [Tooltip("보스 위치 기준 범위 시작점의 오프셋(m), x는 오른쪽, y는 앞쪽. 범위는 보스 정면 방향으로 펼쳐진다.")]
            public Vector2 offset;
            [Tooltip("예고 표시의 안쪽이 다 차오르는 데 걸리는 시간(초), OnHitWarn에서 OnHit까지의 시간에 맞춘다.")]
            [Min(0f)] public float warnDuration = 0.5f;
            [Min(0)] public int damage = 10;
            [Tooltip("피격 대상에게 줄 반응, 실제 경직/다운 처리는 피격 대상 쪽에서 담당")]
            public HitReaction reaction = HitReaction.Stagger;
            [Tooltip("켜면 예고 순간 범위를 고정하고 보스 회전 추적도 멈춘다. 루트 모션으로 이동하며 때리는 공격에 사용한다.")]
            public bool lockOnWarn;
            [Tooltip("예고부터 타격까지 루트 모션으로 보스가 이동하는 거리(m), x는 오른쪽, y는 앞쪽. 고정할 범위를 이만큼 옮겨 둔다.")]
            [ShowIf(nameof(lockOnWarn))] public Vector2 moveLead;
        }

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

        [Title("Melee Hits"), ShowIf(nameof(UsesMeleeHits))]
        [Tooltip("타격 목록, 콤보처럼 여러 번 때리면 신호 매개변수로 인덱스를 지정한다.")]
        [SerializeField] private MeleeHit[] hits = { new MeleeHit() };
        [Tooltip("타격 후 범위 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f), ShowIf(nameof(UsesMeleeHits))] private float hitFadeDuration = 0.3f;

        private float readyTime;
        private float trackEndTime;
        private float targetRadius;
        // hits와 같은 인덱스의 예고 표시, 예고 순간 고정한 범위 위치
        private AttackTelegraph[] hitTelegraphs;
        private Pose[] lockedPoses;
        private bool[] isLocked;

        public virtual bool CanUse => Clock.LocalTime >= readyTime;

        // 근접 타격 신호를 쓰지 않는 패턴(투척, 투하, 충격파)은 false로 두어 인스펙터에서 타격 목록을 숨김
        protected virtual bool UsesMeleeHits => true;
        
        public bool IsTooClose => root.DistanceToTarget < MinDistance;
        public bool IsDistanceInRange => root.DistanceToTarget >= MinDistance && root.DistanceToTarget <= MaxDistance;
        public bool IsInRange => IsDistanceInRange && IsAngleInside(root.SignedAngleToTarget);
        
        
        private float AngleWidth => minAngle <= maxAngle ? maxAngle - minAngle : maxAngle - minAngle + 360f;


        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            hitTelegraphs = new AttackTelegraph[hits.Length];
            lockedPoses = new Pose[hits.Length];
            isLocked = new bool[hits.Length];
        }

        protected override void OnEnter()
        {
            readyTime = Clock.LocalTime + cooldown;
            trackEndTime = Clock.LocalTime + trackDuration;
            targetRadius = GetTargetRadius();

            root.AnimEventListener.OnOccursAnimStateEvent += OnOccursAnimEvent;
            
            Animator.applyRootMotion = useRootMotion;
            Animator.SetTrigger(trigger);

            OnPatternEnter();
        }

        protected override void OnUpdate()
        {
            // 고정하지 않은 예고는 타격 시점까지 보스를 따라감
            for (int index = 0; index < hits.Length; index++)
                if (hitTelegraphs[index] != null && isLocked[index] == false)
                    hitTelegraphs[index].SetPlacement(hits[index].shape, GetHitPose(hits[index], Vector2.zero));

            if (Clock.LocalTime >= trackEndTime) return;
            root.RotateYaw(root.SignedAngleToTarget, trackSpeed * Clock.LocalDeltaTime);
        }

        protected override void OnExit()
        {
            root.AnimEventListener.OnOccursAnimStateEvent -= OnOccursAnimEvent;

            Animator.applyRootMotion = false;
            Animator.ResetTrigger(trigger);

            // 타격 전에 패턴이 끊기면 예고만 남지 않도록 바로 지움, 이미 타격한 표시는 끝까지 사라지게 둠
            for (int index = 0; index < hits.Length; index++)
            {
                if (hitTelegraphs[index] != null) hitTelegraphs[index].Cancel();
                hitTelegraphs[index] = null;
                isLocked[index] = false;
            }

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

            if (name == "OnHitWarn" || name == "OnClawWarn")
            {
                if (TryGetHitIndex(name, param, out int index)) WarnHit(index);
                return;
            }

            if (name == "OnHit" || name == "OnClawHit")
            {
                if (TryGetHitIndex(name, param, out int index)) ResolveHit(index);
                return;
            }

            OnPatternAnimEventOccurs(name, param);
        }

        protected static int ParseIndex(string param) => string.IsNullOrEmpty(param) ? 0 : int.Parse(param);

        private bool TryGetHitIndex(string name, string param, out int index)
        {
            index = name == "OnClawWarn" || name == "OnClawHit" ? 0 : ParseIndex(param);
            if (index >= 0 && index < hits.Length) return true;

            Debug.LogWarning($"{GetType().Name}: {name} 신호의 타격 인덱스 {index}가 hits 범위(0~{hits.Length - 1})를 벗어났습니다.", this);
            return false;
        }

        // 범위 시작점은 보스 기준 오프셋 위치(+ 이동 예측), 방향은 보스 정면
        private Pose GetHitPose(MeleeHit hit, Vector2 lead)
        {
            Transform boss = root.transform;
            Vector2 local = hit.offset + lead;
            Vector3 position = boss.position + boss.rotation * new Vector3(local.x, 0f, local.y);
            return new Pose(position, boss.rotation);
        }

        // 고정하는 타격은 타격 시점의 보스 위치를 예측해 그 자리에 범위를 두고, 보이는 방향과 맞도록 추적을 멈춤
        private void WarnHit(int index)
        {
            MeleeHit hit = hits[index];
            Pose pose = GetHitPose(hit, hit.lockOnWarn ? hit.moveLead : Vector2.zero);

            if (hit.lockOnWarn)
            {
                lockedPoses[index] = pose;
                isLocked[index] = true;
                trackEndTime = 0f;
            }

            if (hitTelegraphs[index] != null) hitTelegraphs[index].Cancel();
            hitTelegraphs[index] = root.ShowTelegraph(hit.shape, pose, hit.warnDuration);
        }

        // 고정한 타격은 실제 이동량과 달라도 예고한 위치로 판정
        private void ResolveHit(int index)
        {
            MeleeHit hit = hits[index];
            Pose pose = isLocked[index] ? lockedPoses[index] : GetHitPose(hit, Vector2.zero);
            isLocked[index] = false;
            
            Player.Player.Instance.PlayerController.ApplyKnockback(root.transform.position, hit.reaction, hit.shape.radius);
            Debug.Log("resolve");

            TryHit(hit.shape, pose, hit.damage, hit.reaction);

            // 예고 없이 타격했으면 타격 순간에만 범위를 보여 줌
            AttackTelegraph resolved = hitTelegraphs[index] != null ? hitTelegraphs[index] : root.ShowTelegraph(hit.shape, pose, 0f);
            hitTelegraphs[index] = null;
            if (resolved != null) resolved.Resolve(hitFadeDuration);
        }

        // 대상 몸통 반경만큼 범위를 넓혀 판정, 대상이 범위 끝에 걸쳐 있어도 맞음
        protected bool TryHit(TelegraphShape shape, Pose pose, int damage, HitReaction reaction)
        {
            if (shape.Contains(pose, Target.position, targetRadius) == false) return false;

            Debug.Log("try");
            SendDamage(damage, reaction, Target.position);
            return true;
        }

        // 피해를 받을 수 있는 대상일 때만 전달, 보낸 쪽은 보스 전용 피격 처리(ICombatable)를 정리할 때까지 비워 둠
        // 반응은 피해 정보에 담아 보내고, 경직/다운 처리는 피격 대상이 담당
        protected void SendDamage(int damage, HitReaction reaction, Vector3 hitPoint)
        {
            Debug.Log($"[{GetType().Name}] 피격, 피해 {damage}, 반응 {reaction}", this);

            ICombatable receiver = Target.GetComponentInParent<ICombatable>();
            if (receiver == null) return;

            DamageInfo info = new DamageInfo(damage, hitPoint, receiver.Collider, reaction);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(null, receiver, info));
        }

        // CharacterController처럼 세워진 몸통 기준, 수평 크기 중 작은 쪽 절반을 반경으로 사용
        private float GetTargetRadius()
        {
            if (Target.TryGetComponent(out Collider targetCollider) == false) return 0f;

            Vector3 extents = targetCollider.bounds.extents;
            return Mathf.Min(extents.x, extents.z);
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

        // 거리 판정이 FlatDistanceTo(XZ 평면)이므로 사용 사거리는 바닥에 부채꼴(빨강)로, 타격 범위는 주황으로 표시
        // 타격 범위는 현재 보스 위치 기준이며, lockOnWarn의 moveLead만큼 옮겨진 위치는 그리지 않음
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

            if (UsesMeleeHits)
            {
                UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);

                foreach (MeleeHit hit in hits)
                {
                    Vector3 start = position + center.rotation * new Vector3(hit.offset.x, 0f, hit.offset.y);
                    hit.shape.DrawHandles(new Pose(start, center.rotation));
                }
            }

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
