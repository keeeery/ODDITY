using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 쓰러진 보스에서 같은 모습의 홀로그램 막이 허물처럼 벗겨져 살짝 떠오르다 흩어지는 사망 연출.
    // 부를 때 SkinnedMeshRenderer들의 지금 자세를 메시로 구워 홀로그램 머티리얼(Knife/Hologram Shader Unlit)로 그리고,
    // 나타남(Alpha) → 떠오르며 아주 살짝 줄어듦 → 노이즈로 녹아 사라짐(Dissolve Hide)과 함께 아래에서 위로 지워짐 순서로 진행한 뒤 구운 메시까지 지운다.
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
        [Tooltip("시작부터 다 사라질 때까지의 시간(초)")]
        [SerializeField, Min(0.1f)] private float duration = 1.8f;
        [Tooltip("나타나는 데 걸리는 시간(초), 몸과 겹친 채 갑자기 튀어나오지 않도록")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
        [Tooltip("떠오르는 높이(m)")]
        [SerializeField, Min(0f)] private float riseHeight = 1.1f;
        [Tooltip("다 떠올랐을 때의 크기 배율, 1보다 작으면 몸 중심 기준으로 줄어든다.")]
        [SerializeField, Range(0.5f, 1.5f)] private float endScale = 0.97f;
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
        private Vector3 startPosition;
        private float halfHeight; // 구운 막의 절반 높이(m), 크기를 바꾸기 전 기준
        private float elapsed;

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
            float progress = Mathf.Clamp01(elapsed / duration);

            if (progress >= 1f)
            {
                Clear();
                return;
            }

            // 처음엔 몸에서 떨어지듯 빨리, 끝으로 갈수록 느리게 떠오름
            float eased = 1f - (1f - progress) * (1f - progress);
            shell.position = startPosition + Vector3.up * (riseHeight * eased);
            shell.localScale = Vector3.one * Mathf.Lerp(1f, endScale, eased);

            float alpha = opacity * (fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f);
            float dissolve = Mathf.InverseLerp(dissolveStart, 1f, progress);
            Apply(alpha, Mathf.Lerp(-1f, 1f, dissolve), GetWipeHeight(progress));
        }

        // 막의 지금 바닥 아래(다 보임)에서 꼭대기(다 지워짐)까지, 끝으로 갈수록 빨라지게 올림
        private float GetWipeHeight(float progress)
        {
            float extent = halfHeight * shell.localScale.y;
            float bottom = shell.position.y - extent - wipeSoftness;
            float top = shell.position.y + extent;

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
            Apply(0f, -1f, GetWipeHeight(0f));
        }

        #endregion

        // 지금 자세를 구워 몸 중심에 둔 부모 아래에 놓음, 부모 크기를 바꾸면 몸 중심 기준으로 커지거나 줄어듦
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
            startPosition = bounds.center;
            halfHeight = bounds.extents.y;
            shell.position = startPosition;

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
