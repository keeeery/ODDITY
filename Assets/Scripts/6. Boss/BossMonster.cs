using System;
using System.Collections.Generic;
using TeamOdd.Player;
using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;
using Study.Utilities;
using Study3D.FpsGame;
using TeamOdd.TimeControl;

namespace TeamOdd.Boss
{
    public class BossMonster : MonoBehaviourFSM
    {
        [field: Title("Components")]
        [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public Rigidbody Body { get; private set; }
        [field: SerializeField] public BossMovement Movement { get; private set; }
        [field: SerializeField] public BossPathfinder Pathfinder { get; private set; } // 비어 있으면 장애물을 돌아가지 않고 대상을 바로 향함
        [field: SerializeField] public AnimStateEventListener AnimEventListener { get; private set; }

        [field: Title("References")]
        [field: SerializeField] public Transform Target { get; private set; } // 임시용

        [TitleGroup("Settings")]
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float sightHalfAngle = 60f;
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float sightRange = 10f;
        [SerializeField, TabGroup("Settings/Type", "Sight")] private LayerMask sightBlockMask; // 대상을 가리는 레이어 (벽, 장애물)
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float eyeHeight = 3f; // 보스 위치에서 시야 판정 시작점까지의 높이
        [SerializeField, TabGroup("Settings/Type", "Movement")] private float moveHalfAngle = 45f;
        [SerializeField, TabGroup("Settings/Type", "Movement")] private float keepMoveHalfAngle = 90f; // 이미 이동 중일 때 계속 이동할 수 있는 각도
        [SerializeField, TabGroup("Settings/Type", "Motion")] private float motionSpeed = 1f;
        
        [Title("States")]
        [ShowInInspector, ReadOnly] public float IntentStartTime { get; private set; }
        [ShowInInspector, ReadOnly] public BossMeleePattern Intent { get; private set; }
        [ShowInInspector, ReadOnly] private BossMeleePattern followUp; // 앞 패턴이 끝나자마자 이어 갈 공격(포효 후 연계 등)

        [Title("Telegraph")]
        [Tooltip("패턴들이 공유하는 피격 범위 표시(AttackTelegraph) 풀의 키, 풀은 ObjectPoolManager에 등록")]
        [SerializeField] private string telegraphPoolKey = "AttackTelegraph";

        private bool isTelegraphPoolMissing;

        // 부르는 타입 → 실제로 붙어 있는 상태 타입
        private readonly Dictionary<Type, Type> stateTypes = new();

        private Collider targetCollider;
        private bool hasLineOfSight = false;
        private int sightFrame = -1;

        // 시간 정지 스킬이 보스를 멈출 때 쓰는 시계, 프리팹에 없으면 Awake에서 붙임
        public TimeFreezable Clock { get; private set; }

        // 대상이 죽으면 Target은 그대로 두고(거리·각도 계산이 계속 쓰므로) 새 판단만 멈춤
        public bool IsTargetDead => Player.Player.Instance.PlayerController.IsDead;

        public float DistanceToTarget =>transform.FlatDistanceTo(Target);
        public float SignedAngleToTarget => Mathf.DeltaAngle(Movement.Rotation.eulerAngles.y, transform.FlatRotationTo(Target).eulerAngles.y);

        // 걸어서 대상에게 갈 때 지금 향해야 할 지점, 장애물이 가로막으면 대상 대신 경로의 다음 꺾임점
        public Vector3 SteerPoint => Pathfinder != null ? Pathfinder.GetSteerPoint(Movement.Position, Target.position) : Target.position;
        public float SignedAngleToSteer => SignedAngleTo(SteerPoint);

        // 대상까지 NavMesh 위로 막힘 없이 걸어갈 수 있는지(구멍·난간 너머가 아닌지), 경로를 쓰지 않으면 항상 true
        // 경로는 SteerPoint를 물을 때 갱신되므로 먼저 물어 최신 판정을 씀
        public bool HasDirectPath
        {
            get
            {
                if (Pathfinder == null) return true;
                _ = SteerPoint;
                return Pathfinder.IsDirect;
            }
        }

        public bool IsTargetOnRight => SignedAngleToTarget >= 0f;

        // 장애물을 돌아가는 중엔 대상이 아닌 경로 방향으로 걸으므로 이동 가능 각도도 경로 기준
        public bool CanMoveToTarget => Mathf.Abs(SignedAngleToSteer) <= moveHalfAngle;
        public bool CanKeepMovingToTarget => Mathf.Abs(SignedAngleToSteer) <= keepMoveHalfAngle;

        // 장애물에 가려지지 않고 대상이 보이는지, 패턴마다 여러 번 물어도 판정은 프레임당 한 번만 함
        public bool HasLineOfSight
        {
            get
            {
                if (sightFrame != Time.frameCount)
                {
                    sightFrame = Time.frameCount;
                    hasLineOfSight = CheckLineOfSight();
                }

                return hasLineOfSight;
            }
        }

        protected override void Awake()
        {
            Clock = TimeFreezable.GetOrAdd(gameObject);
            Target = Player.Player.Instance.transform;
            Target.TryGetComponent(out targetCollider);
            base.Awake();
            MapStateTypes();
            Animator.speed = motionSpeed;
        }

        private void Update()
        {
            Animator.SetFloat(BossAnimParams.DISTANCE_TO_TARGET, DistanceToTarget);
        }

        // Animator가 루트 모션을 transform에 바로 적용하면 벽을 통과하므로 BossMovement를 거쳐 적용
        private void OnAnimatorMove()
        {
            if (Animator.applyRootMotion == false) return;

            Movement.Move(Animator.deltaPosition);
            Movement.Rotate(Mathf.DeltaAngle(0f, Animator.deltaRotation.eulerAngles.y));
        }

        #region PUBLIC_METHODS

        // 페이즈 전환 등으로 보스 전체 동작 빠르기를 바꿈
        // 시간 정지·감속이 기억해 둔 속도로 풀 때 되돌아가므로 정상 시간(Clock.TimeScale 1)일 때 호출
        public void SetMotionSpeed(float speed)
        {
            motionSpeed = speed;
            Animator.speed = speed;
        }

        // 공용 FSM은 실제 타입으로만 상태를 찾으므로, 부모 타입으로 불러도 상속한 상태(HumanoidIdleState 등)로 바꿔 넘김
        public void ChangeBossState(Type nextState, bool ignoreReservedState = false)
        {
            Type stateType = stateTypes.TryGetValue(nextState, out Type mapped) ? mapped : nextState;
            ChangeState(stateType, ignoreReservedState);
        }

        public bool IsTargetInRange(float range) => DistanceToTarget <= range;
        public bool IsTargetInSight() => IsTargetInRange(sightRange) && Mathf.Abs(SignedAngleToTarget) <= sightHalfAngle;
        
        public void SetIntent(BossMeleePattern intent)
        {
            Intent = intent;
            IntentStartTime = Clock.LocalTime;
        }
        
        public void ClearIntent() => Intent = null;

        // Idle이 다음 판단에서 한 번만 꺼내 씀, intent와 달리 다시 고르지 않고 사거리 안이면 바로 실행
        public void SetFollowUp(BossMeleePattern pattern) => followUp = pattern;

        public BossMeleePattern TakeFollowUp()
        {
            BossMeleePattern result = followUp;
            followUp = null;
            return result;
        }

        // 이전 판단(의도·연계·예약 상태·상태별 기억)을 모두 버리고 기본 상태부터 새로 시작
        public override void Restart()
        {
            ClearIntent();
            followUp = null;

            foreach (BossStateBase state in GetComponentsInChildren<BossStateBase>(true))
                state.ResetMemory();

            base.Restart();
        }

        public void RotateYaw(float deltaAngle, float maxDegrees)
        {
            float step = Mathf.Clamp(deltaAngle, -maxDegrees, maxDegrees);
            Movement.Rotate(step);
        }
        
        // 풀을 쓸 수 없으면 null을 반환하므로 호출하는 쪽은 null을 허용해야 함
        // 풀이 없으면 한 번만 알리고 이후에는 표시 없이 진행
        // clock을 넘기지 않으면 보스의 시계를 따라 차오름, 낙하물처럼 보스와 따로 멈추는 대상의 예고는 그 대상의 시계를 넘김
        public AttackTelegraph ShowTelegraph(TelegraphShape shape, Pose origin, float warnDuration, TimeFreezable clock = null)
        {
            if (isTelegraphPoolMissing) return null;

            AttackTelegraph telegraph = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.GetObjectOrNull(telegraphPoolKey) as AttackTelegraph
                : null;

            if (telegraph == null)
            {
                Debug.LogWarning($"{name}: ObjectPoolManager에 '{telegraphPoolKey}' 키의 AttackTelegraph 풀이 없어 피격 범위를 표시하지 않습니다.", this);
                isTelegraphPoolMissing = true;
                return null;
            }

            telegraph.Show(shape, origin, warnDuration, clock != null ? clock : Clock);
            return telegraph;
        }

        #endregion

        // 실제 타입을 먼저 다 넣고, 그 타입의 상태가 따로 없을 때만 부모 타입을 상속한 상태에 연결 (페르데로스처럼 부모 상태가 직접 있으면 그대로)
        private void MapStateTypes()
        {
            BossStateBase[] states = GetComponentsInChildren<BossStateBase>(true);

            foreach (BossStateBase state in states)
                stateTypes[state.GetType()] = state.GetType();

            foreach (BossStateBase state in states)
            {
                for (Type type = state.GetType().BaseType; type != typeof(BossStateBase); type = type.BaseType)
                    stateTypes.TryAdd(type, state.GetType());
            }
        }

        private float SignedAngleTo(Vector3 point)
        {
            Vector3 dir = transform.FlatDirectionTo(point);
            if (dir == Vector3.zero) return 0f;

            return Mathf.DeltaAngle(Movement.Rotation.eulerAngles.y, Quaternion.LookRotation(dir).eulerAngles.y);
        }

        // 발끝은 낮은 턱에도 가려지므로 몸통 중심을 봄
        private bool CheckLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = targetCollider != null ? targetCollider.bounds.center : Target.position;

            return Physics.Linecast(eye, targetPoint, sightBlockMask, QueryTriggerInteraction.Ignore) == false;
        }

        #region SIGHT_VISUALIZATION

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 position = transform.position;
            Vector3 forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;

            // 왼쪽 끝(-halfAngle)에서 시작해 전체 각도만큼 부채꼴을 그림
            Vector3 from = Quaternion.AngleAxis(-sightHalfAngle, Vector3.up) * forward;

            Color originalColor = UnityEditor.Handles.color;

            UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.15f);
            UnityEditor.Handles.DrawSolidArc(position, Vector3.up, from, sightHalfAngle * 2f, sightRange);

            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireArc(position, Vector3.up, from, sightHalfAngle * 2f, sightRange);
            UnityEditor.Handles.Label(position + forward * sightRange, $"Sight (±{sightHalfAngle}°, {sightRange})");

            // 실행 중엔 시야 판정선을 보이면 초록, 가려지면 빨강으로 표시
            if (Application.isPlaying && Target != null)
            {
                Vector3 targetPoint = targetCollider != null ? targetCollider.bounds.center : Target.position;

                UnityEditor.Handles.color = HasLineOfSight ? Color.green : Color.red;
                UnityEditor.Handles.DrawLine(position + Vector3.up * eyeHeight, targetPoint);
            }

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}