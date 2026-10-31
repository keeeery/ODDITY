using System;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    // 휴머노이드 보스의 부위 피격 콜라이더를 아바타 뼈에 만들어 붙임
    // 모델 FBX 안의 뼈는 임포트할 때 정해지는 ID라 씬·프리팹에서 미리 붙여 두기 어려워 실행할 때 만든다.
    // BossHitboxRegistrar가 OnEnable에서 자식 콜라이더를 등록하므로 그보다 먼저 실행되도록 순서를 앞당김
    // 다리·발은 BossShatterCollider를 붙여 염동력 물체를 밟아 부수게 함 (Perderos와 같은 규칙)
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidBossHitboxes : MonoBehaviour
    {
        [Serializable]
        private class BonePart
        {
            public HumanBodyBones bone;
            [Tooltip("이 뼈에서 끝 뼈까지를 캡슐로 덮음, LastBone이면(머리 등) 반지름만큼의 구")]
            public HumanBodyBones endBone = HumanBodyBones.LastBone;
            [Tooltip("모델 원래 크기 기준 반지름(m), 보스 스케일이 곱해짐")]
            [Min(0.01f)] public float radius = 0.06f;
            public bool isShatter = false;
        }

        [Title("Settings")]
        [SerializeField] private string hitboxLayer = "Monster";
        [SerializeField] private BonePart[] parts =
        {
            new() { bone = HumanBodyBones.Hips, endBone = HumanBodyBones.Neck, radius = 0.14f },
            new() { bone = HumanBodyBones.Head, radius = 0.1f },
            new() { bone = HumanBodyBones.LeftUpperArm, endBone = HumanBodyBones.LeftLowerArm, radius = 0.05f },
            new() { bone = HumanBodyBones.LeftLowerArm, endBone = HumanBodyBones.LeftHand, radius = 0.045f },
            new() { bone = HumanBodyBones.RightUpperArm, endBone = HumanBodyBones.RightLowerArm, radius = 0.05f },
            new() { bone = HumanBodyBones.RightLowerArm, endBone = HumanBodyBones.RightHand, radius = 0.045f },
            new() { bone = HumanBodyBones.LeftUpperLeg, endBone = HumanBodyBones.LeftLowerLeg, radius = 0.07f, isShatter = true },
            new() { bone = HumanBodyBones.LeftLowerLeg, endBone = HumanBodyBones.LeftFoot, radius = 0.06f, isShatter = true },
            new() { bone = HumanBodyBones.LeftFoot, endBone = HumanBodyBones.LeftToes, radius = 0.05f, isShatter = true },
            new() { bone = HumanBodyBones.RightUpperLeg, endBone = HumanBodyBones.RightLowerLeg, radius = 0.07f, isShatter = true },
            new() { bone = HumanBodyBones.RightLowerLeg, endBone = HumanBodyBones.RightFoot, radius = 0.06f, isShatter = true },
            new() { bone = HumanBodyBones.RightFoot, endBone = HumanBodyBones.RightToes, radius = 0.05f, isShatter = true },
        };

        private void Awake()
        {
            Animator animator = GetComponent<Animator>();

            if (animator.isHuman == false)
            {
                Debug.LogError($"[{nameof(HumanoidBossHitboxes)}] {name}의 Animator가 휴머노이드가 아니라 부위 콜라이더를 만들지 않습니다.", this);
                return;
            }

            int layer = LayerMask.NameToLayer(hitboxLayer);
            float scale = transform.lossyScale.x;

            for (int i = 0; i < parts.Length; i++)
                CreateHitbox(animator, parts[i], layer, scale);
        }

        // 콜라이더 크기는 월드 기준으로 정한 뒤 뼈의 스케일로 나눠 넣음 (임포트 단위·보스 스케일과 무관하게 같은 크기)
        private void CreateHitbox(Animator animator, BonePart part, int layer, float scale)
        {
            Transform bone = animator.GetBoneTransform(part.bone);
            if (bone == null) return;

            GameObject hitbox = new($"Hitbox {part.bone}") { layer = layer };
            Transform hitboxTrans = hitbox.transform;
            hitboxTrans.SetParent(bone, false);

            float worldRadius = part.radius * scale;
            Transform end = part.endBone == HumanBodyBones.LastBone ? null : animator.GetBoneTransform(part.endBone);

            if (end == null)
            {
                SphereCollider sphere = hitbox.AddComponent<SphereCollider>();
                sphere.radius = worldRadius / hitboxTrans.lossyScale.x;
            }
            else
            {
                Vector3 offset = end.position - bone.position;
                float length = offset.magnitude;

                // 캡슐의 길이 방향(Z)을 끝 뼈 쪽으로 맞춤
                hitboxTrans.SetPositionAndRotation(bone.position + offset * 0.5f, Quaternion.LookRotation(offset));

                CapsuleCollider capsule = hitbox.AddComponent<CapsuleCollider>();
                float localScale = hitboxTrans.lossyScale.x;

                capsule.direction = 2;
                capsule.radius = worldRadius / localScale;
                capsule.height = (length + worldRadius * 2f) / localScale;
            }

            if (part.isShatter) hitbox.AddComponent<BossShatterCollider>();
        }
    }
}
