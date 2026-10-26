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
        [field: SerializeField] public AnimStateEventListener AnimEventListener { get; private set; }

        [field: Title("References")]
        [field: SerializeField] public Transform Target { get; private set; } // 임시용

        [TitleGroup("Settings")]
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float sightHalfAngle = 60f;
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float sightRange = 10f;
        [SerializeField, TabGroup("Settings/Type", "Movement")] private float moveHalfAngle = 45f;
        [SerializeField, TabGroup("Settings/Type", "Movement")] private float keepMoveHalfAngle = 90f; // 이미 이동 중일 때 계속 이동할 수 있는 각도

        [Title("States")]
        [ShowInInspector, ReadOnly] public float IntentStartTime { get; private set; }
        [ShowInInspector, ReadOnly] public BossMeleePattern Intent { get; private set; }

        [Title("Telegraph")]
        [Tooltip("패턴들이 공유하는 피격 범위 표시(AttackTelegraph) 풀의 키, 풀은 ObjectPoolManager에 등록")]
        [SerializeField] private string telegraphPoolKey = "AttackTelegraph";

        private bool isTelegraphPoolMissing;

        // 시간 정지 스킬이 보스를 멈출 때 쓰는 시계, 프리팹에 없으면 Awake에서 붙임
        public TimeFreezable Clock { get; private set; }

        public float DistanceToTarget => transform.FlatDistanceTo(Target);
        public float SignedAngleToTarget => Mathf.DeltaAngle(Movement.Rotation.eulerAngles.y, transform.FlatRotationTo(Target).eulerAngles.y);
        
        public bool IsTargetOnRight => SignedAngleToTarget >= 0f;
        public bool CanMoveToTarget => Mathf.Abs(SignedAngleToTarget) <= moveHalfAngle;
        public bool CanKeepMovingToTarget => Mathf.Abs(SignedAngleToTarget) <= keepMoveHalfAngle;
        
        protected override void Awake()
        {
            Clock = TimeFreezable.GetOrAdd(gameObject);
            base.Awake();
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
        
        public bool IsTargetInRange(float range) => DistanceToTarget <= range;
        public bool IsTargetInSight() => IsTargetInRange(sightRange) && Mathf.Abs(SignedAngleToTarget) <= sightHalfAngle;
        
        public void SetIntent(BossMeleePattern intent)
        {
            Intent = intent;
            IntentStartTime = Clock.LocalTime;
        }
        
        public void ClearIntent() => Intent = null;

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

            UnityEditor.Handles.color = originalColor;
        }
#endif

        #endregion
    }
}