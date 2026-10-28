using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스가 헤롱거리거나 지쳐 무너진 모습: 애니메이션 위에 척추·목·머리 뼈를 덧흔든다. 흔드는 방식은 두 겹이다.
    // - Sway: 원을 그리며 기울고 고개를 느리게 좌우로 돌림, 머리를 부딪혀 멍한 느낌(돌진 벽 충돌)
    // - Slump: 고개·등이 아래로 처지고 거친 숨에 맞춰 들썩이며 잘게 떨림, 힘이 빠져 버티는 느낌(무력화)
    // 쓰는 쪽이 겹마다 Begin/End를 짝지어 부르면 그 겹의 가중치가 서서히 오르고 내린다. 둘 다 횟수로 세므로 겹쳐 걸어도 안전
    // 부위마다 정한 각도를 그 부위의 뼈 수로 나눠 쓰므로 목뼈 개수가 다른 보스도 같은 값으로 비슷하게 흔들린다.
    // Slump 동안에는 척추에 매달린 팔(쇄골)을 흔들기 전 월드 회전으로 되돌려, 바닥을 짚은 손이 몸통을 따라 들리지 않게 한다.
    // 보스 시간(TimeFreezable)을 따라 시간 정지 중에는 흔들림도 그 자세로 멈춘다.
    [DisallowMultipleComponent]
    public sealed class BossDizzy : MonoBehaviour
    {
        // Begin/End 횟수와 서서히 오르내리는 가중치
        [Serializable]
        public sealed class Layer
        {
            [SerializeField, Min(0.01f)] private float fadeInDuration = 0.8f;
            [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.5f;

            private int requestCount;

            [ShowInInspector, ReadOnly] public float Weight { get; private set; }
            public bool IsActive => requestCount > 0;

            // 천천히 들어오고 빠지도록 가중치에 이징
            public float Eased => Weight * Weight * (3f - 2f * Weight);

            public void Begin() => requestCount++;

            public void End() => requestCount = Mathf.Max(0, requestCount - 1);

            public void Tick(float deltaTime)
            {
                float target = IsActive ? 1f : 0f;
                Weight = Mathf.MoveTowards(Weight, target, deltaTime / (IsActive ? fadeInDuration : fadeOutDuration));
            }

            public void Clear() => Weight = 0f;
        }

        [Title("Bones")]
        [Tooltip("흔들 뼈, 비워 두면 이름이 \" Spine\"·\" Neck\"(뒤에 숫자 허용)·\" Head\"로 끝나는 뼈를 찾는다.")]
        [SerializeField] private Transform[] bones;
        [Tooltip("뼈 하나 내려갈 때마다 늦어지는 위상(도), 클수록 채찍처럼 끝이 늦게 따라온다.")]
        [SerializeField, Range(0f, 90f)] private float phaseLagPerBone = 25f;

        [Title("Sway")]
        [SerializeField, InlineProperty, HideLabel] private Layer sway = new();
        [Tooltip("척추 전체가 기우는 각도(도), 척추뼈 수로 나눠 쓴다.")]
        [SerializeField, Min(0f)] private float spineAngle = 3f;
        [Tooltip("목 전체가 기우는 각도(도), 목뼈 수로 나눠 쓴다.")]
        [SerializeField, Min(0f)] private float neckAngle = 9f;
        [SerializeField, Min(0f)] private float headAngle = 8f;
        [Tooltip("원을 그리는 기울임 위에 더하는 느린 좌우 고개 돌림(도), 척추·목·머리에 같은 비율로 나눠 쓴다.")]
        [SerializeField, Min(0f)] private float yawSwayAngle = 10f;
        [Tooltip("원을 한 바퀴 그리는 빈도(회/초)")]
        [SerializeField, Min(0f)] private float circleFrequency = 0.55f;
        [Tooltip("좌우 고개 돌림 빈도(회/초)")]
        [SerializeField, Min(0f)] private float yawSwayFrequency = 0.23f;
        [Tooltip("빈도와 각도를 노이즈로 흔드는 정도(0~1), 일정한 원이 기계적으로 보이지 않게 함")]
        [SerializeField, Range(0f, 1f)] private float irregularity = 0.35f;

        [Title("Slump")]
        [SerializeField, InlineProperty, HideLabel] private Layer slump = new();
        [Tooltip("척추·목·머리가 아래로 처지는 각도(도), x: 척추, y: 목, z: 머리. 부위의 뼈 수로 나눠 쓴다.")]
        [SerializeField] private Vector3 droopAngle = new(4f, 10f, 12f);
        [Tooltip("거친 숨에 맞춰 들썩이는 각도(도), x: 척추, y: 목, z: 머리")]
        [SerializeField] private Vector3 heaveAngle = new(2.5f, 2f, 1.5f);
        [Tooltip("숨을 몰아쉬는 빈도(회/초)")]
        [SerializeField, Min(0f)] private float heaveFrequency = 0.75f;
        [Tooltip("Slump 동안 몸통이 들썩여도 애니메이션 방향을 유지할 팔다리 시작 뼈, 비워 두면 이름이 \" Clavicle\"로 끝나는 뼈를 찾는다.")]
        [SerializeField] private Transform[] pinnedLimbs;
        [Tooltip("잘게 떨리는 각도(도), 뼈마다 같은 값을 쓴다.")]
        [SerializeField, Min(0f)] private float trembleAngle = 0.8f;
        [Tooltip("떨리는 빈도(회/초)")]
        [SerializeField, Min(0f)] private float trembleFrequency = 9f;

        private struct Bone
        {
            public Transform Transform;
            public int Part;                // 0: 척추, 1: 목, 2: 머리
            public float PartShare;         // 부위 각도를 이 뼈가 나눠 갖는 비율
            public float YawShare;
            public float PhaseLag;
            public float NoiseSeed;
            public Quaternion BaseLocal;    // 흔들기 전 애니메이션 자세
            public Quaternion AppliedLocal; // 흔든 뒤 자세, 다음 프레임에 그대로면 애니메이터가 덮어쓰지 않은 것
            public Quaternion PinnedWorld;  // 고정할 팔다리의 흔들기 전 월드 회전
        }

        private TimeFreezable clock;
        private Bone[] chain = Array.Empty<Bone>();
        private Bone[] limbs = Array.Empty<Bone>();
        private bool hasApplied;
        private float circlePhase;
        private float yawPhase;
        private float heavePhase;
        private float noiseSeed;

        public Layer Sway => sway;
        public Layer Slump => slump;

        // 두 겹 중 큰 쪽, 어느 쪽이든 흔들리고 있는지 볼 때 씀
        public float Weight => Mathf.Max(sway.Weight, slump.Weight);

        // Slump의 숨 들썩임(-0.6~1, 1이면 가장 깊이 들이쉰 순간), 콧김처럼 숨에 맞출 연출이 읽음
        public float SlumpBreath => GetBreath(heavePhase);

        private void Awake()
        {
            if (bones == null || bones.Length == 0) bones = FindBones();
            chain = BuildChain(bones);

            if (pinnedLimbs == null || pinnedLimbs.Length == 0) pinnedLimbs = FindLimbs();
            limbs = Array.ConvertAll(Array.FindAll(pinnedLimbs, limb => limb != null), limb => new Bone { Transform = limb });
            noiseSeed = UnityEngine.Random.Range(0f, 100f);
        }

        private void Start()
        {
            BossMonster boss = GetComponent<BossMonster>();
            clock = boss != null ? boss.Clock : TimeFreezable.GetOrAdd(gameObject);
        }

        // 애니메이터가 자세를 쓴 뒤에 덧붙임
        private void LateUpdate()
        {
            float deltaTime = clock != null ? clock.LocalDeltaTime : Time.deltaTime;
            sway.Tick(deltaTime);
            slump.Tick(deltaTime);

            if (Weight <= 0f)
            {
                RestoreIfStale();
                hasApplied = false;
                return;
            }

            float time = clock != null ? clock.LocalTime : Time.time;
            float noise = (Mathf.PerlinNoise(noiseSeed, time * 0.4f) - 0.5f) * 2f * irregularity;
            circlePhase += deltaTime * circleFrequency * (1f + noise * 0.5f) * Mathf.PI * 2f;
            yawPhase += deltaTime * yawSwayFrequency * Mathf.PI * 2f;
            heavePhase += deltaTime * heaveFrequency * Mathf.PI * 2f;

            Apply(sway.Eased * (1f + noise * 0.4f), slump.Eased, time);
        }

        private void OnDisable()
        {
            RestoreIfStale();
            hasApplied = false;
            sway.Clear();
            slump.Clear();
        }

        private void Apply(float swayAmount, float slumpAmount, float time)
        {
            Transform root = transform;

            // 흔들기 전 애니메이션 자세를 먼저 모두 되찾은 뒤에야 팔다리의 원래 월드 회전을 읽을 수 있음
            for (int index = 0; index < chain.Length; index++) RestoreBase(ref chain[index]);
            for (int index = 0; index < limbs.Length; index++)
            {
                RestoreBase(ref limbs[index]);
                if (limbs[index].Transform != null) limbs[index].PinnedWorld = limbs[index].Transform.rotation;
            }

            for (int index = 0; index < chain.Length; index++)
            {
                ref Bone bone = ref chain[index];
                if (bone.Transform == null) continue;

                // 부모부터 차례로 돌리므로 자식은 부모가 기운 자세 위에 더 기움
                Quaternion offset = Quaternion.identity;

                if (swayAmount > 0f)
                {
                    float phase = circlePhase - bone.PhaseLag;
                    Vector3 axis = root.right * Mathf.Cos(phase) + root.forward * Mathf.Sin(phase);
                    float yaw = Mathf.Sin(yawPhase - bone.PhaseLag * 0.5f) * yawSwayAngle * bone.YawShare * swayAmount;
                    float tilt = GetPartAngle(bone.Part, spineAngle, neckAngle, headAngle) * bone.PartShare * swayAmount;
                    offset = Quaternion.AngleAxis(yaw, root.up) * Quaternion.AngleAxis(tilt, axis);
                }

                if (slumpAmount > 0f)
                {
                    // 보스 오른쪽 축으로 양수만큼 돌면 앞이 아래로 숙여지고, 숨을 들이쉬면 들림. 끝으로 갈수록 조금 늦게 따라옴
                    float heave = GetBreath(heavePhase - bone.PhaseLag * 0.3f);
                    float pitch = (droopAngle[bone.Part] - heave * heaveAngle[bone.Part]) * bone.PartShare;
                    float trembleTime = time * trembleFrequency;
                    float trembleX = (Mathf.PerlinNoise(bone.NoiseSeed, trembleTime) - 0.5f) * 2f * trembleAngle;
                    float trembleZ = (Mathf.PerlinNoise(trembleTime, bone.NoiseSeed) - 0.5f) * 2f * trembleAngle;

                    offset = Quaternion.AngleAxis((pitch + trembleX) * slumpAmount, root.right)
                             * Quaternion.AngleAxis(trembleZ * slumpAmount, root.forward)
                             * offset;
                }

                bone.Transform.rotation = offset * bone.Transform.rotation;
                bone.AppliedLocal = bone.Transform.localRotation;
            }

            // 바닥을 짚은 팔이 몸통을 따라 들리지 않도록 Slump만큼 원래 방향으로 되돌림, 서서 헤롱거릴 때(Sway)는 팔이 몸을 따라감
            for (int index = 0; index < limbs.Length; index++)
            {
                ref Bone limb = ref limbs[index];
                if (limb.Transform == null) continue;

                limb.Transform.rotation = Quaternion.Slerp(limb.Transform.rotation, limb.PinnedWorld, slumpAmount);
                limb.AppliedLocal = limb.Transform.localRotation;
            }

            hasApplied = true;
        }

        // 컬링으로 애니메이터가 자세를 쓰지 않은 프레임이면 지난 자세 위에 또 더해지지 않도록 흔들기 전 자세로 되돌림
        private void RestoreBase(ref Bone bone)
        {
            if (bone.Transform == null) return;

            Quaternion current = bone.Transform.localRotation;
            if (hasApplied && current == bone.AppliedLocal) current = bone.BaseLocal;
            bone.BaseLocal = current;
            bone.Transform.localRotation = current;
        }

        // 숨을 들이쉴 때 크게 들리고 내쉴 때 덜 처지도록 사인을 한쪽으로 치우침
        private static float GetBreath(float phase)
        {
            float breath = Mathf.Sin(phase);
            return breath > 0f ? breath : breath * 0.6f;
        }

        private static float GetPartAngle(int part, float spine, float neck, float head) => part == 0 ? spine : part == 1 ? neck : head;

        // 흔들기를 그만둔 프레임에 애니메이터가 자세를 쓰지 않았다면 흔든 자세가 남지 않도록 되돌림
        private void RestoreIfStale()
        {
            if (hasApplied == false) return;

            foreach (Bone bone in chain)
                if (bone.Transform != null && bone.Transform.localRotation == bone.AppliedLocal) bone.Transform.localRotation = bone.BaseLocal;
            foreach (Bone limb in limbs)
                if (limb.Transform != null && limb.Transform.localRotation == limb.AppliedLocal) limb.Transform.localRotation = limb.BaseLocal;
        }

        private Bone[] BuildChain(Transform[] source)
        {
            // 부모가 먼저 돌아야 하므로 깊이 순으로 정렬
            var sorted = new List<Transform>(Array.FindAll(source, bone => bone != null));
            sorted.Sort((a, b) => GetDepth(a).CompareTo(GetDepth(b)));

            var partCounts = new int[3];
            foreach (Transform bone in sorted) partCounts[GetPart(bone.name)]++;
            int total = Mathf.Max(1, sorted.Count);

            var result = new Bone[sorted.Count];
            for (int index = 0; index < sorted.Count; index++)
            {
                int part = GetPart(sorted[index].name);
                result[index] = new Bone
                {
                    Transform = sorted[index],
                    Part = part,
                    PartShare = 1f / Mathf.Max(1, partCounts[part]),
                    YawShare = 1f / total,
                    PhaseLag = phaseLagPerBone * Mathf.Deg2Rad * index,
                    NoiseSeed = index * 13.7f + 3.1f,
                };
            }

            return result;
        }

        // 이름 규칙 밖의 뼈를 직접 넣은 경우 머리로 봄
        private static int GetPart(string boneName) => IsPart(boneName, "Spine") ? 0 : IsPart(boneName, "Neck") ? 1 : 2;

        private int GetDepth(Transform bone)
        {
            int depth = 0;
            for (Transform current = bone; current != null && current != transform; current = current.parent) depth++;
            return depth;
        }

        private Transform[] FindBones()
        {
            var found = new List<Transform>();
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                if (IsPart(bone.name, "Spine") || IsPart(bone.name, "Neck") || IsPart(bone.name, "Head")) found.Add(bone);

            // 휴머노이드 보스는 뼈 이름 규칙이 달라 아바타의 척추·목·머리 뼈를 씀
            if (found.Count == 0)
                AddHumanBones(found, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head);

            if (found.Count == 0)
                Debug.LogWarning($"[{nameof(BossDizzy)}] {name}에서 척추·목·머리 뼈를 찾지 못했습니다. Bones를 직접 지정하세요.", this);

            return found.ToArray();
        }

        private Transform[] FindLimbs()
        {
            var found = new List<Transform>();
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                if (bone.name.EndsWith(" Clavicle")) found.Add(bone);

            if (found.Count == 0) AddHumanBones(found, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder);
            return found.ToArray();
        }

        // 아바타에 매핑되지 않은 뼈(UpperChest 등)는 건너뜀
        private void AddHumanBones(List<Transform> found, params HumanBodyBones[] humanBones)
        {
            if (TryGetComponent(out Animator animator) == false || animator.isHuman == false) return;

            foreach (HumanBodyBones humanBone in humanBones)
            {
                Transform bone = animator.GetBoneTransform(humanBone);
                if (bone != null) found.Add(bone);
            }
        }

        // " Neck", " Neck1"처럼 공백 + 부위 이름 + 숫자(없어도 됨)로 끝나는지
        private static bool IsPart(string boneName, string part)
        {
            int end = boneName.Length;
            while (end > 0 && char.IsDigit(boneName[end - 1])) end--;

            string key = " " + part;
            return end >= key.Length && string.CompareOrdinal(boneName, end - key.Length, key, 0, key.Length) == 0;
        }
    }
}
