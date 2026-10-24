using System;
using UnityEngine;
using Sirenix.OdinInspector;
using FantasyGarden.FSM;
using Study.Utilities;
using Study3D.FpsGame;

namespace TeamOdd.Boss
{
    public class BossMonster : MonoBehaviourFSM
    {
        [field: Title("Components")]
        [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public Rigidbody Body { get; private set; }
        [field: SerializeField] public AnimStateEventListener AnimEventListener { get; private set; }

        [field: Title("References")]
        [field: SerializeField] public Transform Target { get; private set; } // 임시용

        [TitleGroup("Settings")]
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float sightHalfAngle = 60f;
        [SerializeField, TabGroup("Settings/Type", "Sight")] private float sightRange = 10f;
        [SerializeField, TabGroup("Settings/Type", "Movement")] private float moveHalfAngle = 45f;

        [Title("States")]
        [ShowInInspector, ReadOnly] public float IntentStartTime { get; private set; }
        [ShowInInspector, ReadOnly] public BossMeleePattern Intent { get; private set; }

        public float DistanceToTarget => transform.FlatDistanceTo(Target);
        public float SignedAngleToTarget => Mathf.DeltaAngle(transform.eulerAngles.y, transform.FlatRotationTo(Target).eulerAngles.y);
        
        public bool IsTargetOnRight => SignedAngleToTarget >= 0f;
        public bool CanMoveToTarget => Mathf.Abs(SignedAngleToTarget) <= moveHalfAngle;
        
        private void Update()
        {
            Animator.SetFloat(BossAnimParams.DISTANCE_TO_TARGET, DistanceToTarget);
        }
        
        #region PUBLIC_METHODS
        
        public bool IsTargetInRange(float range) => DistanceToTarget <= range;
        public bool IsTargetInSight() => IsTargetInRange(sightRange) && Mathf.Abs(SignedAngleToTarget) <= sightHalfAngle;
        
        public void SetIntent(BossMeleePattern intent)
        {
            Intent = intent;
            IntentStartTime = Time.time;
        }
        
        public void ClearIntent() => Intent = null;

        public void RotateYaw(float deltaAngle, float maxDegrees)
        {
            float step = Mathf.Clamp(deltaAngle, -maxDegrees, maxDegrees);
            transform.rotation = Quaternion.Euler(0f, step, 0f) * transform.rotation;
        }
        
        #endregion
        
        #region ANIMATION_EVENT

        private void OnEnable()
        {
            AnimEventListener.OnOccursAnimStateEvent += OnAnimEventEnded;
        }

        private void OnDisable()
        {
            AnimEventListener.OnOccursAnimStateEvent -= OnAnimEventEnded;
        }

        private void OnAnimEventEnded(string eventName, string eventParam)
        {
            if (eventName == "OnAttackEnded")
            {
                ChangeState(typeof(BossIdleState));
                return;
            }
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