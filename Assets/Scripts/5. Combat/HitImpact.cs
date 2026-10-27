using UnityEngine;

namespace TeamOdd.Combat
{
    // 강한 피격 지점의 흰색 충격 빌보드(Hit Impact 셰이더), 선택적으로 점광원과 흩날리는 입자를 함께 낸다.
    // Create로 만들어 돌려 쓰고, 히트스톱 중에도 보이도록 unscaled 시간으로 진행한다.
    public sealed class HitImpact : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private float duration;
        private float elapsed;
        private float size;
        private float roll;
        private Light flashLight;
        private float lightIntensity;
        private ParticleSystem debris;
        private int debrisCount;

        public static HitImpact Create(Material material, string name)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());

            HitImpact impact = quad.AddComponent<HitImpact>();
            impact.meshRenderer = quad.GetComponent<MeshRenderer>();
            impact.meshRenderer.sharedMaterial = material;
            impact.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            impact.meshRenderer.receiveShadows = false;
            impact.properties = new MaterialPropertyBlock();
            quad.SetActive(false);
            return impact;
        }

        private void OnDestroy()
        {
            if (debris != null) Destroy(debris.gameObject);
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            if (progress >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }

            FaceCamera();
            UpdateLight(progress);
            properties.SetFloat(ProgressId, progress);
            meshRenderer.SetPropertyBlock(properties);
        }

        #region PUBLIC_METHODS

        // size: 한 변 길이(m), towardCamera: 카메라 쪽으로 당기는 거리(m), rollRange: 화면 가로 기준 ±기울기(도)
        public void Play(Vector3 position, float size, float seconds, float towardCamera, float rollRange)
        {
            Camera camera = Camera.main;
            if (camera != null) position += (camera.transform.position - position).normalized * towardCamera;

            if (debris != null && debrisCount > 0)
            {
                debris.transform.position = position;
                debris.Play(true);
                debris.Emit(debrisCount);
            }

            transform.position = position;
            this.size = size;
            duration = Mathf.Max(seconds, 0.01f);
            elapsed = 0f;
            roll = Random.Range(-rollRange, rollRange);

            properties.SetFloat(SeedId, Random.Range(0f, 100f));
            properties.SetFloat(ProgressId, 0f);
            meshRenderer.SetPropertyBlock(properties);

            gameObject.SetActive(true);
            FaceCamera();
            UpdateLight(0f);
        }

        // intensity가 0이면 점광원을 쓰지 않음
        public void SetLight(Color color, float intensity, float range)
        {
            lightIntensity = intensity;
            if (intensity <= 0f)
            {
                if (flashLight != null) flashLight.enabled = false;
                return;
            }

            if (flashLight == null)
            {
                var lightObject = new GameObject("Light");
                lightObject.transform.SetParent(transform, false);
                flashLight = lightObject.AddComponent<Light>();
                flashLight.type = LightType.Point;
                flashLight.shadows = LightShadows.None;
            }

            flashLight.enabled = true;
            flashLight.color = color;
            flashLight.range = range;
        }

        // count가 0이거나 material이 없으면 입자를 쓰지 않음, speed·lifetime은 (최소, 최대)
        // 입자는 충격보다 오래 남으므로 사각형과 따로 둔다.
        public void SetDebris(Material material, int count, Vector2 speed, Vector2 lifetime, float particleSize, float spawnRadius, Color tipColor)
        {
            debrisCount = material != null ? count : 0;
            if (debrisCount <= 0) return;

            if (debris == null)
            {
                debris = new GameObject($"{name} Debris").AddComponent<ParticleSystem>();
                debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            ParticleSystem.MainModule main = debris.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize);
            main.startColor = Color.white;
            main.gravityModifier = 0.4f;
            main.maxParticles = Mathf.Max(count * 4, 16);

            ParticleSystem.EmissionModule emission = debris.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = debris.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(spawnRadius, 0.01f);
            shape.radiusThickness = 0f;

            ParticleSystem.LimitVelocityOverLifetimeModule drag = debris.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 4f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tipColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = debris.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule shrink = debris.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystemRenderer debrisRenderer = debris.GetComponent<ParticleSystemRenderer>();
            debrisRenderer.sharedMaterial = material;
            debrisRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            debrisRenderer.velocityScale = 0.06f;
            debrisRenderer.lengthScale = 1.5f;
            debrisRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            debrisRenderer.receiveShadows = false;
        }

        #endregion

        private void UpdateLight(float progress)
        {
            if (flashLight == null || lightIntensity <= 0f) return;

            float remain = 1f - Mathf.Clamp01(progress * 2f);
            flashLight.intensity = lightIntensity * remain * remain;
        }

        private void FaceCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            transform.rotation = camera.transform.rotation * Quaternion.Euler(0f, 0f, roll);
            transform.localScale = Vector3.one * size;
        }
    }
}
