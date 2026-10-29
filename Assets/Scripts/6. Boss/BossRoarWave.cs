using System.Collections.Generic;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 포효할 때 보스 주변으로 퍼져 나가는 음파 연출, 쓰는 쪽(포효 패턴)이 타격 순간에 Play로 터뜨린다.
    // - 바닥 음파: 발밑에서 수평으로 퍼지는 공간 일그러짐 고리를 시간차로 여러 겹, 밀어내는 범위를 보여 준다.
    // - 공중 음파: 머리(입)에서 카메라를 향해 퍼지는 고리, 소리가 터져 나오는 느낌을 준다.
    // - 흙먼지: 바닥에 깔려 밀려 나가는 충격파와 바깥으로 쓸려 가는 먼지
    // 고리는 ForceRipple 셰이더(화면 굴절)를 쓰고, 보스 시간(TimeFreezable)을 따라 시간 정지 중에는 멈춘다.
    [DisallowMultipleComponent]
    public sealed class BossRoarWave : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int StartRadiusId = Shader.PropertyToID("_StartRadius");
        private static readonly int EndRadiusId = Shader.PropertyToID("_EndRadius");
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        // 셰이더의 고리 반지름은 사각형 반쪽 대비 비율, 끝 반지름에서 고리가 사각형 밖으로 잘리지 않을 만큼 여유를 둠
        private const float RingStartRadius = 0.05f;
        private const float RingEndRadius = 0.92f;

        [Title("Ripple")]
        [Tooltip("TeamOdd/Force Ripple 머티리얼, 비워 두면 음파 고리를 그리지 않는다.")]
        [SerializeField] private Material rippleMaterial;
        [Tooltip("공중 음파가 퍼지는 중심, 비워 두면 이름이 \" Head\"로 끝나는 뼈를 찾는다.")]
        [SerializeField] private Transform mouth;

        [Title("Ground Wave")]
        [SerializeField, Min(0)] private int groundCount = 3;
        [Tooltip("다 퍼졌을 때의 반지름(m), 밀쳐내기 범위에 맞춘다.")]
        [SerializeField, Min(0.1f)] private float groundRadius = 7f;
        [SerializeField] private float groundHeight = 0.3f;
        [SerializeField, Min(0.01f)] private float groundDuration = 0.7f;

        [Title("Air Wave")]
        [SerializeField, Min(0)] private int airCount = 2;
        [SerializeField, Min(0.1f)] private float airRadius = 5f;
        [SerializeField, Min(0.01f)] private float airDuration = 0.5f;

        [Title("Timing")]
        [Tooltip("겹 사이 시간차(초)")]
        [SerializeField, Min(0f)] private float interval = 0.12f;

        [Title("Dust")]
        [Tooltip("TeamOdd/Ground Dust 머티리얼, 비워 두면 흙먼지를 뿜지 않는다.")]
        [SerializeField] private Material dustMaterial;
        [Tooltip("바닥에 깔려 밀려 나가는 먼지 고리가 다 퍼졌을 때의 지름(m), 0이면 생략")]
        [SerializeField, Min(0f)] private float dustShockwaveDiameter = 14f;
        [SerializeField, Min(0.05f)] private float dustShockwaveLifetime = 0.8f;
        [Tooltip("바깥으로 쓸려 가는 먼지 수")]
        [SerializeField, Min(0)] private int dustCount = 16;
        [SerializeField] private Vector2 dustSpeed = new(7f, 11f);
        [SerializeField] private Vector2 dustSize = new(1.2f, 2f);
        [SerializeField] private Vector2 dustLifetime = new(0.6f, 1f);
        [SerializeField, Range(0f, 1f)] private float dustOpacity = 0.6f;

        private sealed class Ring
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Properties;
            public float StartTime;
            public float Duration;
            public bool IsAir;
        }

        private readonly List<Ring> playing = new();
        private readonly Stack<Ring> pool = new();

        private TimeFreezable clock;
        private GroundDust dust;
        private Mesh quadMesh;
        private Camera viewCamera;

        private void Awake()
        {
            if (mouth == null) mouth = FindMouth();
        }

        private void Start()
        {
            BossMonster boss = GetComponent<BossMonster>();
            clock = boss != null ? boss.Clock : TimeFreezable.GetOrAdd(gameObject);
            clock.FrozenChanged += HandleFrozenChanged;
        }

        private void OnDestroy()
        {
            if (clock != null) clock.FrozenChanged -= HandleFrozenChanged;
            if (dust != null) Destroy(dust.gameObject);

            foreach (Ring ring in playing) Destroy(ring.Transform.gameObject);
            foreach (Ring ring in pool) Destroy(ring.Transform.gameObject);
            if (quadMesh != null) Destroy(quadMesh);
        }

        // 보스가 움직여도 이미 퍼진 바닥 음파는 그 자리에 남고, 공중 음파만 머리를 따라감
        private void LateUpdate()
        {
            if (playing.Count == 0) return;

            float now = clock.LocalTime;
            Quaternion billboard = GetBillboardRotation();

            for (int index = playing.Count - 1; index >= 0; index--)
            {
                Ring ring = playing[index];
                float progress = (now - ring.StartTime) / ring.Duration;

                if (progress >= 1f)
                {
                    Release(ring);
                    playing.RemoveAt(index);
                    continue;
                }

                // 시간차로 늦게 시작하는 고리는 차례가 오기 전까지 숨김
                ring.Renderer.enabled = progress >= 0f;
                if (progress < 0f) continue;

                if (ring.IsAir) ring.Transform.SetPositionAndRotation(GetMouthPosition(), billboard);

                ring.Properties.SetFloat(ProgressId, progress);
                ring.Renderer.SetPropertyBlock(ring.Properties);
            }
        }

        #region PUBLIC_METHODS

        // 발밑 바닥(groundPoint)에서 음파를 터뜨림
        public void Play(Vector3 groundPoint)
        {
            float now = clock != null ? clock.LocalTime : Time.time;

            if (rippleMaterial != null)
            {
                for (int index = 0; index < groundCount; index++)
                {
                    Ring ring = Rent(false, now + index * interval, groundDuration);
                    ring.Transform.SetPositionAndRotation(groundPoint + Vector3.up * groundHeight, Quaternion.Euler(90f, 0f, 0f));
                    ring.Transform.localScale = Vector3.one * (groundRadius * 2f / RingEndRadius);
                }

                for (int index = 0; index < airCount; index++)
                {
                    Ring ring = Rent(true, now + index * interval, airDuration);
                    ring.Transform.SetPositionAndRotation(GetMouthPosition(), GetBillboardRotation());
                    ring.Transform.localScale = Vector3.one * (airRadius * 2f / RingEndRadius);
                }
            }

            EmitDust(groundPoint);
        }

        #endregion

        private void EmitDust(Vector3 groundPoint)
        {
            GroundDust system = GetDust();
            if (system == null) return;

            if (dustShockwaveDiameter > 0f) system.EmitShockwave(groundPoint, dustShockwaveDiameter, dustShockwaveLifetime, dustOpacity);
            system.EmitRing(groundPoint + Vector3.up * 0.2f, Vector3.up, dustCount, 1.5f, dustSpeed, 0.6f, dustSize, dustLifetime, dustOpacity);
        }

        private void HandleFrozenChanged(bool frozen)
        {
            if (dust != null) dust.SetPaused(frozen);
        }

        private Ring Rent(bool isAir, float startTime, float duration)
        {
            Ring ring = pool.Count > 0 ? pool.Pop() : CreateRing();
            ring.IsAir = isAir;
            ring.StartTime = startTime;
            ring.Duration = duration;
            ring.Renderer.enabled = false;

            ring.Properties.SetFloat(ProgressId, 0f);
            ring.Properties.SetFloat(StartRadiusId, RingStartRadius);
            ring.Properties.SetFloat(EndRadiusId, RingEndRadius);
            ring.Properties.SetFloat(FillId, 0f);
            ring.Properties.SetFloat(FadeId, 1f);
            ring.Renderer.SetPropertyBlock(ring.Properties);

            ring.Transform.gameObject.SetActive(true);
            playing.Add(ring);
            return ring;
        }

        private void Release(Ring ring)
        {
            ring.Transform.gameObject.SetActive(false);
            pool.Push(ring);
        }

        // 보스 자식으로 두면 시간 정지 오버레이 등 보스 렌더러를 모으는 기능에 섞이므로 씬 최상위에 둠
        private Ring CreateRing()
        {
            var ringObject = new GameObject($"{name} Roar Wave");
            ringObject.layer = 2; // Ignore Raycast
            ringObject.AddComponent<MeshFilter>().sharedMesh = GetQuadMesh();

            MeshRenderer meshRenderer = ringObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = rippleMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            return new Ring
            {
                Transform = ringObject.transform,
                Renderer = meshRenderer,
                Properties = new MaterialPropertyBlock()
            };
        }

        private GroundDust GetDust()
        {
            if (dust == null && dustMaterial != null)
            {
                dust = GroundDust.Create(dustMaterial, $"{name} Roar Dust");
                if (clock != null && clock.IsFrozen) dust.SetPaused(true);
            }
            return dust;
        }

        private Vector3 GetMouthPosition() => mouth != null ? mouth.position : transform.position + Vector3.up * 4f;

        private Quaternion GetBillboardRotation()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            return viewCamera != null ? Quaternion.LookRotation(viewCamera.transform.forward, viewCamera.transform.up) : Quaternion.identity;
        }

        private Transform FindMouth()
        {
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                if (bone.name.EndsWith(" Head")) return bone;

            Debug.LogWarning($"[{nameof(BossRoarWave)}] {name}에서 머리 뼈(\" Head\")를 찾지 못했습니다. Mouth를 직접 지정하세요.", this);
            return null;
        }

        private Mesh GetQuadMesh()
        {
            if (quadMesh != null) return quadMesh;

            quadMesh = new Mesh { name = "Roar Wave Quad" };
            quadMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            });
            quadMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            quadMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
            return quadMesh;
        }
    }
}
