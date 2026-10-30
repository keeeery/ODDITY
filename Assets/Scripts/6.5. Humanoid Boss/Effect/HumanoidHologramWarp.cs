using System;
using System.Collections;
using System.Collections.Generic;
using TeamOdd.Boss;
using UnityEngine;

namespace TeamOdd
{
    // 차원 가르기의 순간이동 연출: 사망 연출(BossDeathShed)과 같은 홀로그램 머티리얼로 지금 자세를 구워 덮고
    // - 사라짐: 원본을 홀로그램으로 바꾼 뒤 떠오르며 아래에서 위로 지워짐
    // - 나타남: 위에서 내려오며 위에서 아래로 그려지고, 다 그려지면 원본으로 바꾼 뒤 홀로그램이 옅어짐
    // 머티리얼·메시·불투명도는 같은 오브젝트의 BossDeathShed 설정을 그대로 씀, 원본을 숨기고 보이는 건 부르는 쪽이 콜백으로 처리
    // 보스는 세상 정지의 예외라 게임 시간(Time.deltaTime)으로 진행
    public sealed class HumanoidHologramWarp : MonoBehaviour
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int DissolveHideId = Shader.PropertyToID("_DissolveHide");
        private static readonly int MaskCenterId = Shader.PropertyToID("_MaskCenter");
        private static readonly int MaskSizeId = Shader.PropertyToID("_MaskSize");
        private static readonly int MaskFalloffId = Shader.PropertyToID("_MaskFalloff");

        // 지울 높이 아래를 가로·아래로 넉넉히 덮는 상자 크기(m)
        private const float MaskBoxSize = 200f;

        [Serializable]
        public struct Settings
        {
            [Tooltip("사라질 때 홀로그램이 다 덮인 뒤 떠오르며 지워지는 시간(초)")]
            public float vanishDuration;
            [Tooltip("나타날 때 위에서 내려오며 다 그려지는 시간(초)")]
            public float appearDuration;
            [Tooltip("홀로그램이 덮이거나(사라짐) 원본으로 바뀐 뒤 옅어지는(나타남) 시간(초)")]
            public float fadeDuration;
            [Tooltip("사라질 때 떠오르는·나타날 때 내려오는 높이(m)")]
            public float travelHeight;
            [Tooltip("지워지고 그려지는 경계의 부드러운 폭(m)")]
            public float softness;

            public static Settings Default => new()
            {
                vanishDuration = 0.55f,
                appearDuration = 0.6f,
                fadeDuration = 0.12f,
                travelHeight = 1.2f,
                softness = 0.5f,
            };
        }

        private readonly List<Mesh> bakedMeshes = new();
        private readonly List<MeshRenderer> shellRenderers = new();
        private MaterialPropertyBlock properties;
        private BossDeathShed source;
        private Transform shell;
        private Vector3 basePosition; // 구운 자리의 몸 중심
        private float halfHeight;

        private void Awake() => properties = new MaterialPropertyBlock();

        private void OnDestroy() => Clear();

        #region PUBLIC_METHODS

        // 사망 연출 설정이 없으면(머티리얼 미지정 등) null, 부르는 쪽은 홀로그램 없이 바로 숨기고 보임
        public static HumanoidHologramWarp GetOrAdd(GameObject target)
        {
            if (target.TryGetComponent(out BossDeathShed shed) == false || shed.Material == null) return null;

            HumanoidHologramWarp warp = target.TryGetComponent(out HumanoidHologramWarp existing) ? existing : target.AddComponent<HumanoidHologramWarp>();
            warp.source = shed;
            return warp;
        }

        // 지금 보이는 렌더러의 자세를 구움. 나타날 때는 원본이 숨겨진 채라 잠깐 보이게 한 같은 프레임에 불러야 함
        public bool Bake()
        {
            Clear();

            Bounds bounds = default;
            bool hasBounds = false;

            foreach (Renderer renderer in GetSources())
            {
                if (hasBounds) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;

                hasBounds = true;
            }

            if (hasBounds == false) return false;

            shell = new GameObject($"{name} Hologram Warp").transform;
            basePosition = bounds.center;
            halfHeight = bounds.extents.y;
            shell.position = basePosition;

            Material material = source.Material;
            foreach (Renderer renderer in GetSources())
            {
                GameObject part = new GameObject(renderer.name);
                part.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);

                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh { name = $"{renderer.name} Warp" };
                    skinned.BakeMesh(mesh, true);
                    bakedMeshes.Add(mesh);
                    part.transform.localScale = GetParentScale(renderer.transform);
                }
                else
                {
                    if (renderer.TryGetComponent(out MeshFilter filter) == false || filter.sharedMesh == null)
                    {
                        Destroy(part);
                        continue;
                    }

                    mesh = filter.sharedMesh;
                    part.transform.localScale = renderer.transform.lossyScale;
                }

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

