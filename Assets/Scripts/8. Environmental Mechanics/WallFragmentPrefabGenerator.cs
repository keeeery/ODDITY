#if UNITY_EDITOR
using System.IO;
using TeamOdd.Psychokinesis;
using UnityEditor;
using UnityEngine;

namespace TeamOdd.Arena
{
    internal static class WallFragmentPrefabGenerator
    {
        private const int MaxSubdivisions = 5;
        private const int MediumMaxSubdivisions = 3;
        private const float MediumDeformationStrength = 1.25f;
        private const string OutputFolder = "Assets/Prefabs/8. Environmental Mechanics/GeneratedFragments";

        internal static T[] Generate<T>(PsychokinesisObject template, int count, Vector2 diameterRange, float density, string group, bool visualOnly = false) where T : Component
        {
            string folder = OutputFolder + "/" + group;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            T[] prefabs = new T[count];
            float minimumDiameter = Mathf.Max(0.01f, diameterRange.x);
            float maximumDiameter = Mathf.Max(minimumDiameter, diameterRange.y);
            string templatePath = AssetDatabase.GetAssetPath(template);
            int maxSubdivisions = group == "Medium" ? MediumMaxSubdivisions : MaxSubdivisions;
            float deformationStrength = group == "Medium" ? MediumDeformationStrength : 1f;

            for (int index = 0; index < count; index++)
            {
                string name = (group + "Object") + (index + 1).ToString("D2");
                float diameter = Mathf.Lerp(minimumDiameter, maximumDiameter, (index + 0.5f) / count);
                Mesh generated = CreateFragmentMesh(diameter, index + (group == "Large" ? 20 : 0), maxSubdivisions, deformationStrength);
                generated.name = name;
                string meshPath = folder + "/" + name + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

                if (mesh == null)
                {
                    mesh = generated;
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                else
                {
                    UpdateMesh(mesh, generated);
                    Object.DestroyImmediate(generated);
                    EditorUtility.SetDirty(mesh);
                }

                GameObject root = PrefabUtility.LoadPrefabContents(templatePath);

                try
                {
                    root.name = name;
                    root.transform.localScale = Vector3.one;
                    root.GetComponent<MeshFilter>().sharedMesh = mesh;
                    BoxCollider collider = root.GetComponent<BoxCollider>();
                    collider.center = mesh.bounds.center;
                    collider.size = mesh.bounds.size;
                    root.GetComponent<Rigidbody>().mass = Mathf.Max(0.1f, CalculateVolume(mesh) * density);
                    if (visualOnly)
                    {
                        Object.DestroyImmediate(root.GetComponent<PsychokinesisObject>());
                        root.AddComponent<WallFragmentDebris>();
                    }
                    if (root.GetComponent<WallFragment>() == null) root.AddComponent<WallFragment>();
                    root.SetActive(true);
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
                    prefabs[index] = prefab.GetComponent<T>();
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return prefabs;
        }

        private static void UpdateMesh(Mesh mesh, Mesh generated)
        {
            mesh.Clear();
            mesh.indexFormat = generated.indexFormat;
            mesh.vertices = generated.vertices;
            mesh.normals = generated.normals;
            mesh.triangles = generated.triangles;
            mesh.bounds = generated.bounds;
            mesh.UploadMeshData(false);
        }

        private static float CalculateVolume(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float volume = 0f;

            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
            }
            return Mathf.Abs(volume);
        }

        private static int GetSubdivisions(float diameter)
        {
            int subdivisions = 2;
            float threshold = 0.5f;

            while (diameter > threshold && subdivisions < MaxSubdivisions)
            {
                threshold *= 2f;
                subdivisions++;
            }
            return subdivisions;
        }

        private static Mesh CreateFragmentMesh(float diameter, int variation, int maxSubdivisions, float deformationStrength)
        {
            float goldenRatio = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1f, goldenRatio, 0f), new Vector3(1f, goldenRatio, 0f),
                new Vector3(-1f, -goldenRatio, 0f), new Vector3(1f, -goldenRatio, 0f),
                new Vector3(0f, -1f, goldenRatio), new Vector3(0f, 1f, goldenRatio),
                new Vector3(0f, -1f, -goldenRatio), new Vector3(0f, 1f, -goldenRatio),
                new Vector3(goldenRatio, 0f, -1f), new Vector3(goldenRatio, 0f, 1f),
                new Vector3(-goldenRatio, 0f, -1f), new Vector3(-goldenRatio, 0f, 1f)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            Vector3[] sizes =
            {
                new Vector3(0.7f, 0.65f, 0.7f), new Vector3(0.85f, 0.45f, 0.65f),
                new Vector3(0.6f, 0.8f, 0.65f), new Vector3(0.85f, 0.55f, 0.5f),
                new Vector3(0.65f, 0.6f, 0.8f), new Vector3(0.75f, 0.7f, 0.55f)
            };
            int subdivisions = Mathf.Min(GetSubdivisions(diameter), maxSubdivisions);
            Vector3 size = sizes[variation % sizes.Length];
            Vector3[] vertices = new Vector3[faces.Length * (1 << (subdivisions * 2))];
            int vertexIndex = 0;

            for (int face = 0; face < faces.Length; face += 3)
            {
                Vector3 a = corners[faces[face]].normalized;
                Vector3 b = corners[faces[face + 1]].normalized;
                Vector3 c = corners[faces[face + 2]].normalized;
                SubdivideTriangle(vertices, ref vertexIndex, a, b, c, subdivisions);
            }

            int[] triangles = new int[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];

            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                Vector3 direction = vertices[vertex];
                EvaluateSurface(direction, size, variation, deformationStrength, out vertices[vertex], out normals[vertex]);
                triangles[vertex] = vertex;
            }

            NormalizeDiameter(vertices, diameter);

            Mesh mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                normals = normals
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void NormalizeDiameter(Vector3[] vertices, float diameter)
        {
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);

            Vector3 size = bounds.size;
            float multiplier = diameter / Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            // 균일 배율이므로 곡면 Normal의 방향은 그대로 유지된다.
            for (int index = 0; index < vertices.Length; index++) vertices[index] *= multiplier;
        }

