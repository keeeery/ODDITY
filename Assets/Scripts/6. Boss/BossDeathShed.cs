using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 쓰러진 보스가 같은 모습의 홀로그램으로 바뀌어 제자리에서 흩어지는 사망 연출.
    // 부를 때 SkinnedMeshRenderer들의 지금 자세를 메시로 구워 홀로그램 머티리얼(Knife/Hologram Shader Unlit)로 같은 자리에 그리고,
    // 나타남(Alpha) → 다 나타나면 원본 메시를 끄고 몸 충돌도 없앰 → 노이즈로 녹아 사라짐(Dissolve Hide)과 함께 아래에서 위로 지워짐 순서로 진행한 뒤 구운 메시까지 지운다.
    // 아래에서 위로 지우는 건 셰이더의 상자 마스크(Mask Feature)로, 지울 높이 아래를 모두 덮는 큰 상자를 올리며 그 위 경계만 부드럽게 남긴다.
    // 게임 시간을 따르므로 슬로모션 중에는 함께 느려진다.
    [DisallowMultipleComponent]
    public sealed class BossDeathShed : MonoBehaviour
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int DissolveHideId = Shader.PropertyToID("_DissolveHide");
        private static readonly int MaskCenterId = Shader.PropertyToID("_MaskCenter");
        private static readonly int MaskSizeId = Shader.PropertyToID("_MaskSize");
        private static readonly int MaskFalloffId = Shader.PropertyToID("_MaskFalloff");

        // 지울 높이 아래를 가로·아래로 넉넉히 덮는 상자 크기(m)
        private const float MaskBoxSize = 200f;

        [Title("Hologram")]
        [Tooltip("Knife/Hologram Shader Unlit 머티리얼(Dissolve·Mask Feature 켬), 비워 두면 막이 생기지 않는다.")]
        [SerializeField] private Material material;
        [Tooltip("막을 만들 메시, 비워 두면 자식의 SkinnedMeshRenderer를 모두 쓴다.")]
        [SerializeField] private SkinnedMeshRenderer[] meshes;
        [Tooltip("가장 진할 때의 불투명도(0~1), 머티리얼의 Alpha에 곱하지 않고 대신한다.")]
        [SerializeField, Range(0f, 1f)] private float opacity = 0.45f;

        [Title("Motion")]
        [Tooltip("사라지는 과정(녹음·지워짐)이 진행되는 시간(초), 머무는 시간 뒤부터 잰다.")]
        [SerializeField, Min(0.1f)] private float duration = 1.8f;
        [Tooltip("나타나는 데 걸리는 시간(초), 다 나타나면 원본 메시를 끈다.")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
        [Tooltip("다 나타난 모습 그대로 머무는 시간(초), 쓰러질 때의 흙먼지가 걷힌 뒤에 사라지기 시작하도록")]
        [SerializeField, Min(0f)] private float holdDuration = 0.6f;
        [Tooltip("녹아 사라지기 시작하는 진행도(0~1)")]
        [SerializeField, Range(0f, 1f)] private float dissolveStart = 0.35f;

        [Title("Wipe")]
        [Tooltip("아래에서 위로 지워지기 시작하는 진행도(0~1)")]
        [SerializeField, Range(0f, 1f)] private float wipeStart = 0.25f;
        [Tooltip("지워지는 경계의 부드러운 폭(m), 클수록 흐릿하게 사라진다.")]
        [SerializeField, Min(0.01f)] private float wipeSoftness = 0.6f;

        private readonly List<Mesh> bakedMeshes = new();
        private readonly List<MeshRenderer> shellRenderers = new();
        private MaterialPropertyBlock properties;
        private Transform shell;
        private float halfHeight; // 구운 막의 절반 높이(m)
        private float elapsed;
        private bool hasHiddenSource;

        private void Awake()
        {
            if (meshes == null || meshes.Length == 0)
                meshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);

            properties = new MaterialPropertyBlock();
        }

        private void OnDestroy() => Clear();

        private void Update()
        {
            if (shell == null) return;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01((elapsed - holdDuration) / duration);

            if (progress >= 1f)
            {
                Clear();
                return;
            }

            if (hasHiddenSource == false && elapsed >= fadeInDuration) HideSource();

            float alpha = opacity * (fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f);
            float dissolve = Mathf.InverseLerp(dissolveStart, 1f, progress);
            Apply(alpha, Mathf.Lerp(-1f, 1f, dissolve), GetWipeHeight(progress));
        }

        // 막의 바닥 아래(다 보임)에서 꼭대기(다 지워짐)까지, 끝으로 갈수록 빨라지게 올림
        private float GetWipeHeight(float progress)
        {
            float bottom = shell.position.y - halfHeight - wipeSoftness;
            float top = shell.position.y + halfHeight;

            float wipe = Mathf.InverseLerp(wipeStart, 1f, progress);
            return Mathf.Lerp(bottom, top, wipe * wipe);
        }

        #region PUBLIC_METHODS

        public void Play()
        {
            if (material == null) return;

            Clear();

            if (Bake() == false) return;

            elapsed = 0f;
            hasHiddenSource = false;
            Apply(0f, -1f, GetWipeHeight(0f));
        }

        #endregion

        // 지금 자세를 구워 몸 중심에 둔 부모 아래 같은 자리에 놓음
        private bool Bake()
        {
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (SkinnedMeshRenderer source in meshes)
            {
                if (source == null || source.enabled == false || source.gameObject.activeInHierarchy == false) continue;

                if (hasBounds) bounds.Encapsulate(source.bounds);
                else bounds = source.bounds;

                hasBounds = true;
            }

            if (hasBounds == false) return false;

            shell = new GameObject($"{name} Death Shed").transform;
            halfHeight = bounds.extents.y;
            shell.position = bounds.center;

            foreach (SkinnedMeshRenderer source in meshes)
            {
                if (source == null || source.enabled == false || source.gameObject.activeInHierarchy == false) continue;

                // useScale로 뼈 크기까지 구웠으므로 놓을 때는 크기 1
                Mesh mesh = new Mesh { name = $"{source.name} Shed" };
                source.BakeMesh(mesh, true);
                bakedMeshes.Add(mesh);

                GameObject part = new GameObject(source.name);
                part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                part.transform.SetParent(shell, true);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer meshRenderer = part.AddComponent<MeshRenderer>();
                Material[] materials = new Material[mesh.subMeshCount];

                for (int index = 0; index < materials.Length; index++) materials[index] = material;

                meshRenderer.sharedMaterials = materials;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                shellRenderers.Add(meshRenderer);
            }

            return true;
        }

        // 홀로그램이 다 나타난 뒤 원본을 꺼 보스가 홀로그램으로 바뀐 것처럼 보이게 함, 쓰러진 보스는 다시 보이지 않음
        private void HideSource()
        {
            hasHiddenSource = true;
            foreach (SkinnedMeshRenderer source in meshes)
                if (source != null) source.enabled = false;

            DisableBodyCollision();
        }

        // 보이지 않는 몸에 플레이어가 막히거나 밀려나지 않도록 몸 콜라이더를 트리거로 바꿈
        // 끄지 않고 트리거로 두는 건 사망 잔재(BossDeathResidue)가 부위 표면에서 계속 입자를 뿜어야 해서
        // 바닥에 받쳐 줄 콜라이더가 없어지므로 몸체는 먼저 kinematic으로 고정, 오목한 MeshCollider는 트리거가 될 수 없어 끔
        private void DisableBodyCollision()
        {
            if (TryGetComponent(out Rigidbody body))
            {
                if (body.isKinematic == false)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;

                // 시간이 멈춘 중이면 풀릴 때 원래(kinematic 아님)로 돌아가 바닥을 뚫고 떨어지지 않도록
                if (TryGetComponent(out TimeControl.TimeFreezable clock)) clock.KeepKinematicAfterUnfreeze(body);
            }

            foreach (Collider part in GetComponentsInChildren<Collider>(true))
            {
                if (part.isTrigger) continue;

                if (part is MeshCollider { convex: false }) part.enabled = false;
                else part.isTrigger = true;
            }
        }

        // wipeHeight 아래는 지워지고, 그 위 wipeSoftness 폭에 걸쳐 다시 보임
        private void Apply(float alpha, float dissolveHide, float wipeHeight)
        {
            properties.SetFloat(AlphaId, alpha);
            properties.SetFloat(DissolveHideId, dissolveHide);
            properties.SetVector(MaskCenterId, new Vector3(shell.position.x, wipeHeight - MaskBoxSize * 0.5f, shell.position.z));
            properties.SetVector(MaskSizeId, Vector3.one * MaskBoxSize);
            properties.SetFloat(MaskFalloffId, wipeSoftness);

            foreach (MeshRenderer shellRenderer in shellRenderers) shellRenderer.SetPropertyBlock(properties);
        }

        private void Clear()
        {
            if (shell != null) Destroy(shell.gameObject);

            shell = null;
            shellRenderers.Clear();

            foreach (Mesh mesh in bakedMeshes) Destroy(mesh);

            bakedMeshes.Clear();
        }
    }
}
