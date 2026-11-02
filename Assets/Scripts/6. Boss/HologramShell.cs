using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 현재 자세의 홀로그램 메시를 만들고 해제한다. 연출 시간과 원본 표시 전환은 호출자가 맡는다.
    public sealed class HologramShell
    {
        private readonly List<Mesh> bakedMeshes = new();
        private readonly List<MeshRenderer> renderers = new();

        public Transform Root { get; private set; }
        public float HalfHeight { get; private set; }

        public bool Bake(string name, Material material, IReadOnlyList<SkinnedMeshRenderer> meshes, IReadOnlyList<MeshRenderer> rigidMeshes)
        {
            Clear();

            Bounds bounds = default;
            bool hasBounds = false;

            foreach (Renderer source in GetSources(meshes, rigidMeshes))
            {
                if (hasBounds) bounds.Encapsulate(source.bounds);
                else bounds = source.bounds;

                hasBounds = true;
            }

            if (hasBounds == false) return false;

            Root = new GameObject(name).transform;
            Root.position = bounds.center;
            HalfHeight = bounds.extents.y;

            foreach (Renderer source in GetSources(meshes, rigidMeshes))
            {
                GameObject part = new GameObject(source.name);
                part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

                Mesh mesh;
                if (source is SkinnedMeshRenderer skinned)
                {
                    // useScale은 렌더러 자신의 스케일만 구워 넣으므로 부모 스케일은 별도로 적용한다.
                    mesh = new Mesh { name = $"{source.name} Hologram" };
                    skinned.BakeMesh(mesh, true);
                    bakedMeshes.Add(mesh);
                    part.transform.localScale = GetParentScale(source.transform);
                }
                else
                {
                    if (source.TryGetComponent(out MeshFilter filter) == false || filter.sharedMesh == null)
                    {
                        Object.Destroy(part);
                        continue;
                    }

                    mesh = filter.sharedMesh;
                    part.transform.localScale = source.transform.lossyScale;
                }

                part.transform.SetParent(Root, true);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                Material[] materials = new Material[mesh.subMeshCount];
                for (int index = 0; index < materials.Length; index++) materials[index] = material;

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderers.Add(renderer);
            }

            return true;
        }

        public void SetPropertyBlock(MaterialPropertyBlock properties)
        {
            foreach (MeshRenderer renderer in renderers) renderer.SetPropertyBlock(properties);
        }

        public void Clear()
        {
            if (Root != null) Object.Destroy(Root.gameObject);

            Root = null;
            renderers.Clear();

            // 무기의 sharedMesh는 원본 에셋이므로 직접 구운 메시만 해제한다.
            foreach (Mesh mesh in bakedMeshes) Object.Destroy(mesh);

            bakedMeshes.Clear();
        }

        private static IEnumerable<Renderer> GetSources(IReadOnlyList<SkinnedMeshRenderer> meshes, IReadOnlyList<MeshRenderer> rigidMeshes)
        {
            foreach (SkinnedMeshRenderer source in meshes)
                if (IsVisible(source)) yield return source;

            if (rigidMeshes == null) yield break;

            foreach (MeshRenderer source in rigidMeshes)
                if (IsVisible(source)) yield return source;
        }

        private static bool IsVisible(Renderer source) => source != null && source.enabled && source.gameObject.activeInHierarchy;

        private static Vector3 GetParentScale(Transform source)
        {
            Vector3 lossy = source.lossyScale;
            Vector3 local = source.localScale;
            return new Vector3(lossy.x / local.x, lossy.y / local.y, lossy.z / local.z);
        }
    }
}