        private static void EvaluateSurface(Vector3 direction, Vector3 size, int variation, float deformationStrength, out Vector3 position, out Vector3 normal)
        {
            Vector3 firstFrequency = new Vector3(2.4f, 1.8f, 0f);
            Vector3 secondFrequency = new Vector3(0f, -1.6f, 2.2f);
            Vector3 detailFrequency = new Vector3(4.1f, -2.7f, 3.3f);
            float firstAmplitude = 0.18f * deformationStrength;
            float secondAmplitude = 0.12f * deformationStrength;
            float detailAmplitude = 0.05f * deformationStrength;
            float dentDepth = 0.2f * deformationStrength;
            const float dentSharpness = 5f;
            float firstWave = Vector3.Dot(direction, firstFrequency) + variation * 1.3f;
            float secondWave = Vector3.Dot(direction, secondFrequency) + variation * 2.1f;
            float detailWave = Vector3.Dot(direction, detailFrequency) + variation * 0.7f;
            Vector3 dentDirection = new Vector3(Mathf.Sin(variation * 1.7f + 0.4f),
                Mathf.Cos(variation * 2.3f + 0.8f), Mathf.Sin(variation * 0.9f + 1.2f)).normalized;
            float dent = dentDepth * Mathf.Exp((Vector3.Dot(direction, dentDirection) - 1f) * dentSharpness);
            float radius = 1f + Mathf.Sin(firstWave) * firstAmplitude + Mathf.Sin(secondWave) * secondAmplitude
                + Mathf.Sin(detailWave) * detailAmplitude - dent;
            Vector3 gradient = firstFrequency * (Mathf.Cos(firstWave) * firstAmplitude)
                + secondFrequency * (Mathf.Cos(secondWave) * secondAmplitude)
                + detailFrequency * (Mathf.Cos(detailWave) * detailAmplitude)
                - dentDirection * (dent * dentSharpness);

            position = Vector3.Scale(direction * radius, size);
            // 구면 위의 반지름 변화율에서 법선을 구하고, 축별 크기 차이는 역전치 변환으로 보정
            Vector3 surfaceNormal = direction * (radius + Vector3.Dot(gradient, direction)) - gradient;
            normal = new Vector3(surfaceNormal.x / size.x, surfaceNormal.y / size.y, surfaceNormal.z / size.z).normalized;
        }

        private static void SubdivideTriangle(Vector3[] vertices, ref int index, Vector3 a, Vector3 b, Vector3 c, int subdivisions)
        {
            if (subdivisions == 0)
            {
                vertices[index++] = a;
                vertices[index++] = b;
                vertices[index++] = c;
                return;
            }

            Vector3 ab = (a + b).normalized;
            Vector3 bc = (b + c).normalized;
            Vector3 ca = (c + a).normalized;
            SubdivideTriangle(vertices, ref index, a, ab, ca, subdivisions - 1);
            SubdivideTriangle(vertices, ref index, b, bc, ab, subdivisions - 1);
            SubdivideTriangle(vertices, ref index, c, ca, bc, subdivisions - 1);
            SubdivideTriangle(vertices, ref index, ab, bc, ca, subdivisions - 1);
        }
    }
}
#endif
