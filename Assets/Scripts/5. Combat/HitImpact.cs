using DG.Tweening;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 강한 피격 지점의 흰색 충격 빌보드(Hit Impact 셰이더), 선택적으로 점광원과 흩날리는 입자를 함께 낸다.
    // Create로 만들어 돌려 쓰고, 히트스톱 중에도 보이도록 unscaled 시간으로 진행한다. 슬로모션에 맞춰 느려져야 하면 SetUnscaledTime(false)
    public sealed class HitImpact : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int AxisBiasId = Shader.PropertyToID("_AxisBias");
        private static readonly int RayDensityId = Shader.PropertyToID("_RayDensity");
        private static readonly int CoreSizeId = Shader.PropertyToID("_CoreSize");
        private static readonly int StreakId = Shader.PropertyToID("_Streak");

        // 입자를 빛줄기로 보이게 늘이는 정도, 크기에 곱하는 기본 길이와 속도에 곱하는 추가 길이
        private const float DebrisLengthScale = 2.5f;
        private const float DebrisVelocityScale = 0.08f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private float progress;
        private Tween impactTween;
        private float size;
        private float roll;
        private Light flashLight;
        private float lightIntensity;
        private ParticleSystem debris;
        private int debrisCount;
        private bool useUnscaledTime = true;
        private float aspect = 1f; // 가로 길이 / 세로 길이
        private float debrisUpSpread = 1f; // 입자가 화면 위로 퍼지는 비율
        private float debrisDownSpread = 1f; // 입자가 화면 아래로 퍼지는 비율
        private ParticleSystem.Particle[] debrisBuffer;

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

        private void OnDisable() => impactTween?.Kill();

        private void LateUpdate() => FaceCamera();

        private void SetProgress(float value)
        {
            progress = value;
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
                if (debrisUpSpread < 1f || debrisDownSpread < 1f) FlattenDebris(camera);
            }

            transform.position = position;
            this.size = size;
            impactTween?.Kill();
            progress = 0f;
            roll = Random.Range(-rollRange, rollRange);

            properties.SetFloat(SeedId, Random.Range(0f, 100f));
            properties.SetFloat(ProgressId, 0f);
            meshRenderer.SetPropertyBlock(properties);

            gameObject.SetActive(true);
            FaceCamera();
            UpdateLight(0f);
            impactTween = DOTween.To(() => progress, SetProgress, 1f, Mathf.Max(seconds, 0.01f))
                .SetEase(Ease.Linear).SetUpdate(UpdateType.Late, useUnscaledTime).SetTarget(this)
                .OnComplete(() => gameObject.SetActive(false)).OnKill(() => impactTween = null);
        }

        // 만든 쪽(분신 등)이 먼저 삭제돼도 재생 중인 충격과 입자를 끝까지 보여 준 뒤 지움
        public void DestroyAfterPlaying()
        {
            float remain = impactTween != null && impactTween.IsActive() ? impactTween.Duration(false) - impactTween.Elapsed(false) : 0f;
            if (debris != null && debris.IsAlive(true)) remain = Mathf.Max(remain, debris.main.startLifetime.constantMax);
            Destroy(gameObject, remain);
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

        // aspect: 충격의 가로/세로 비율(1이면 원래 모양), upSpread·downSpread: 입자가 화면 위·아래로 퍼지는 비율(1이면 고르게)
        // 둥글게 사방으로 퍼져 해처럼 보이지 않도록 가로로 긴 타원으로 만들 때 씀
        public void SetShape(float aspect, float upSpread, float downSpread)
        {
            this.aspect = Mathf.Max(aspect, 0.1f);
            debrisUpSpread = Mathf.Clamp01(upSpread);
            debrisDownSpread = Mathf.Clamp01(downSpread);
        }

        // 이 충격만 머티리얼 값을 바꿈, axisBias: 가로 줄기만 길게 하는 정도, rayDensity: 줄기 수, coreSize: 가운데 둥근 섬광 크기
        public void SetLook(float axisBias, float rayDensity, float coreSize)
        {
            properties.SetFloat(AxisBiasId, axisBias);
            properties.SetFloat(RayDensityId, rayDensity);
            properties.SetFloat(CoreSizeId, coreSize);
        }

        // false면 히트스톱 동안 멈춰 있다가 슬로모션 배율대로 느리게 퍼짐(마무리 일격)
        public void SetUnscaledTime(bool unscaled)
        {
            useUnscaledTime = unscaled;
            if (debris == null) return;

            ParticleSystem.MainModule main = debris.main;
            main.useUnscaledTime = unscaled;
        }

        // count가 0이거나 material이 없으면 입자를 쓰지 않음, speed·lifetime은 (최소, 최대)
        // spawnFill: 뿜는 자리가 반지름 안을 채우는 정도(0이면 구 표면에서만 나와 둥근 테두리로 보임, 1이면 안쪽 어디서나 제각각)
        // 입자는 충격보다 오래 남으므로 사각형과 따로 둔다.
        public void SetDebris(Material material, int count, Vector2 speed, Vector2 lifetime, float particleSize, float spawnRadius, Color tipColor, float spawnFill)
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
            main.useUnscaledTime = useUnscaledTime;
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
            shape.radiusThickness = Mathf.Clamp01(spawnFill);

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
            debrisRenderer.velocityScale = DebrisVelocityScale;
            debrisRenderer.lengthScale = DebrisLengthScale;
            debrisRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            debrisRenderer.receiveShadows = false;

            // 같은 머티리얼을 쓰는 사망 잔재는 다이아몬드로 두고, 피격 입자만 빛줄기로
            var debrisProperties = new MaterialPropertyBlock();
            debrisRenderer.GetPropertyBlock(debrisProperties);
            debrisProperties.SetFloat(StreakId, 1f);
            debrisRenderer.SetPropertyBlock(debrisProperties);
        }

        #endregion

        // 방금 뿜은 입자(맨 뒤에 붙음)의 위치와 속도를 화면 세로로 눌러 가로로 퍼지게 함, 위로 가는 입자와 아래로 가는 입자를 따로 누름
        private void FlattenDebris(Camera camera)
        {
            if (camera == null) return;

            if (debrisBuffer == null || debrisBuffer.Length < debris.main.maxParticles)
                debrisBuffer = new ParticleSystem.Particle[debris.main.maxParticles];

            int count = debris.GetParticles(debrisBuffer);
            Vector3 up = camera.transform.up;
            Vector3 center = debris.transform.position;
            for (int index = Mathf.Max(0, count - debrisCount); index < count; index++)
            {
                bool goingUp = Vector3.Dot(debrisBuffer[index].velocity, up) > 0f;
                float squash = 1f - (goingUp ? debrisUpSpread : debrisDownSpread);
                Vector3 offset = debrisBuffer[index].position - center;
                debrisBuffer[index].position -= up * (Vector3.Dot(offset, up) * squash);
                debrisBuffer[index].velocity -= up * (Vector3.Dot(debrisBuffer[index].velocity, up) * squash);
            }

            debris.SetParticles(debrisBuffer, count);
        }

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
            transform.localScale = new Vector3(size * aspect, size, size);
        }
    }
}
