using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 땅에서 일어나는 흙먼지(Ground Dust 셰이더), 쓰는 쪽이 위치·속도를 정해 한 알씩 뿜는다.
    // 뭉게 먼지·바닥에 깔려 퍼지는 충격파·튀었다 떨어지는 흙 파편을 각각의 파티클 시스템으로 그리고,
    // 머티리얼 사본의 _Mode로 셰이더 모양을 고른다. Create로 만들어 돌려 쓰고, 시간 정지 중에는 SetPaused로 함께 멈춘다.
    public sealed class GroundDust : MonoBehaviour
    {
        private static readonly int ModeId = Shader.PropertyToID("_Mode");

        private const float PuffMode = 0f;
        private const float RingMode = 1f;
        private const float ChunkMode = 2f;

        private readonly List<Material> materials = new();
        private ParticleSystem puffs;
        private ParticleSystem rings;
        private ParticleSystem chunks;

        public static GroundDust Create(Material material, string name)
        {
            var dustObject = new GameObject(name);
            GroundDust dust = dustObject.AddComponent<GroundDust>();

            dust.puffs = dust.CreateSystem(dustObject, material, PuffMode);
            dust.rings = dust.CreateSystem(dust.CreateChild("Shockwave"), material, RingMode);
            dust.chunks = dust.CreateSystem(dust.CreateChild("Debris"), material, ChunkMode);
            dust.SetupPuffs();
            dust.SetupRings();
            dust.SetupChunks();

            dust.puffs.Play(true);
            return dust;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Destroy(material);
        }

        #region PUBLIC_METHODS

        // opacity는 0~1, 수명에 따른 투명도 변화에 곱해짐
        public void Emit(Vector3 position, Vector3 velocity, float size, float lifetime, float opacity)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = new Color(1f, 1f, 1f, opacity),
                applyShapeToPosition = false
            };
            puffs.Emit(emit, 1);
        }

        // 바닥(normal)을 따라 고리 모양으로 퍼지는 먼지, size·lifetime은 (최소, 최대)
        public void EmitRing(Vector3 center, Vector3 normal, int count, float radius, Vector2 speed, float rise, Vector2 size, Vector2 lifetime, float opacity)
        {
            if (count <= 0) return;

            GetTangents(normal, out Vector3 tangent, out Vector3 bitangent);
            float angleStep = Mathf.PI * 2f / count;
            float angleOffset = Random.Range(0f, angleStep);

            for (int index = 0; index < count; index++)
            {
                float angle = angleOffset + angleStep * (index + Random.Range(-0.25f, 0.25f));
                Vector3 radial = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                Vector3 velocity = radial * Random.Range(speed.x, speed.y) + normal * (rise * Random.Range(0.4f, 1f));
                Emit(center + radial * radius, velocity, Random.Range(size.x, size.y), Random.Range(lifetime.x, lifetime.y), opacity);
            }
        }

        // 바닥에 깔려 가운데에서 바깥으로 밀려 나가는 먼지 고리, diameter는 다 퍼졌을 때의 지름
        public void EmitShockwave(Vector3 groundPoint, float diameter, float lifetime, float opacity)
        {
            var emit = new ParticleSystem.EmitParams
            {
                // 바닥과 겹치면 깊이 페이드에 지워지므로 살짝 띄움
                position = groundPoint + Vector3.up * 0.12f,
                velocity = Vector3.zero,
                startSize = diameter,
                startLifetime = lifetime,
                startColor = new Color(1f, 1f, 1f, opacity),
                applyShapeToPosition = false
            };
            rings.Emit(emit, 1);
        }

        // 굴러가며 튀었다가 땅에 부딪혀 멈추는 흙 파편 하나
        public void EmitChunk(Vector3 position, Vector3 velocity, float size, float lifetime)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-720f, 720f),
                startColor = Color.white,
                applyShapeToPosition = false
            };
            chunks.Emit(emit, 1);
        }

        // 바닥(normal)에서 사방 위쪽으로 튀어 오르는 흙 파편, speed·size·lifetime은 (최소, 최대)
        public void EmitDebris(Vector3 center, Vector3 normal, int count, Vector2 speed, Vector2 size, Vector2 lifetime)
        {
            GetTangents(normal, out Vector3 tangent, out Vector3 bitangent);
            for (int index = 0; index < count; index++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                Vector3 direction = (radial * Random.Range(0.4f, 1f) + normal * Random.Range(0.9f, 1.8f)).normalized;
                EmitChunk(center + radial * Random.Range(0f, 0.4f), direction * Random.Range(speed.x, speed.y),
                    Random.Range(size.x, size.y), Random.Range(lifetime.x, lifetime.y));
            }
        }

        public void SetPaused(bool paused)
        {
            if (paused) puffs.Pause(true);
            else puffs.Play(true);
        }

        #endregion

        private static void GetTangents(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            bitangent = Vector3.Cross(normal, tangent);
        }

        private GameObject CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child;
        }

        private ParticleSystem CreateSystem(GameObject owner, Material source, float mode)
        {
            ParticleSystem system = owner.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var material = new Material(source) { name = $"{source.name} ({owner.name})" };
            material.SetFloat(ModeId, mode);
            materials.Add(material);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            // 셰이더가 입자마다 다른 노이즈와 수명에 따른 변화를 쓰도록 무작위 값과 나이를 넘김 (TEXCOORD0.zw)
            particleRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.StableRandomX,
                ParticleSystemVertexStream.AgePercent
            });
            return system;
        }

        private void SetupPuffs()
        {
            ParticleSystem.MainModule main = puffs.main;
            main.gravityModifier = -0.03f;
            main.maxParticles = 512;

            ParticleSystem.LimitVelocityOverLifetimeModule drag = puffs.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 4.5f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;

            // 멈춘 뒤에도 공기에 떠밀려 천천히 굽이침
            ParticleSystem.NoiseModule turbulence = puffs.noise;
            turbulence.enabled = true;
            turbulence.strength = 0.6f;
            turbulence.frequency = 0.35f;
            turbulence.scrollSpeed = 0.25f;
            turbulence.octaveCount = 1;
            turbulence.damping = true;
            turbulence.quality = ParticleSystemNoiseQuality.Medium;

            // 처음에 빠르게 부풀고 끝으로 갈수록 천천히 퍼짐
            ParticleSystem.SizeOverLifetimeModule grow = puffs.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 4f), new Keyframe(1f, 2.4f, 0.5f, 0f)));

            SetAlphaOverLifetime(puffs, new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f));
        }

        private void SetupRings()
        {
            ParticleSystem.MainModule main = rings.main;
            main.maxParticles = 32;

            ParticleSystemRenderer particleRenderer = rings.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            // 바닥에 깔린 고리가 뭉게 먼지보다 뒤에 그려지도록
            particleRenderer.sortingFudge = 10f;

            SetAlphaOverLifetime(rings, new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f));
        }

        private void SetupChunks()
        {
            ParticleSystem.MainModule main = chunks.main;
            main.gravityModifier = 1.8f;
            main.maxParticles = 128;

            ParticleSystem.CollisionModule collision = chunks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.bounce = 0.3f;
            collision.dampen = 0.45f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.4f;
            collision.enableDynamicColliders = false;

            SetAlphaOverLifetime(chunks, new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f));
        }

        private static void SetAlphaOverLifetime(ParticleSystem system, params GradientAlphaKey[] alphaKeys)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphaKeys);
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }
    }
}
