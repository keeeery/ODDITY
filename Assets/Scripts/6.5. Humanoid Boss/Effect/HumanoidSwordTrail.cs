using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamOdd
{
    // 검 궤적 리본: 칼날 밑동~끝 두 점의 지난 위치를 이어 칼날 폭만큼의 면을 만듦
    // 휘두를 때뿐 아니라 평소 움직임에도 은은하게 남도록, 칼끝 속도에 따라 진하기만 바뀜
    // 일렁임은 머티리얼(HS_Blend_CG 등)의 텍스처 스크롤, 노이즈 왜곡이 담당
    // 빠르게 휘두르면 프레임 사이 간격이 벌어져 각지므로 Catmull-Rom으로 사이를 메움
    public class HumanoidSwordTrail : MonoBehaviour
    {
        private struct TrailSample
        {
            public Vector3 BasePos;
            public Vector3 TipPos;
            public float Time;
            public float Intensity;
        }

        [Title("References")]
        [SerializeField] private Transform baseTrans;
        [SerializeField] private Transform tipTrans;
        [SerializeField] private Material material;

        [Title("Settings")]
        [SerializeField] private float lifetime = 0.25f;
        [SerializeField, Min(1)] private int subdivision = 4;
        [SerializeField] private float minSampleDistance = 0.02f;
        [SerializeField] private Gradient colorOverAge = CreateDefaultGradient();

        // 칼끝 속도(m/s) 기준, idleSpeed 이하는 idleIntensity, swingSpeed 이상은 최대 진하기
        [Space]
        [SerializeField] private float idleSpeed = 1f;
        [SerializeField] private float swingSpeed = 12f;
        [SerializeField, Range(0f, 1f)] private float idleIntensity = 0.25f;
        [SerializeField] private float intensitySharpness = 15f;

        // 순간이동(워프, 분신 배치 등) 때 이전 위치와 이어져 거대한 면이 생기는 것을 막음
        [Space]
        [SerializeField] private float teleportDistance = 3f;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private float tipSpeed = 0f;
        [ShowInInspector, ReadOnly] private float currIntensity = 0f;
        [ShowInInspector, ReadOnly] private int SampleCount => samples.Count;

        private readonly List<TrailSample> samples = new();
        private readonly List<Vector3> vertices = new();
        private readonly List<Color> colors = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> triangles = new();

        private Mesh mesh;
        private GameObject meshObject;
        private Vector3 prevTipPos;

        private void Awake()
        {
            bool hasReferences =
                baseTrans != null &&
                tipTrans != null &&
                material != null;

            if (hasReferences == false)
            {
                Debug.LogError($"[HumanoidSwordTrail] References are missing on {name}");
                enabled = false;
                return;
            }

            CreateMeshObject();
        }

        private void OnEnable()
        {
            if (meshObject == null) return;

            meshObject.SetActive(true);
            ClearTrail();
        }

        private void OnDisable()
        {
            if (meshObject != null) meshObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (meshObject != null) Destroy(meshObject);
            if (mesh != null) Destroy(mesh);
        }

        // 애니메이션이 칼 위치를 갱신한 뒤에 샘플링
        private void LateUpdate()
        {
            float moveDist = Vector3.Distance(tipTrans.position, prevTipPos);
            if (moveDist > teleportDistance) ClearTrail();

            UpdateIntensity();
            AddSample();
            RemoveExpiredSamples();
            BuildMesh();
        }

        [Button]
        public void ClearTrail()
        {
            samples.Clear();
            prevTipPos = tipTrans.position;
            tipSpeed = 0f;
            currIntensity = idleIntensity;

            if (mesh != null) mesh.Clear();
        }

        // 꼬리 정점을 월드 좌표로 쌓으므로 칼을 따라 움직이지 않는 별도 오브젝트에 그림
        private void CreateMeshObject()
        {
            mesh = new Mesh { name = $"{name} Trail" };
            mesh.MarkDynamic();

            meshObject = new GameObject($"{name} Trail");

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void UpdateIntensity()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            Vector3 tipPos = tipTrans.position;

            tipSpeed = Vector3.Distance(tipPos, prevTipPos) / deltaTime;
            prevTipPos = tipPos;

            float speedRatio = Mathf.InverseLerp(idleSpeed, swingSpeed, tipSpeed);
            float targetIntensity = Mathf.Lerp(idleIntensity, 1f, speedRatio);
            float blend = 1f - Mathf.Exp(-intensitySharpness * deltaTime);

            currIntensity = Mathf.Lerp(currIntensity, targetIntensity, blend);
        }

        // 칼이 거의 멈춰 있으면 점을 쌓지 않아 꼬리가 수명대로 줄어들며 사라짐
        private void AddSample()
        {
            Vector3 tipPos = tipTrans.position;

            if (samples.Count > 0)
            {
                float dist = Vector3.Distance(samples[^1].TipPos, tipPos);
                if (dist < minSampleDistance) return;
            }

            samples.Add(new TrailSample
            {
                BasePos = baseTrans.position,
                TipPos = tipPos,
                Time = Time.time,
                Intensity = currIntensity
            });
        }

        private void RemoveExpiredSamples()
        {
            float expireTime = Time.time - lifetime;

            int expiredCount = 0;
            while (expiredCount < samples.Count && samples[expiredCount].Time < expireTime)
                expiredCount++;

            if (expiredCount > 0) samples.RemoveRange(0, expiredCount);
        }

        private void BuildMesh()
        {
            mesh.Clear();

            // 맨 앞은 항상 현재 칼 위치라 쌓인 점이 하나만 있어도 면이 됨
            int pointCount = samples.Count + 1;
            if (pointCount < 2) return;

            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();

            float now = Time.time;
            int columnCount = (pointCount - 1) * subdivision + 1;

            for (int i = 0; i < pointCount - 1; i++)
            {
                TrailSample p0 = GetPoint(Mathf.Max(i - 1, 0));
                TrailSample p1 = GetPoint(i);
                TrailSample p2 = GetPoint(i + 1);
                TrailSample p3 = GetPoint(Mathf.Min(i + 2, pointCount - 1));

                for (int s = 0; s < subdivision; s++)
                {
                    float t = (float)s / subdivision;
                    AddColumn(Interpolate(p0, p1, p2, p3, t), now, columnCount);
                }
            }

            AddColumn(GetPoint(pointCount - 1), now, columnCount);

            for (int c = 0; c < columnCount - 1; c++)
            {
                int baseIdx = c * 2;

                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);

                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // 0이 현재 칼 위치, 클수록 오래된 점
        private TrailSample GetPoint(int idx)
        {
            if (idx == 0)
            {
                return new TrailSample
                {
                    BasePos = baseTrans.position,
                    TipPos = tipTrans.position,
                    Time = Time.time,
                    Intensity = currIntensity
                };
            }

            return samples[samples.Count - idx];
        }

        // UV: U는 칼(0) → 꼬리(1), V는 밑동(0) → 칼끝(1)
        private void AddColumn(TrailSample sample, float now, int columnCount)
        {
            float u = (float)(vertices.Count / 2) / (columnCount - 1);
            float ageRatio = Mathf.Clamp01((now - sample.Time) / lifetime);

            Color color = colorOverAge.Evaluate(ageRatio);
            color.a *= sample.Intensity;

            vertices.Add(sample.BasePos);
            vertices.Add(sample.TipPos);

            colors.Add(color);
            colors.Add(color);

            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));
        }

        private static TrailSample Interpolate(TrailSample p0, TrailSample p1, TrailSample p2, TrailSample p3, float t)
        {
            return new TrailSample
            {
                BasePos = CatmullRom(p0.BasePos, p1.BasePos, p2.BasePos, p3.BasePos, t),
                TipPos = CatmullRom(p0.TipPos, p1.TipPos, p2.TipPos, p3.TipPos, t),
                Time = Mathf.Lerp(p1.Time, p2.Time, t),
                Intensity = Mathf.Lerp(p1.Intensity, p2.Intensity, t)
            };
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (p2 - p0) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (3f * p1 - p0 - 3f * p2 + p3) * t3
            );
        }

        private static Gradient CreateDefaultGradient()
        {
            Gradient gradient = new();

            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );

            return gradient;
        }
    }
}
