using System;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 쓰러진 보스 몸에서 염동 빛 입자가 천천히 떠올라 흩어지는 사망 잔재, 보스의 힘이 빠져나가는 느낌을 준다.
    // 메시 읽기 권한이 없어도 되도록 부위 콜라이더(뼈를 따라 움직임) 표면의 무작위 지점에서 직접 뿜고, 뿜는 양은 시간이 지날수록 줄어든다.
    // 게임 시간을 따르므로 슬로모션 중에는 함께 느려진다.
    [DisallowMultipleComponent]
    public sealed class BossDeathResidue : MonoBehaviour
    {
        [Title("Particles")]
        [Tooltip("TeamOdd/Hit Debris 머티리얼, 비워 두면 뿜지 않는다.")]
        [SerializeField] private Material material;
        [Tooltip("입자 색(정점 색), 머티리얼 색과 곱해진다.")]
        [SerializeField] private Color color = new(0.45f, 0.85f, 1f);
        [Tooltip("사라질 때 물드는 색")]
        [SerializeField] private Color tipColor = new(0.55f, 0.4f, 1f);

        [Title("Emission")]
        [Tooltip("뿜는 시간(초), 처음이 가장 많고 끝으로 갈수록 줄어든다.")]
        [SerializeField, Min(0.1f)] private float duration = 6f;
        [Tooltip("처음 1초에 뿜는 입자 수")]
        [SerializeField, Min(0f)] private float startRate = 45f;
        [Tooltip("떠오르는 속도(m/s), x: 최소, y: 최대")]
        [SerializeField] private Vector2 riseSpeed = new(0.6f, 1.6f);
        [Tooltip("옆으로 흩어지는 속도(m/s)")]
        [SerializeField, Min(0f)] private float spread = 0.35f;
        [SerializeField] private Vector2 size = new(0.05f, 0.12f);
        [SerializeField] private Vector2 lifetime = new(1.4f, 2.4f);

        private Collider[] bodyParts = Array.Empty<Collider>();
        private ParticleSystem particles;
        private float startTime = float.NegativeInfinity;
        private float pending;

        private bool IsPlaying => Time.time - startTime < duration;

        // 방출이 끝나도 이미 나온 입자가 모두 사라질 때까지 연출은 진행 중이다.
        public bool IsEffectPlaying => isActiveAndEnabled && particles != null
            && (IsPlaying || particles.particleCount > 0);

        private void Awake()
        {
            // 루트 캡슐·트리거는 몸 표면이 아니고, 오목한 MeshCollider는 ClosestPoint를 못 써서 뺌
            Collider ownCollider = GetComponent<ICombatable>()?.Collider;
            bodyParts = Array.FindAll(GetComponentsInChildren<Collider>(true),
                part => part.isTrigger == false && part != ownCollider && part is not MeshCollider { convex: false });

            if (bodyParts.Length == 0)
                Debug.LogWarning($"[{nameof(BossDeathResidue)}] {name}에서 부위 콜라이더를 찾지 못해 사망 잔재를 뿜지 않습니다.", this);
        }

        private void OnDestroy()
        {
            if (particles != null) Destroy(particles.gameObject);
        }

        private void Update()
        {
            if (IsPlaying == false || particles == null) return;

            // 남은 비율의 제곱으로 줄여 끝무렵엔 드문드문 남음
            float remain = 1f - (Time.time - startTime) / duration;
            pending += startRate * remain * remain * Time.deltaTime;

            while (pending >= 1f)
            {
                pending -= 1f;
                EmitOne();
            }
        }

        #region PUBLIC_METHODS

        public void Play()
        {
            if (material == null || bodyParts.Length == 0) return;

            particles ??= CreateParticles();
            startTime = Time.time;
            pending = 0f;
            particles.Play();
        }

        #endregion

        private void EmitOne()
        {
            if (TryGetSurfacePoint(out Vector3 point) == false) return;

            Vector2 side = UnityEngine.Random.insideUnitCircle * spread;
            var emit = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = new Vector3(side.x, UnityEngine.Random.Range(riseSpeed.x, riseSpeed.y), side.y),
                startSize = UnityEngine.Random.Range(size.x, size.y),
                startLifetime = UnityEngine.Random.Range(lifetime.x, lifetime.y),
                startColor = color,
            };
            particles.Emit(emit, 1);
        }

        // 무작위 부위의 바운드 안 한 점에서 가장 가까운 표면, 꺼진 부위는 건너뜀
        private bool TryGetSurfacePoint(out Vector3 point)
        {
            point = default;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Collider part = bodyParts[UnityEngine.Random.Range(0, bodyParts.Length)];
                if (part == null || part.enabled == false || part.gameObject.activeInHierarchy == false) continue;

                Bounds bounds = part.bounds;
                Vector3 inside = bounds.center + Vector3.Scale(bounds.extents, UnityEngine.Random.insideUnitSphere);
                Vector3 outside = inside + UnityEngine.Random.onUnitSphere * bounds.extents.magnitude;
                point = part.ClosestPoint(outside);
                return true;
            }
            return false;
        }

        private ParticleSystem CreateParticles()
        {
            ParticleSystem system = new GameObject($"{name} Death Residue").AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.maxParticles = Mathf.Max(64, Mathf.CeilToInt(startRate * lifetime.y * 1.5f));

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // 떠오르며 살짝 흔들리도록
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tipColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.velocityScale = 0.15f;
            particleRenderer.lengthScale = 1.5f;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            return system;
        }
    }
}
