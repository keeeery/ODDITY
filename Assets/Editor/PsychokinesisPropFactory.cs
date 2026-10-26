using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TeamOdd.EditorTools
{
    // Sci-Fi Base 소품을 염동력으로 잡을 수 있는 프리팹으로 바꿔 만든다.
    // PsychokinesisObject는 한 오브젝트에 MeshFilter·MeshRenderer·BoxCollider·Rigidbody가 함께 있어야 하므로,
    // 소품의 LOD0 렌더러들을 머티리얼별 서브메시 하나로 합치고 벽 파편 템플릿(WallFragment.prefab)의 설정을 이어받는다.
    // 부서질 때 콘크리트 대신 소품 색의 금속 조각이 튀도록 파편 머티리얼도 함께 만든다.
    internal static class PsychokinesisPropFactory
    {
        private const string TemplatePath = "Assets/Prefabs/8. Environmental Mechanics/WallFragment.prefab";
        private const string OutputFolder = "Assets/Prefabs/8. Environmental Mechanics/PsychokinesisProps";
        // 바운드 부피(m³)당 질량, 잡을 수 있는 최대 질량(PsychokinesisController 기본 20)을 넘지 않게 자른다.
        private const float Density = 0.6f;
        private const float MinMass = 2f;
        private const float MaxMass = 18f;

        private static readonly HashSet<string> builtThisSession = new();

        public static void ResetSession() => builtThisSession.Clear();

        // 만든 프리팹의 경로를 돌려줌, 한 번의 배치 중에는 같은 소품을 다시 만들지 않는다.
        public static string GetOrCreate(string sourceName)
        {
            string prefabPath = $"{OutputFolder}/{sourceName}.prefab";
            if (builtThisSession.Contains(sourceName)) return prefabPath;

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArenaKit.SciFiPrefabs}/{sourceName}.prefab");
            if (source == null)
            {
                Debug.LogWarning($"[Arena] 염동력 소품 원본을 찾지 못했습니다: {sourceName}");
                return null;
            }

            System.IO.Directory.CreateDirectory(OutputFolder);
            Mesh mesh = CombineLod0(source, out Material[] materials);
            mesh.name = sourceName;
            string meshPath = $"{OutputFolder}/{sourceName}_Mesh.asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
                mesh = saved;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(TemplatePath);
            try
            {
                root.name = sourceName;
                root.layer = LayerMask.NameToLayer("Psychokinesis");
                root.transform.localScale = Vector3.one;
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                root.GetComponent<MeshRenderer>().sharedMaterials = materials;
                BoxCollider box = root.GetComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
                Vector3 size = mesh.bounds.size;
                root.GetComponent<Rigidbody>().mass = Mathf.Clamp(size.x * size.y * size.z * Density, MinMass, MaxMass);
                SerializedObject psychokinesis = new(root.GetComponent<TeamOdd.Psychokinesis.PsychokinesisObject>());
                psychokinesis.FindProperty("debrisMaterial").objectReferenceValue = GetOrCreateDebrisMaterial(sourceName, mesh, materials);
                psychokinesis.ApplyModifiedPropertiesWithoutUndo();
                // 템플릿은 풀에서 꺼낼 때 켜지도록 꺼진 채 저장되어 있어, 씬에 바로 두는 소품은 켜서 저장한다.
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            builtThisSession.Add(sourceName);
            return prefabPath;
        }

        // Sci-Fi 소품은 여러 소품이 한 텍스처 아틀라스를 나눠 쓰므로, 머티리얼을 복사해 타일링·오프셋을
        // 소품에서 가장 넓은 면이 쓰는 UV 영역에 맞춘다. 금속 조각의 UV(0~1)가 그 영역만 보게 되어 소품의 주 색이 입혀진다.
        private static Material GetOrCreateDebrisMaterial(string sourceName, Mesh mesh, Material[] materials)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            if (uvs.Length == 0) return null;

            float largestArea = 0f;
            int subMesh = 0;
            Vector2 uvMin = Vector2.zero, uvMax = Vector2.one;
            for (int index = 0; index < mesh.subMeshCount; index++)
            {
                int[] triangles = mesh.GetTriangles(index);
                for (int corner = 0; corner < triangles.Length; corner += 3)
                {
                    int a = triangles[corner], b = triangles[corner + 1], c = triangles[corner + 2];
                    float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude;
                    if (area <= largestArea) continue;
                    largestArea = area;
                    subMesh = index;
                    uvMin = Vector2.Min(uvs[a], Vector2.Min(uvs[b], uvs[c]));
                    uvMax = Vector2.Max(uvs[a], Vector2.Max(uvs[b], uvs[c]));
                }
            }

            // 가늘고 긴 삼각형이면 한쪽 폭이 0에 가까워 줄무늬만 보이므로 최소 폭을 둔다.
            const float MinimumWindow = 0.04f;
            Vector2 center = (uvMin + uvMax) * 0.5f;
            Vector2 window = Vector2.Max(uvMax - uvMin, Vector2.one * MinimumWindow);

            string path = $"{OutputFolder}/{sourceName}_Debris.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(materials[subMesh]);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(materials[subMesh]);

            material.name = sourceName + "_Debris";
            material.mainTextureScale = window;
            material.mainTextureOffset = center - window * 0.5f;
            EditorUtility.SetDirty(material);
            return material;
        }

        // 소품 루트 기준 좌표로 LOD0 렌더러를 모두 합치고, 같은 머티리얼끼리는 한 서브메시로 묶는다.
        private static Mesh CombineLod0(GameObject source, out Material[] materials)
        {
            GameObject probe = Object.Instantiate(source);
            try
            {
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                probe.transform.localScale = Vector3.one;
                Matrix4x4 toRoot = probe.transform.worldToLocalMatrix;

                IEnumerable<Renderer> renderers = probe.TryGetComponent(out LODGroup lodGroup) && lodGroup.GetLODs().Length > 0
                    ? lodGroup.GetLODs()[0].renderers
                    : probe.GetComponentsInChildren<Renderer>();

                Dictionary<Material, List<CombineInstance>> byMaterial = new();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer is not MeshRenderer || renderer.TryGetComponent(out MeshFilter filter) == false || filter.sharedMesh == null) continue;
                    Material[] shared = renderer.sharedMaterials;
                    for (int index = 0; index < filter.sharedMesh.subMeshCount; index++)
                    {
                        Material material = shared[Mathf.Min(index, shared.Length - 1)];
                        if (byMaterial.TryGetValue(material, out List<CombineInstance> list) == false)
                            byMaterial[material] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = index, transform = toRoot * renderer.transform.localToWorldMatrix });
                    }
                }

                List<CombineInstance> parts = new();
                List<Material> partMaterials = new();
                foreach (KeyValuePair<Material, List<CombineInstance>> pair in byMaterial)
                {
                    Mesh part = new() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    part.CombineMeshes(pair.Value.ToArray(), true, true);
                    parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                    partMaterials.Add(pair.Key);
                }

                Mesh combined = new() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(parts.ToArray(), false, false);
                combined.RecalculateBounds();
                foreach (CombineInstance part in parts) Object.DestroyImmediate(part.mesh);

                materials = partMaterials.ToArray();
                return combined;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }
    }
}
