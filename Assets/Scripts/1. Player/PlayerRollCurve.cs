using UnityEngine;

namespace TeamOdd.Player
{
    // 구르기 이동 속도를 AnimationCurve로 제어
    // - 커브의 X축은 Roll 상태 진행도(0 ~ moveEndTime)를 0~1로 늘린 값, Y축은 상대 속도(모양만 의미 있음)
    // - 커브 모양과 상관없이 총 이동 거리가 rollDistance가 되도록 속도를 자동으로 맞춤
    // - 진행도를 애니메이터에서 읽으므로 히트스톱 등으로 애니메이션이 느려지면 이동도 같이 느려짐
    public class PlayerRollCurve : MonoBehaviour
    {
        private const int AreaSampleCount = 64;

        [Header("대상")]
        [SerializeField] private PlayerController owner; // 비어 있으면 같은 오브젝트 또는 Player.Instance
        [SerializeField] private string rollStateName = "Roll";
        [SerializeField] private string rollClipName = "Unarmed-DiveRoll-Forward1"; // 첫 구르기 전 길이 계산용

        [Header("이동")]
        [SerializeField, Min(0f)] private float rollDistance = 4f;
        [SerializeField, Range(0.05f, 1f)] private float moveEndTime = 0.7f; // 이 진행도 이후로는 이동하지 않음
        [SerializeField] private AnimationCurve speedCurve = new(
            new Keyframe(0f, 0.3f, 0f, 6f),
            new Keyframe(0.15f, 1f),
            new Keyframe(0.6f, 0.6f),
            new Keyframe(1f, 0f));

        private Animator animator;
        private int rollStateHash;
        private bool hasEnteredRollState; // 이번 구르기에서 Roll 상태를 한 번이라도 거쳤는지

        public float RollDistance => rollDistance;
        public float MoveEndTime => moveEndTime;
        public AnimationCurve SpeedCurve => speedCurve;

        // 마지막으로 확인한 Roll 상태 길이(초, 상태 Speed 반영)
        public float RollStateLength { get; private set; } = 1f;

        // 이번 프레임 계산 결과 (디버거 표시용)
        public float CurrentNormalizedTime { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool IsInRollState { get; private set; }

        private void Start()
        {
            if (owner == null) owner = GetComponent<PlayerController>();
            if (owner == null) owner = Player.Instance.PlayerController;

            animator = owner.GetComponent<Animator>();
            rollStateHash = Animator.StringToHash(rollStateName);

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name != rollClipName) continue;
                RollStateLength = clip.length;
                break;
            }

            owner.RollCurve = this;
        }

        private void OnEnable()
        {
            if (owner != null) owner.RollCurve = this;
        }

        private void OnDisable()
        {
            // 꺼지면 PlayerController의 기존 고정 속도 방식으로 돌아감
            if (owner != null && owner.RollCurve == this) owner.RollCurve = null;
        }

        // PlayerController가 구르기를 시작할 때 호출
        public void BeginRoll()
        {
            hasEnteredRollState = false;
        }

        // PlayerController가 구르는 동안 매 프레임 호출, 수평 속도(m/s)를 돌려줌
        public Vector3 EvaluateVelocity(Vector3 direction)
        {
            CurrentNormalizedTime = ReadRollNormalizedTime(out bool found);
            IsInRollState = found;
            if (found) hasEnteredRollState = true;
            CurrentSpeed = GetSpeedAt(CurrentNormalizedTime);

            return direction * CurrentSpeed;
        }

        // Roll 상태 진행도에서의 속도(m/s)
        public float GetSpeedAt(float normalizedTime)
        {
            if (normalizedTime < 0f || normalizedTime >= moveEndTime) return 0f;

            float moveSeconds = RollStateLength * moveEndTime;
            float area = GetCurveArea();
            if (moveSeconds <= 0f || area <= 0f) return 0f;

            // 거리 = ∫속도 dt = rollDistance * ∫curve dx / area 가 되도록 정규화
            return rollDistance * Mathf.Max(0f, speedCurve.Evaluate(normalizedTime / moveEndTime)) / (area * moveSeconds);
        }

        // 커브 아래 넓이 (0~1), 플레이 중 커브를 고쳐도 바로 반영되게 매번 계산
        public float GetCurveArea()
        {
            float sum = 0f;
            for (int i = 0; i < AreaSampleCount; i++)
            {
                float x = (i + 0.5f) / AreaSampleCount;
                sum += Mathf.Max(0f, speedCurve.Evaluate(x));
            }

            return sum / AreaSampleCount;
        }

        private float ReadRollNormalizedTime(out bool found)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == rollStateHash)
            {
                found = true;
                RollStateLength = current.length;
                return current.normalizedTime;
            }

            // 다른 상태에서 Roll로 블렌딩되는 중이면 넘어가는 대상의 진행도를 사용
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == rollStateHash)
                {
                    found = true;
                    RollStateLength = next.length;
                    return next.normalizedTime;
                }
            }

            found = false;

            // Roll 상태를 이미 빠져나갔으면 이동 끝 (EndRolling보다 전이가 먼저 끝나는 경우)
            if (hasEnteredRollState) return 1f;

            // 트리거는 켰지만 애니메이터가 아직 처리하지 않은 프레임은 시작 속도로 이동
            return 0f;
        }
    }
}
