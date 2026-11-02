using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 레이저를 쏘는 동안 플레이어에 붙어 시전 애니메이션을 틀고, 두 손을 모아 레이저 시작점으로 뻗는다 (IK)
    // PsychoBeam이 쏘기 시작할 때 AddComponent로 붙이고, 매 프레임 Aim으로 손을 모을 곳을 넘기고, 끝나면 Release를 부른다
    // 애니메이션: Player Animator의 Base Layer에서 Beam 트리거로 Beam Start → Beam Loop, IsBeam이 꺼지면 Beam End → 대기
    //            몸 자세(다리, 상체)는 이 애니메이션이 맡는다
    // 상체 IK(LookAt): 척추와 머리를 조준 쪽으로 돌려 위아래로 조준할 때 몸통도 같이 기울게 한다
    // 손 IK: 애니메이션은 정면으로만 쏘므로 위아래로 조준해도 손이 레이저 시작점에 오도록 손만 IK로 옮긴다
    //        레이저 시작점은 팔 길이보다 멀어서 손이 닿지 못하고, 그래서 두 팔이 쭉 펴진다
    // Release 뒤에는 팔을 서서히 애니메이션 자세로 돌리고 스스로 사라진다
    // 플레이어 Animator의 Base Layer는 IK Pass가 켜져 있어 여기서 손 IK를 줄 수 있다
    public class PsychoBeamPose : MonoBehaviour
    {
        private static readonly int BeamTrigger = Animator.StringToHash("Beam");
        private static readonly int IsBeam = Animator.StringToHash("IsBeam");

        private const float HandGap = 0.08f;        // 모은 두 손이 가운데에서 떨어진 거리 (m)
        private const float BlendSpeed = 8f;        // 손을 뻗고 거두는 빠르기 (가중치/초), 0.125초 만에 다 뻗는다
        private const float LookDistance = 10f;     // 상체가 바라볼 곳, 레이저 시작점에서 레이저 방향으로 이만큼 앞 (m)
        private const float LookBodyWeight = 0.6f;  // 상체(척추)가 조준 쪽으로 돌아가는 정도 (0~1)
        private const float LookHeadWeight = 0.8f;  // 머리가 조준 쪽으로 돌아가는 정도 (0~1)
        private const float LookClampWeight = 0.5f; // 너무 많이 꺾이지 않게 막는 정도 (0이면 제한 없음, 1이면 안 돌아감)

        // 팔꿈치가 향할 곳 (플레이어 기준 m). x는 오른팔 기준이고 왼팔은 반대로 쓴다. 옆으로 벌리고 살짝 아래로 굽힌다
        private static readonly Vector3 ElbowHint = new Vector3(0.5f, 0.9f, 0f);

        private Animator animator;
        private Vector3 handCenter; // 두 손을 모을 가운데 (레이저 시작점)
        private Vector3 direction;  // 레이저 방향
        private float weight;       // 0이면 애니메이션 자세 그대로, 1이면 손을 다 뻗은 자세
        private bool isReleased;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.SetTrigger(BeamTrigger);
            animator.SetBool(IsBeam, true);
        }

        public void Aim(Vector3 handCenter, Vector3 direction)
        {
            this.handCenter = handCenter;
            this.direction = direction;
        }

        public void Release()
        {
            isReleased = true;
            animator.SetBool(IsBeam, false);
        }

        private void Update()
        {
            float target = isReleased ? 0f : 1f;
            weight = Mathf.MoveTowards(weight, target, BlendSpeed * Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;

            // 상체와 머리를 조준 쪽으로 돌린다. LookAt은 손 IK보다 먼저 풀려서, 상체가 기운 뒤에 손이 레이저 시작점으로 간다
            // PlayerController도 Base Layer에서 LookAt 가중치를 주지만, 이 컴포넌트가 나중에 붙어 뒤에 불리므로 덮어쓴다
            animator.SetLookAtPosition(handCenter + direction * LookDistance);
            animator.SetLookAtWeight(weight, LookBodyWeight, LookHeadWeight, 0f, LookClampWeight);

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            SetHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, handCenter - right * HandGap, -1f);
            SetHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, handCenter + right * HandGap, 1f);

            // IK 가중치는 다시 줄 때까지 남으므로 0으로 돌려놓은 이 자리에서 사라진다
            if (isReleased && weight <= 0f)
                Destroy(this);
        }

        // 손을 target으로 옮긴다. 애니메이션의 팔 방향에서 새 팔 방향으로 돌린 만큼 손도 돌려 손목 모양을 지킨다
        // side는 오른팔 1, 왼팔 -1 (팔꿈치를 바깥쪽으로 벌리는 방향)
        private void SetHand(AvatarIKGoal goal, AvatarIKHint hint, HumanBodyBones upperArm, Vector3 target, float side)
        {
            Vector3 shoulder = animator.GetBoneTransform(upperArm).position;
            Vector3 animatedArm = animator.GetIKPosition(goal) - shoulder;
            Quaternion rotation = Quaternion.FromToRotation(animatedArm, target - shoulder) * animator.GetIKRotation(goal);

            animator.SetIKPosition(goal, target);
            animator.SetIKRotation(goal, rotation);
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight);

            Vector3 elbow = transform.TransformPoint(new Vector3(ElbowHint.x * side, ElbowHint.y, ElbowHint.z));
            animator.SetIKHintPosition(hint, elbow);
            animator.SetIKHintPositionWeight(hint, weight);
        }
    }
}