            Apply(0f, -1f, 0f, float.NegativeInfinity);
            return true;
        }

        // Bake 뒤에 부름. 홀로그램이 원본 위에 다 덮이면 onCovered(원본 숨기기), 이후 떠오르며 아래에서 위로 지워짐
        public IEnumerator Vanish(Settings settings, Action onCovered)
        {
            if (shell == null)
            {
                onCovered?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < settings.fadeDuration)
            {
                Apply(Mathf.Clamp01(elapsed / settings.fadeDuration), -1f, 0f, float.NegativeInfinity);
                elapsed += Time.deltaTime;
                yield return null;
            }

            onCovered?.Invoke();

            elapsed = 0f;
            while (elapsed < settings.vanishDuration)
            {
                // 처음엔 천천히, 끝으로 갈수록 빠르게 떠오르며 지워짐
                float progress = elapsed / settings.vanishDuration;
                float eased = progress * progress;
                float rise = settings.travelHeight * eased;

                float bottom = basePosition.y - halfHeight - settings.softness;
                float top = basePosition.y + halfHeight;
                float wipe = Mathf.Lerp(bottom, top, eased) + rise;

                Apply(1f, Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(0.4f, 1f, progress)), rise, wipe, settings.softness);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Clear();
        }

        // Bake 뒤에 부름. 위에서 내려오며 위에서 아래로 그려지고, 다 그려지면 onRevealed(원본 보이기) 뒤 홀로그램이 옅어짐
        public IEnumerator Appear(Settings settings, Action onRevealed)
        {
            if (shell == null)
            {
                onRevealed?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < settings.appearDuration)
            {
                // 빠르게 내려오다 제자리에서 감속(cubic ease-out)
                float progress = elapsed / settings.appearDuration;
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                float drop = settings.travelHeight * (1f - eased);

                // 그려진 경계가 머리 위에서 발밑까지 내려옴, 경계 아래는 아직 안 보임
                float top = basePosition.y + halfHeight + settings.softness;
                float bottom = basePosition.y - halfHeight - settings.softness;
                float reveal = Mathf.Lerp(top, bottom, eased) + drop;

                Apply(Mathf.Clamp01(elapsed / settings.fadeDuration), Mathf.Lerp(1f, -1f, Mathf.Clamp01(progress * 1.6f)), drop, reveal, settings.softness);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Apply(1f, -1f, 0f, float.NegativeInfinity);
            onRevealed?.Invoke();

            elapsed = 0f;
            while (elapsed < settings.fadeDuration)
            {
                Apply(1f - Mathf.Clamp01(elapsed / settings.fadeDuration), -1f, 0f, float.NegativeInfinity);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Clear();
        }

        public void Clear()
        {
            if (shell != null) Destroy(shell.gameObject);

            shell = null;
            shellRenderers.Clear();

            foreach (Mesh mesh in bakedMeshes) Destroy(mesh);

            bakedMeshes.Clear();
        }

        #endregion

        private IEnumerable<Renderer> GetSources()
        {
            foreach (SkinnedMeshRenderer renderer in source.SkinnedMeshes)
                if (IsVisible(renderer)) yield return renderer;

            if (source.RigidMeshes == null) yield break;

            foreach (MeshRenderer renderer in source.RigidMeshes)
                if (IsVisible(renderer)) yield return renderer;
        }

        private static bool IsVisible(Renderer renderer) => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;

        private static Vector3 GetParentScale(Transform target)
        {
            Vector3 lossy = target.lossyScale;
            Vector3 local = target.localScale;
            return new Vector3(lossy.x / local.x, lossy.y / local.y, lossy.z / local.z);
        }

        // fade: 0~1, 사망 연출의 불투명도를 곱함 / offsetY: 구운 자리에서 위로 옮긴 높이(m)
        // wipeHeight 아래는 안 보이고 그 위 softness 폭에 걸쳐 보임, 음의 무한대면 마스크 없이 다 보임
        private void Apply(float fade, float dissolveHide, float offsetY, float wipeHeight, float softness = 0.01f)
        {
            shell.position = basePosition + Vector3.up * offsetY;

            float maskTop = float.IsNegativeInfinity(wipeHeight) ? basePosition.y - MaskBoxSize : wipeHeight;
            properties.SetFloat(AlphaId, source.Opacity * fade);
            properties.SetFloat(DissolveHideId, dissolveHide);
            properties.SetVector(MaskCenterId, new Vector3(basePosition.x, maskTop - MaskBoxSize * 0.5f, basePosition.z));
            properties.SetVector(MaskSizeId, Vector3.one * MaskBoxSize);
            properties.SetFloat(MaskFalloffId, softness);

            foreach (MeshRenderer shellRenderer in shellRenderers) shellRenderer.SetPropertyBlock(properties);
        }
    }
}
