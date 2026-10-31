using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Player
{
    /// <summary>
    /// 대기 중 호흡·미세한 흔들림으로 전투 태세를 드러내고 발 위치를 보정한다.
    /// - 흔들림: LateUpdate에서 척추·가슴·머리에 덧입혀 클립과 무관하게 동작
    /// - 발 폭: Base Layer IK Pass에서 양발 좌우 간격만 걷기 폭에 맞춤
    /// - 발 딛기: 대기로 들어갈 때 발을 고정하고, 앞발 기준으로 뒷발만 한 걸음 옮기며 몸도 함께 옮김
    /// - 착지 보정: 구르기에서 일어날 때 발이 떠 보이면 골반을 내려 바닥에 붙임
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerCombatIdle : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Unarmed-Idle");
        private static readonly int WalkState = Animator.StringToHash("Walk");
        private static readonly int RunState = Animator.StringToHash("Run");
        private static readonly int RollState = Animator.StringToHash("Roll");
        private static readonly int RollRecoverState = Animator.StringToHash("RollRecover");
        private static readonly int DeadState = Animator.StringToHash("Dead");

        [Header("흔들림")]
        [SerializeField] private string psychokinesisLayerName = "Psycokinesis"; // 들기·조준 중에는 흔들림을 멈춰 왼팔 IK가 떨리지 않게 함
        [SerializeField, Min(0.01f)] private float fadeSpeed = 4f; // 초당 가중치 변화량
        [SerializeField, Range(0f, 1f)] private float walkSwayWeight = 0.3f; // 걷는 동안 남길 흔들림 비율
        [SerializeField, Min(0f)] private float swaySpeed = 0.35f; // 노이즈가 흐르는 빠르기
        [Tooltip("척추·가슴에 나눠 주는 최대 각도. X: 앞뒤 숙임, Y: 좌우 비틀기, Z: 좌우 기울기")]
        [SerializeField] private Vector3 swayAngle = new(2.5f, 4.5f, 3f);
        [SerializeField] private float idleLeanForward = 4f; // 대기 클립이 뒤로 젖혀 보여 척추·가슴을 앞으로 숙이는 각도
        [SerializeField, Min(0f)] private float breathRate = 0.3f; // 초당 호흡 횟수
        [SerializeField, Min(0f)] private float breathAngle = 1.5f; // 숨을 들이쉴 때 가슴을 젖히는 각도
        [SerializeField, Range(0f, 1f)] private float headStabilize = 0.7f; // 몸이 흔들려도 시선이 따라가지 않게 머리를 되돌리는 비율

        [Header("발 폭")]
        [SerializeField] private bool matchWalkFootWidth = true; // 걷는 동안 잰 평균 발 폭에 대기 발 폭을 맞춤
        [Tooltip("걷기 폭을 아직 재지 못했거나 맞추지 않을 때 쓰는 대기 발 폭(양발 중심 사이 좌우 거리, m)")]
        [SerializeField, Min(0.05f)] private float idleFootWidth = 0.25f;
        [SerializeField, Min(0.01f)] private float walkWidthSmoothing = 2f; // 걷기 발 폭 평균이 따라가는 빠르기

        [Header("발 딛기")]
        [Tooltip("대기로 들어갈 때 발을 바닥에 고정하고, 앞발을 기준으로 뒷발만 한 걸음 옮겨 대기 자세의 두 발 간격을 맞춤 (끄면 발이 미끄러지듯 모임)")]
        [SerializeField] private bool stepFeet = true;
        [Tooltip("뒷발이 앞발 기준 대기 자세 자리에서 이 거리(m)보다 벌어져 있으면 옮김")]
        [SerializeField, Min(0.01f)] private float stepThreshold = 0.07f;
        [Tooltip("몸이 돌아 발끝이 이 각도(도)보다 좌우로 틀어지면 옮김")]
        [SerializeField, Range(1f, 90f)] private float stepTurnThreshold = 18f;
        [SerializeField, Min(0.05f)] private float stepDuration = 0.22f; // 한 발을 옮기는 시간(초)
        [SerializeField, Min(0f)] private float stepHeight = 0.07f; // 옮기는 동안 발을 드는 최고 높이(m)
        [Tooltip("대기 전환이 이만큼 진행된 뒤부터 발을 옮김, 그 전에는 대기 자세 자리가 아직 많이 움직이므로 고정만 함")]
        [SerializeField, Range(0f, 1f)] private float stepStartProgress = 0.4f;
        [Tooltip("대기 중 몸이 돌거나 밀려 앞발이 대기 자세 자리에서 이 거리(m)보다 벗어나면 다시 한 걸음 딛음")]
        [SerializeField, Min(0.05f)] private float restepDistance = 0.3f;
        [Tooltip("이 거리(m)보다 벌어지면 걷지 않고 바로 다시 딛음 (되감기·순간 이동)")]
        [SerializeField, Min(0.1f)] private float maxPlantDistance = 0.8f;
        [Tooltip("뒷발을 옮기는 동안 몸(캡슐)도 앞발 쪽으로 옮겨, 다 디뎠을 때 두 발이 대기 자세처럼 몸 아래에 오게 함")]
        [SerializeField] private bool moveBodyWithStep = true;
        [SerializeField, Min(0f)] private float maxBodyShift = 0.5f; // 한 걸음에 몸을 옮기는 최대 거리(m)
        [Tooltip("구르기·피격으로 넘어갈 때 발 고정을 푸는 시간(초), 길면 몸만 나가고 발이 남아 다리가 늘어나 보임")]
        [SerializeField, Min(0.01f)] private float quickReleaseTime = 0.06f;
        [SerializeField, Min(0.01f)] private float deathReleaseTime = 0.25f; // 사망으로 넘어갈 때 발 고정을 푸는 시간(초)

        [Header("착지 보정")]
        [SerializeField] private bool groundFeet = true;
        // 이 진행도부터 구르기를 일어나는 구간으로 봄, 그 전(뒤집혀 구르는 구간)은 발이 원래 공중에 있으므로 건드리지 않는다
        [SerializeField, Range(0f, 1f)] private float rollGroundFrom = 0.5f;
        [SerializeField, Min(0f)] private float maxGroundDrop = 0.5f; // 한 프레임에 골반을 내리는 최대 거리(m)
        [SerializeField, Min(0.01f)] private float groundDropReturnSpeed = 0.6f; // 보정이 끝날 때 골반을 원래 높이로 되돌리는 빠르기(m/s)
        [SerializeField, Min(0.01f)] private float overshootReturnSpeed = 3f; // 허용치보다 내려가 있는 골반을 되돌리는 빠르기(m/s)

        private Animator animator;
        private Transform spine;
        private Transform chest;
        private Transform head;
        private Transform hips;
        private Transform leftFoot;
        private Transform rightFoot;
        private CharacterController characterController;
        private float standingFootHeight = -1f; // 대기 중 낮은 쪽 발 뼈의 캐릭터 기준 높이, 음수면 아직 못 잼
        private float standingHipsHeight = -1f; // 대기 중 골반 높이, 누운 자세를 바닥 아래로 끌어내리지 않는 하한 계산용
        private float groundDrop; // 지금 골반을 내리고 있는 거리, 보정이 끝나도 한 번에 0으로 튀지 않게 이어서 줄임
        private int psychokinesisLayer;
        private float swayWeight;
        private float leanWeight; // 숙임은 대기 자세 보정이라 걷기에는 남기지 않음
        private float footWidthWeight;
        private float walkFootWidth;
        private bool hasWalkFootWidth;
        private bool wasEnteringIdle;
        private float enterIdleWidth; // 대기로 넘어가기 시작한 프레임에 보이던 발 폭
        private FootPlant leftPlant;
        private FootPlant rightPlant;
        private bool isPlanted; // 발을 바닥에 고정해 둔 상태인지, 대기를 벗어나 보정이 다 풀리면 놓음
        private bool leftIsFront; // 고정해 둔 두 발 중 기준이 되는 앞발이 왼발인지
        private bool stanceStepped; // 이번에 고정한 뒤 뒷발을 한 걸음 옮겼는지(옮길 필요가 없었던 경우 포함)
        private bool skipStanceStep; // 달리기·구르기에서 대기로 들어가는 중, 다음 고정 때 뒷발을 딛지 않음
        private Vector3 pendingBodyShift; // IK에서 정한 몸 이동, 다음 프레임 애니메이션 전에 캡슐에 적용
        private bool isWalking;
        private bool isIdleSettled;
        private float swayTime;
        private float breathTime;
        private Vector3 noiseSeed;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            characterController = GetComponent<CharacterController>();
            psychokinesisLayer = animator.GetLayerIndex(psychokinesisLayerName);
            noiseSeed = new Vector3(Random.value, Random.value, Random.value) * 100f;
        }

        private void Update()
        {
            ApplyBodyShift();

            if (IsAnimatorPaused) return;

            float deltaTime = Time.deltaTime * SlowMotion.PlayerRelativeScale;

            float free = psychokinesisLayer >= 0 ? 1f - animator.GetLayerWeight(psychokinesisLayer) : 1f;
            int state = BaseStateHash();
            float target = (state == IdleState ? 1f : state == WalkState ? walkSwayWeight : 0f) * free;
            swayWeight = Mathf.MoveTowards(swayWeight, target, fadeSpeed * deltaTime);
            leanWeight = Mathf.MoveTowards(leanWeight, state == IdleState ? free : 0f, fadeSpeed * deltaTime);
            // 걷기 폭은 전환이 끝난 뒤에만 잼, 대기 진입 중 발 폭은 OnAnimatorIK에서 맞추고 여기서는 벗어날 때 서서히 푼다
            bool settled = animator.IsInTransition(0) == false;
            footWidthWeight = Mathf.MoveTowards(footWidthWeight, state == IdleState ? 1f : 0f, fadeSpeed * deltaTime);
            isWalking = settled && state == WalkState;
            isIdleSettled = settled && state == IdleState;

            swayTime += deltaTime * swaySpeed;
            breathTime += deltaTime * breathRate;
        }

        private void LateUpdate()
        {
            if (IsAnimatorPaused) return;

            GroundFeet();
            ApplySway();
        }

        // 일어나는 중 두 발 뼈가 대기 때보다 높으면 그만큼 골반을 내림 (IK 목표는 원본 체형 기준이라 뼈 위치로 잼)
        private void GroundFeet()
        {
            if (hips == null || leftFoot == null || rightFoot == null) return;

            float groundY = transform.position.y;
            float lowest = Mathf.Min(leftFoot.position.y, rightFoot.position.y) - groundY;
            float hipsY = hips.position.y - groundY;

            // 골반을 내리기 전에 재야 보정이 섞이지 않음
            if (isIdleSettled)
            {
                standingFootHeight = standingFootHeight < 0f ? lowest : Mathf.Lerp(standingFootHeight, lowest, 0.1f);
                standingHipsHeight = standingHipsHeight < 0f ? hipsY : Mathf.Lerp(standingHipsHeight, hipsY, 0.1f);
            }

            if (groundFeet == false || standingFootHeight < 0f)
            {
                groundDrop = 0f;
                return;
            }

            // 누운 자세처럼 골반이 이미 낮으면 내리지 않음 (서 있을 때 골반 높이의 절반까지만)
            float allowed = Mathf.Max(Mathf.Min(lowest - standingFootHeight, maxGroundDrop, hipsY - standingHipsHeight * 0.5f), 0f);
            bool grounded = characterController == null || characterController.isGrounded;
            float target = grounded && IsGettingUp() ? allowed : 0f;

            // 내릴 때는 바로 따라가고, 보정이 끝나면 천천히 되돌림
            // 발이 먼저 바닥에 닿아 허용치보다 많이 내려가 있으면 한 번에 튀어 오르지 않게 빠르게만 되돌림
            float deltaTime = Time.deltaTime * SlowMotion.PlayerRelativeScale;
            float returnSpeed = groundDrop > allowed ? overshootReturnSpeed : groundDropReturnSpeed;
            groundDrop = target >= groundDrop ? target : Mathf.Max(target, groundDrop - returnSpeed * deltaTime);

            if (groundDrop > 0f) hips.position -= Vector3.up * groundDrop;
        }

        // 구르기 후반이거나, 구르기·피격·기상에서 서 있는 동작으로 넘어가는 중인지 (서 있는 동작끼리의 전환은 제외)
        private bool IsGettingUp()
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == RollState && current.normalizedTime >= rollGroundFrom) return true;
            if (animator.IsInTransition(0) == false || IsStandingState(current.shortNameHash)) return false;

            return IsStandingState(animator.GetNextAnimatorStateInfo(0).shortNameHash);
        }

        // RollRecover는 빼서 거기서 서 있는 동작으로 넘어갈 때도 바닥에 붙임
        private static bool IsStandingState(int hash) =>
            hash == IdleState || hash == WalkState || hash == RunState;

        private void ApplySway()
        {
            float lean = idleLeanForward * leanWeight;
            if ((swayWeight <= 0f && lean == 0f) || spine == null) return;

            Vector3 sway = new(
                Noise(noiseSeed.x) * swayAngle.x,
                Noise(noiseSeed.y) * swayAngle.y,
                Noise(noiseSeed.z) * swayAngle.z);
            // 들이쉴 때만 가슴을 젖히도록 0~1로 오르내림
            float breath = (1f - Mathf.Cos(breathTime * Mathf.PI * 2f)) * 0.5f * breathAngle;
            sway *= swayWeight;
            breath *= swayWeight;

            // 척추와 가슴에 반씩 나눠 휘어지게 하고, 호흡은 가슴에만 준다
            Vector3 bend = sway * 0.5f + new Vector3(lean * 0.5f, 0f, 0f);
            Rotate(spine, bend);
            if (chest != null) Rotate(chest, bend + new Vector3(-breath, 0f, 0f));
            if (head != null) Rotate(head, -(bend * 2f + new Vector3(-breath, 0f, 0f)) * headStabilize);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            // IK 목표는 누적되지 않으므로 멈춘 프레임에도 적용한다 (건너뛰면 풀릴 때 발이 튐)
            if (layerIndex != 0) return;

            // 컴포넌트를 꺼도 OnAnimatorIK는 불리므로 꺼져 있으면 보정을 푼다
            if (enabled == false)
            {
                SetFootIKWeight(0f);
                return;
            }

            Vector3 left = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 right = animator.GetIKPosition(AvatarIKGoal.RightFoot);
            Vector3 side = transform.right;
            float leftX = Vector3.Dot(left - transform.position, side);
            float rightX = Vector3.Dot(right - transform.position, side);

            if (isWalking)
            {
                float t = 1f - Mathf.Exp(-walkWidthSmoothing * Time.deltaTime * SlowMotion.PlayerRelativeScale);
                walkFootWidth = hasWalkFootWidth ? Mathf.Lerp(walkFootWidth, rightX - leftX, t) : rightX - leftX;
                hasWalkFootWidth = true;
            }

            float width = Mathf.Max(matchWalkFootWidth && hasWalkFootWidth ? walkFootWidth : idleFootWidth, 0.05f);

            // 전환이 끝난 뒤 맞추면 발이 오므라졌다 벌어지므로, 전환 첫 프레임의 폭에서 진행도만큼 목표 폭으로 옮긴다
            bool enteringIdle = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == IdleState;
            if (enteringIdle)
            {
                if (wasEnteringIdle == false) enterIdleWidth = Mathf.Lerp(rightX - leftX, width, footWidthWeight);
                width = Mathf.Lerp(enterIdleWidth, width, animator.GetAnimatorTransitionInfo(0).normalizedTime);
                footWidthWeight = 1f;
            }

            wasEnteringIdle = enteringIdle;

            if (footWidthWeight <= 0f)
            {
                isPlanted = false;
                SetFootIKWeight(0f);
                return;
            }

            // 폭을 맞춘 자리가 대기 자세에서 발이 있어야 할 곳
            float center = (leftX + rightX) * 0.5f;
            Vector3 leftTarget = left + side * (center - width * 0.5f - leftX);
            Vector3 rightTarget = right + side * (center + width * 0.5f - rightX);
            Quaternion leftRotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            Quaternion rightRotation = animator.GetIKRotation(AvatarIKGoal.RightFoot);

            Vector3 leftOut = leftTarget;
            Vector3 rightOut = rightTarget;
            Quaternion leftRotationOut = leftRotation;
            Quaternion rightRotationOut = rightRotation;

            if (stepFeet)
            {
                // 대기 중에는 발을 고정하고 앞발 기준으로 뒷발만 옮기며, 벗어날 때는 가중치만 푼다
                // 걷기·달리기·구르기에서 멈출 때는 대기에 다 들어간 뒤부터 고정한다
                // 달리기·구르기에서 멈출 때는 고정한 자리에 그대로 서고 뒷발을 딛지 않는다
                int state = BaseStateHash();
                int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                bool fromWalk = enteringIdle && current == WalkState;
                bool fromRunOrRoll = enteringIdle && (current == RunState || current == RollState || current == RollRecoverState);
                bool settling = fromWalk || fromRunOrRoll;
                if (state != IdleState) skipStanceStep = false; // 대기로 가다 다른 동작으로 끊기면 취소
                if (fromRunOrRoll) skipStanceStep = true;
                bool planting = state == IdleState && settling == false;
                if (settling) isPlanted = false; // 직전 대기에서 고정해 둔 발 자리에 끌리지 않게 놓음
                if (planting && isPlanted == false)
                {
                    leftPlant = new FootPlant(leftTarget, leftRotation);
                    rightPlant = new FootPlant(rightTarget, rightRotation);
                    isPlanted = true;
                    ChooseFrontFoot(leftTarget, rightTarget);

                    if (skipStanceStep)
                    {
                        stanceStepped = true;
                        skipStanceStep = false;
                    }
                    else
                    {
                        // 한 발이 들려 있으면 딛고 있는 발을 기준으로 들린 발을 바로 내려 딛게 함
                        float lift = leftTarget.y - rightTarget.y;
                        if (Mathf.Abs(lift) > stepThreshold * 0.5f)
                        {
                            leftIsFront = lift < 0f;
                            BackPlant.BeginStep();
                            stanceStepped = true;
                        }
                    }
                }

                // 피격·구르기·사망처럼 다리가 크게 움직이는 동작으로 넘어가면 짧게 풀고 다 풀리면 놓음 (한 프레임에 놓으면 발이 튐)
                if (planting == false && state != WalkState && state != RunState && settling == false)
                {
                    float releaseTime = state == DeadState ? deathReleaseTime : quickReleaseTime;
                    footWidthWeight = Mathf.MoveTowards(footWidthWeight, 0f, Time.deltaTime * SlowMotion.PlayerRelativeScale / releaseTime);
                    if (footWidthWeight <= 0f)
                    {
                        isPlanted = false;
                        SetFootIKWeight(0f);
                        return;
                    }
                }

                if (isPlanted)
                {
                    if (planting)
                    {
                        float progress = enteringIdle ? animator.GetAnimatorTransitionInfo(0).normalizedTime : 1f;
                        UpdateFootPlants(leftTarget, leftRotation, rightTarget, rightRotation, progress);
                    }

                    GetPlantGoals(leftTarget, rightTarget, out Vector3 leftGoal, out Vector3 rightGoal);
                    leftOut = leftPlant.Evaluate(leftGoal, leftRotation, stepHeight, out leftRotationOut);
                    rightOut = rightPlant.Evaluate(rightGoal, rightRotation, stepHeight, out rightRotationOut);
                    leftRotationOut = KeepYaw(leftRotationOut, leftRotation);
                    rightRotationOut = KeepYaw(rightRotationOut, rightRotation);
                }
            }

            // 가중치만큼 보정 위치로 옮김, 회전도 정해야 발바닥이 기울지 않음
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, Vector3.Lerp(left, leftOut, footWidthWeight));
            animator.SetIKPosition(AvatarIKGoal.RightFoot, Vector3.Lerp(right, rightOut, footWidthWeight));
            animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.Slerp(leftRotation, leftRotationOut, footWidthWeight));
            animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.Slerp(rightRotation, rightRotationOut, footWidthWeight));
            SetFootIKWeight(1f);
        }

        // 앞발은 두고 뒷발만 대기 자세 간격만큼 떨어진 자리로 옮긴다, 전환 초반은 간격이 흔들려 기다림
        // 대기 중 앞발이 많이 벗어나면 앞발을 다시 골라 한 걸음 딛는다
        private void UpdateFootPlants(Vector3 leftTarget, Quaternion leftRotation, Vector3 rightTarget, Quaternion rightRotation, float progress)
        {
            float deltaTime = Time.deltaTime * SlowMotion.PlayerRelativeScale / stepDuration;
            GetPlantGoals(leftTarget, rightTarget, out Vector3 leftGoal, out Vector3 rightGoal);
            Vector3 frontTarget = leftIsFront ? leftTarget : rightTarget;

            bool backWasStepping = BackPlant.IsStepping;
            float backFrom = BackPlant.StepProgress;
            leftPlant.Advance(leftGoal, leftRotation, deltaTime);
            rightPlant.Advance(rightGoal, rightRotation, deltaTime);
            if (backWasStepping) AccumulateBodyShift(frontTarget, backFrom, BackPlant.IsStepping ? BackPlant.StepProgress : 1f);

            Quaternion frontRotation = leftIsFront ? leftRotation : rightRotation;
            Vector3 backGoal = leftIsFront ? rightGoal : leftGoal;
            Quaternion backRotation = leftIsFront ? rightRotation : leftRotation;

            // 되감기·순간 이동처럼 크게 옮겨졌으면 바로 다시 딛는다
            if (FrontPlant.Error(frontTarget) > maxPlantDistance)
            {
                leftPlant = new FootPlant(leftTarget, leftRotation);
                rightPlant = new FootPlant(rightTarget, rightRotation);
                stanceStepped = true;
                return;
            }

            if (leftPlant.IsStepping || rightPlant.IsStepping || progress < stepStartProgress) return;

            if (stanceStepped == false)
            {
                if (BackPlant.Error(backGoal) > stepThreshold || BackPlant.IsTurned(backRotation, stepTurnThreshold)) BackPlant.BeginStep();
                stanceStepped = true;
                return;
            }

            if (FrontPlant.IsTurned(frontRotation, stepTurnThreshold) || FrontPlant.Error(frontTarget) > restepDistance)
            {
                ChooseFrontFoot(leftPlant.Position, rightPlant.Position);
                stanceStepped = false;
            }
        }

        // 뒷발을 옮기는 동안 앞발의 고정 자리와 대기 자리 차이만큼 몸을 걸음 진행에 맞춰 옮긴다 (남은 차이를 매 프레임 다시 잼)
        private void AccumulateBodyShift(Vector3 frontTarget, float from, float to)
        {
            if (moveBodyWithStep == false) return;

            float done = Mathf.SmoothStep(0f, 1f, from);
            if (done >= 1f) return;

            Vector3 remaining = FrontPlant.Position - frontTarget;
            remaining.y = 0f;
            remaining = Vector3.ClampMagnitude(remaining, maxBodyShift);
            pendingBodyShift += remaining * ((Mathf.SmoothStep(0f, 1f, to) - done) / (1f - done));
        }

        // 애니메이션 전에 옮겨야 이번 프레임 IK에 반영됨, CharacterController가 꺼져 있으면 버림
        private void ApplyBodyShift()
        {
            if (pendingBodyShift == Vector3.zero) return;

            if (characterController != null && characterController.enabled) characterController.Move(pendingBodyShift);
            pendingBodyShift = Vector3.zero;
        }

        // 몸 앞쪽에 더 나가 있는 발을 앞발로 삼음
        private void ChooseFrontFoot(Vector3 leftPosition, Vector3 rightPosition)
        {
            leftIsFront = Vector3.Dot(leftPosition - rightPosition, transform.forward) >= 0f;
        }

        // 앞발은 대기 자세 자리, 뒷발은 고정한 앞발에서 대기 자세의 두 발 간격만큼 떨어진 자리
        private void GetPlantGoals(Vector3 leftTarget, Vector3 rightTarget, out Vector3 leftGoal, out Vector3 rightGoal)
        {
            if (leftIsFront)
            {
                leftGoal = leftTarget;
                rightGoal = leftPlant.Position + (rightTarget - leftTarget);
            }
            else
            {
                rightGoal = rightTarget;
                leftGoal = rightPlant.Position + (leftTarget - rightTarget);
            }
        }

        private ref FootPlant FrontPlant => ref leftIsFront ? ref leftPlant : ref rightPlant;
        private ref FootPlant BackPlant => ref leftIsFront ? ref rightPlant : ref leftPlant;

        // 바닥에 고정한 발 하나, 옮기는 중이면 들어 올렸다 목표 자리에 내림
        private struct FootPlant
        {
            private Vector3 position;
            private Quaternion rotation;
            private Vector3 stepFromPosition;
            private Quaternion stepFromRotation;
            private float stepTime; // 0~1, 옮기는 중일 때만 씀

            public bool IsStepping { get; private set; }
            public Vector3 Position => position;
            public float StepProgress => stepTime;

            public FootPlant(Vector3 position, Quaternion rotation)
            {
                this.position = position;
                this.rotation = rotation;
                stepFromPosition = position;
                stepFromRotation = rotation;
                stepTime = 0f;
                IsStepping = false;
            }

            // 서 있는 발이 목표 자리에서 벌어진 거리(m), 옮기는 중이면 0
            public float Error(Vector3 target) => IsStepping ? 0f : Vector3.Distance(position, target);

            // 몸이 돌아 발끝이 목표 방향에서 기준 각도보다 좌우로 틀어졌는지, 기울기는 애니메이션을 따르므로 빼고 잼
            public bool IsTurned(Quaternion targetRotation, float threshold) =>
                IsStepping == false && Mathf.Abs(YawAngle(rotation, targetRotation)) > threshold;

            public void BeginStep()
            {
                stepFromPosition = position;
                stepFromRotation = rotation;
                stepTime = 0f;
                IsStepping = true;
            }

            public void Advance(Vector3 target, Quaternion targetRotation, float normalizedDelta)
            {
                if (IsStepping == false) return;

                stepTime += normalizedDelta;
                if (stepTime < 1f) return;

                position = target;
                rotation = targetRotation;
                IsStepping = false;
            }

            public Vector3 Evaluate(Vector3 target, Quaternion targetRotation, float height, out Quaternion outRotation)
            {
                if (IsStepping == false)
                {
                    outRotation = rotation;
                    return position;
                }

                float t = Mathf.SmoothStep(0f, 1f, stepTime);
                outRotation = Quaternion.Slerp(stepFromRotation, targetRotation, t);
                return Vector3.Lerp(stepFromPosition, target, t) + Vector3.up * (Mathf.Sin(stepTime * Mathf.PI) * height);
            }
        }

        // 고정한 발은 발끝 방향만 지키고 앞뒤·좌우 기울기는 애니메이션을 따름 (기울기까지 고정하면 골반이 움직일 때 발목이 꺾임)
        private static Quaternion KeepYaw(Quaternion planted, Quaternion animated) =>
            Quaternion.AngleAxis(YawAngle(animated, planted), Vector3.up) * animated;

        // from에서 to까지 발끝이 바닥 위에서 돈 각도(도), 발끝이 수직에 가까우면 0
        private static float YawAngle(Quaternion from, Quaternion to)
        {
            Vector3 fromForward = Vector3.ProjectOnPlane(from * Vector3.forward, Vector3.up);
            Vector3 toForward = Vector3.ProjectOnPlane(to * Vector3.forward, Vector3.up);
            if (fromForward.sqrMagnitude < 0.0001f || toForward.sqrMagnitude < 0.0001f) return 0f;

            return Vector3.SignedAngle(fromForward, toForward, Vector3.up);
        }

        private void SetFootIKWeight(float weight)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, weight);
        }

        // 애니메이터가 포즈를 새로 쓰지 않는 프레임(되감기 등)은 뼈 회전이 누적되므로 건너뜀
        private bool IsAnimatorPaused => animator.enabled == false || animator.speed <= 0f || Time.deltaTime <= 0f;

        // 전환 중이면 넘어가는 상태 기준으로 판단해 미리 가중치를 내리거나 올림
        private int BaseStateHash() =>
            animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).shortNameHash
                : animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        private float Noise(float seed) => Mathf.PerlinNoise(seed, swayTime) * 2f - 1f;

        // 뼈마다 로컬 축이 달라서 캐릭터 기준 축(앞뒤 숙임, 비틀기, 좌우 기울기)으로 돌림
        private void Rotate(Transform bone, Vector3 angle)
        {
            Quaternion offset = Quaternion.AngleAxis(angle.y, transform.up)
                                * Quaternion.AngleAxis(angle.x, transform.right)
                                * Quaternion.AngleAxis(angle.z, transform.forward);
            bone.rotation = offset * bone.rotation;
        }
    }
}
