using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamOdd
{
    // 보스가 물체를 베거나 되돌려 보내는 순간 그 자리에서 바깥으로 터지는 빛 입자, 히트스탑 중에도 퍼지도록 unscaled 시간으로 진행한다.
    // 처음 터뜨릴 때 입자 시스템을 만들고, 주인이 사라질 때 Dispose로 지운다.
    [Serializable]
    public sealed class HumanoidReflectBurst
    {
        private static readonly int StreakId = Shader.PropertyToID("_Streak");

        [Tooltip("TeamOdd/Hit Debris 머티리얼, 비워 두면 터뜨리지 않는다.")]
        [SerializeField] private Material material;
        [Tooltip("한 번에 뿜는 입자 수")]
        [SerializeField, Min(0)] private int count = 40;
        [Tooltip("입자가 튀어 나가는 속도(m/s), x: 최소, y: 최대. 끌림이 있어 빠르게 감속한다.")]
        [SerializeField] private Vector2 speed = new(10f, 20f);
        [Tooltip("입자가 남아 있는 시간(초), x: 최소, y: 최대")]
        [SerializeField] private Vector2 lifetime = new(0.35f, 0.7f);
        [Tooltip("입자 크기(m), 빠르게 날 때는 이보다 길게 늘어난다.")]
        [SerializeField, Min(0.01f)] private float size = 0.12f;
        [Tooltip("입자가 시작하는 구의 반지름(m), 물체 크기 정도")]
        [SerializeField, Min(0.01f)] private float radius = 0.5f;
        [Tooltip("입자 색(정점 색), 머티리얼 색과 곱해진다.")]
        [SerializeField] private Color color = Color.white;
        [Tooltip("입자가 사라질 때 물드는 색")]
        [SerializeField] private Color tipColor = new(0.6f, 0.9f, 1f);

        private ParticleSystem particles;

        public void Play(Vector3 center)
        {
            if (material == null || count <= 0) return;

            particles ??= CreateParticles();
            particles.transform.position = center;
            particles.Play(true);
            particles.Emit(count);
        }

        public void Dispose()
        {
            if (particles != null) Object.Destroy(particles.gameObject);
            particles = null;
        }

        private ParticleSystem CreateParticles()
        {
            ParticleSystem system = new GameObject("Humanoid Reflect Burst").AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = 0.2f;
            main.maxParticles = Mathf.Max(count * 2, 16);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            // 구 표면에서 바깥으로 뿜어 물체 둘레에서 터지는 모양
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0.15f;

            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 5f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tipColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.velocityScale = 0.08f;
            particleRenderer.lengthScale = 2.5f;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            // 피격 입자처럼 빛줄기로
            var properties = new MaterialPropertyBlock();
            particleRenderer.GetPropertyBlock(properties);
            properties.SetFloat(StreakId, 1f);
            particleRenderer.SetPropertyBlock(properties);

            return system;
        }
    }
}
