using System.Collections.Generic;
using TeamOdd.Combat;
using TeamOdd.Player;
using UnityEngine;

namespace TeamOdd.Psychokinesis
{
    // 염동력 기본 공격(염동파): 손을 당겼다 뻗는 순간 염동탄을 쏘고, 염동탄이 닿는 순간 피해·충격파·밀어내기를 처리한다.
    // 조준은 손을 떠나는 순간 화면 중앙으로 다시 잡고, 맞은 콜라이더를 따라가므로 움직이는 적에게도 닿은 자리에서 터진다.
    // 연출은 HitImpact처럼 코드로 만들어 돌려 쓰고, 머티리얼이 비어 있으면 그 연출만 생략한다.
    [DisallowMultipleComponent]
    public sealed class PsychokinesisForceWave : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int StartRadiusId = Shader.PropertyToID("_StartRadius");
        private static readonly int EndRadiusId = Shader.PropertyToID("_EndRadius");
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        [Header("Projectile")]
        [Tooltip("다 가속했을 때 염동탄이 날아가는 속도(m/s), 피해는 염동탄이 닿을 때 들어간다.")]
        [SerializeField, Min(1f)] private float projectileSpeed = 55f;
        [Tooltip("손 앞에 머물며 부풀어 오르는 시간(초)")]
        [SerializeField, Min(0f)] private float lingerDuration = 0.1f;
        [Tooltip("머무는 동안 앞으로 밀려 나가는 속도(m/s)")]
        [SerializeField, Min(0f)] private float lingerSpeed = 2.5f;
        [Tooltip("머문 뒤 최고 속도까지 가속하는 시간(초), 처음엔 느리고 갈수록 빠르게 붙는다.")]
        [SerializeField, Min(0.01f)] private float accelerationDuration = 0.12f;
        [Tooltip("손 앞에 머무는 동안 염동탄이 부풀어 오르는 최대 배율, 날아가기 시작하면 원래 크기로 돌아온다.")]
        [SerializeField, Min(1f)] private float lingerSwell = 1.2f;
        [Tooltip("손바닥에서 앞으로 띄워 출발하는 거리(m)")]
        [SerializeField, Min(0f)] private float handForwardOffset = 0.15f;

        [Header("Hand Burst")]
        [Tooltip("TeamOdd/Force Ripple 머티리얼, 손 앞 응축 고리·염동탄·꼬리 고리·착탄 충격파가 함께 쓴다.")]
        [SerializeField] private Material rippleMaterial;
        [SerializeField, Min(0.1f)] private float handRippleSize = 2.03f;
        [SerializeField, Min(0.01f)] private float handRippleDuration = 0.18f;

        [Header("Force Bullet")]
        [Tooltip("날아가는 염동탄(볼록 렌즈)의 지름(m)")]
        [SerializeField, Min(0.05f)] private float bulletSize = 1.22f;
        [Tooltip("꼬리 고리를 남기는 간격(m)")]
        [SerializeField, Min(0.1f)] private float trailRingInterval = 0.9f;
        [SerializeField, Min(0.05f)] private float trailRingSize = 1.63f;
        [SerializeField, Min(0.01f)] private float trailRingDuration = 0.22f;

        [Header("Impact")]
        [Tooltip("염동탄이 닿을 때 주는 무력화 피해")]
        [SerializeField, Min(0)] private int groggyDamage = 8;
        [SerializeField, Min(0.1f)] private float impactRippleSize = 5.53f;
        [SerializeField, Min(0.01f)] private float impactRippleDuration = 0.3f;

        [Header("Push")]
        [Tooltip("착탄 지점에서 이 반지름(m) 안의 물체를 민다.")]
        [SerializeField, Min(0f)] private float pushRadius = 1.6f;
        [SerializeField, Min(0f)] private float pushDistance = 1.8f;
        [SerializeField, Min(0.01f)] private float pushDuration = 0.22f;
        [Tooltip("이 질량 이상은 밀리지 않고 흔들리기만 한다.")]
        [SerializeField, Min(0.01f)] private float heavyMass = 20f;
        [SerializeField, Range(0f, 30f)] private float wobbleAngle = 8f;

        private sealed class Effect
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Properties;
            public float Elapsed;
            public float Duration;
            public bool Billboard;
            public bool Timed; // false면 염동탄이 직접 움직이고 끝냄
            public int ActiveIndex = -1;
        }

        private sealed class Shot
        {
            public Vector3 Start;
            public Vector3 WorldEnd;
            public Transform Anchor;
            public Vector3 LocalEnd;
            public ICombatable Target;
            public Collider HitCollider;
            public bool HasHit;
            public float Traveled;
            public float Age;
            public bool Released; // 머무는 시간이 끝나 가속하기 시작했는지
            public float NextRing;
            public Effect Bullet;
        }

        private readonly List<float> pendingLaunches = new();
        private readonly List<Shot> shots = new();
        private readonly Stack<Shot> shotPool = new();
        private readonly Stack<Effect> effectPool = new();
        private readonly List<Effect> activeEffects = new();
        private readonly Collider[] overlap = new Collider[32];
        private readonly HashSet<Rigidbody> pushed = new();

        private PsychokinesisController controller;
        private PlayerController playerController;
        private CombatEntity combatEntity;
        private Animator animator;
        private Transform effectRoot;
        private Mesh quadMesh;

        private void Awake()
        {
            controller = GetComponent<PsychokinesisController>();
            playerController = GetComponent<PlayerController>();
            combatEntity = GetComponent<CombatEntity>();
            animator = GetComponent<Animator>();
        }

        private void OnDisable()
        {
            pendingLaunches.Clear();
            for (int index = shots.Count - 1; index >= 0; index--) EndShot(index);
        }

        private void OnDestroy()
        {
            if (effectRoot != null) Destroy(effectRoot.gameObject);
            if (quadMesh != null) Destroy(quadMesh);
        }

        // 애니메이션·IK가 끝난 손 위치에서 쏘도록 LateUpdate에서 처리
        private void LateUpdate()
        {
            if (playerController != null && playerController.IsHit) CancelPendingLaunches();

            while (pendingLaunches.Count > 0 && Time.time >= pendingLaunches[0])
            {
                pendingLaunches.RemoveAt(0);
                Launch();
            }

            for (int index = shots.Count - 1; index >= 0; index--)
                UpdateShot(index);

            UpdateEffects();
        }

        #region PUBLIC_METHODS

        // 손을 당겼다 뻗는 동작을 시작하고, 손이 앞으로 나가는 순간에 염동탄을 쏜다.
        public void Fire()
        {
            if (playerController != null && (playerController.IsHit || playerController.IsActionLocked)) return;

            float releaseDelay = playerController != null ? playerController.PlayAttackMotion() : 0f;
            pendingLaunches.Add(Time.time + releaseDelay);
        }

        // 이미 날아가는 탄은 유지하고, 아직 손을 떠나지 않은 공격만 취소한다.
        public void CancelPendingLaunches() => pendingLaunches.Clear();

        #endregion

        private void Launch()
        {
            if (controller == null) return;

            bool hasTarget = controller.TryFindAttackTarget(out Ray ray, out ICombatable target, out Collider hitCollider, out Vector3 end);
            Vector3 palm = GetPalmPosition();
            Vector3 toEnd = end - palm;
            Vector3 direction = toEnd.sqrMagnitude > 0.0001f ? toEnd.normalized : ray.direction;
            Vector3 start = palm + direction * handForwardOffset;

            // 손보다 가까운 곳(카메라와 플레이어 사이)을 맞혔으면 그 자리에서 바로 터뜨림
            if (Vector3.Dot(end - start, ray.direction) <= 0f) start = end;

            Shot shot = shotPool.Count > 0 ? shotPool.Pop() : new Shot();
            shot.Start = start;
            shot.WorldEnd = end;
            shot.Anchor = hitCollider != null ? hitCollider.transform : null;
            shot.LocalEnd = shot.Anchor != null ? shot.Anchor.InverseTransformPoint(end) : Vector3.zero;
            shot.Target = hasTarget ? target : null;
            shot.HitCollider = hitCollider;
            shot.HasHit = hitCollider != null;
            shot.Traveled = 0f;
            shot.Age = 0f;
            shot.Released = false;
            shot.NextRing = trailRingInterval;
            shot.Bullet = RentEffect(GetQuadMesh(), rippleMaterial, "Force Bullet");
            if (shot.Bullet != null) shot.Bullet.Timed = false;
            shots.Add(shot);

            // 손 앞 응축 고리는 힘이 모이는 순간, 한 번 더 퍼지는 고리는 머문 뒤 튀어 나가는 순간(ReleaseShot)에 냄
            PlayRipple(start, Quaternion.LookRotation(direction), false, handRippleSize, handRippleDuration, 0.15f, 0.9f);
        }

        // 손 앞에 머물렀다가 머무는 속도에서 최고 속도까지 점점 빠르게 붙음
        private float GetShotSpeed(float age)
        {
            if (age < lingerDuration) return lingerSpeed;

            float k = Mathf.Clamp01((age - lingerDuration) / accelerationDuration);
            return Mathf.Lerp(lingerSpeed, projectileSpeed, k * k);
        }

        private void ReleaseShot(Shot shot, Vector3 direction)
        {
            shot.Released = true;
            Vector3 position = shot.Start + direction * shot.Traveled;
            PlayRipple(position, Quaternion.LookRotation(direction), false, handRippleSize * 0.8f, handRippleDuration, 0.3f, 1f);
        }

        private void UpdateShot(int index)
        {
            Shot shot = shots[index];

            // 맞힌 대상이 움직이면 따라가고, 사라졌으면 마지막 위치에서 터짐
            if (shot.Anchor != null) shot.WorldEnd = shot.Anchor.TransformPoint(shot.LocalEnd);

            Vector3 toEnd = shot.WorldEnd - shot.Start;
            float total = toEnd.magnitude;
            Vector3 direction = total > 0.0001f ? toEnd / total : transform.forward;
            shot.Age += Time.deltaTime;
            float speed = GetShotSpeed(shot.Age);
            shot.Traveled += speed * Time.deltaTime;

            if (shot.Released == false && shot.Age >= lingerDuration) ReleaseShot(shot, direction);

            while (shot.NextRing < Mathf.Min(shot.Traveled, total))
            {
                PlayRipple(shot.Start + direction * shot.NextRing, Quaternion.LookRotation(direction), false, trailRingSize, trailRingDuration, 0.2f, 0.85f);
                shot.NextRing += trailRingInterval;
            }

            if (shot.Traveled >= total)
            {
                Impact(shot, direction);
                EndShot(index);
                return;
            }

            Vector3 position = shot.Start + direction * shot.Traveled;
            float spawnFade = Mathf.Clamp01(shot.Age / 0.05f);

            // 머무는 동안 작게 시작해 부풀었다가, 날아가기 시작하면 원래 크기로 줄어듦
            float swell = shot.Age < lingerDuration
                ? Mathf.Lerp(0.5f, lingerSwell, Mathf.SmoothStep(0f, 1f, shot.Age / Mathf.Max(lingerDuration, 0.0001f)))
                : Mathf.Lerp(lingerSwell, 1f, Mathf.Clamp01((shot.Age - lingerDuration) / accelerationDuration));

            if (shot.Bullet != null)
            {
                shot.Bullet.Transform.SetPositionAndRotation(position, GetBillboardRotation());
                shot.Bullet.Transform.localScale = Vector3.one * (bulletSize * swell);
                SetRippleProperties(shot.Bullet, 0f, 0f, 0f, 1f, spawnFade);
            }
        }

        private void Impact(Shot shot, Vector3 direction)
        {
            Vector3 point = shot.WorldEnd;

            if (shot.Target != null && shot.HitCollider != null && IsAlive(shot.Target) && combatEntity != null)
            {
                int damage = combatEntity.CurrentStats.Atk;
                int groggy = controller != null ? controller.CalculateAttackGroggy(damage, groggyDamage) : groggyDamage;
                DamageInfo info = new DamageInfo(damage, point, shot.HitCollider, groggy: groggy);
                CombatManager.Instance.AddCombatEvent(new DamageEvent(combatEntity, shot.Target, info));
            }

            // 허공으로 쏘면 사거리 끝에서 흩어지기만 함
            if (shot.HasHit == false) return;

            PlayRipple(point, GetBillboardRotation(), true, impactRippleSize, impactRippleDuration, 0.05f, 0.85f);
            PushBodies(point, direction);
        }

        private void EndShot(int index)
        {
            Shot shot = shots[index];
            if (shot.Bullet != null) Release(shot.Bullet);
            shot.Bullet = null;
            shot.Anchor = null;
            shot.Target = null;
            shot.HitCollider = null;
            shots.RemoveAt(index);
            shotPool.Push(shot);
        }

        private static bool IsAlive(ICombatable target)
        {
            if (target is Object unityObject && unityObject == null) return false;
            return target is not CombatEntity entity || (entity.IsDead == false && entity.isActiveAndEnabled);
        }

        // 손바닥 가운데, 손목과 가운뎃손가락 뿌리 사이
        private Vector3 GetPalmPosition()
        {
            if (animator == null) return transform.position + Vector3.up * 1.4f;

            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform finger = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            if (hand == null) return transform.position + Vector3.up * 1.4f;

            return finger != null ? Vector3.Lerp(hand.position, finger.position, 0.6f) : hand.position;
        }

        #region PUSH

        // 맞힌 방향 위주로, 중심에서 멀수록 바깥으로 조금 퍼지며 약하게 민다. 적과 플레이어는 밀지 않는다.
        private void PushBodies(Vector3 point, Vector3 direction)
        {
            if (pushRadius <= 0f || pushDistance <= 0f) return;

            int count = Physics.OverlapSphereNonAlloc(point, pushRadius, overlap, controller.QueryLayers, QueryTriggerInteraction.Ignore);
            pushed.Clear();

            for (int index = 0; index < count; index++)
            {
                Rigidbody body = overlap[index].attachedRigidbody;
                if (body == null || pushed.Add(body) == false) continue;
                if (body.transform.IsChildOf(transform) || body.GetComponentInParent<ICombatable>() != null) continue;

                Vector3 offset = body.worldCenterOfMass - point;
                Vector3 spread = Vector3.ProjectOnPlane(offset, direction).normalized * 0.35f;
                float falloff = 1f - 0.6f * Mathf.Clamp01(offset.magnitude / pushRadius);
                ForcePush.Push(body, (direction + spread).normalized, pushDistance * falloff, pushDuration, heavyMass, wobbleAngle);
            }
        }

        #endregion

        #region EFFECTS

        private void PlayRipple(Vector3 position, Quaternion rotation, bool billboard, float size, float duration, float startRadius, float endRadius)
        {
            Effect effect = RentEffect(GetQuadMesh(), rippleMaterial, "Force Ripple");
            if (effect == null) return;

            effect.Transform.SetPositionAndRotation(position, rotation);
            effect.Transform.localScale = Vector3.one * size;
            effect.Billboard = billboard;
            effect.Duration = duration;
            SetRippleProperties(effect, 0f, startRadius, endRadius, 0f, 1f);
        }

        private static void SetRippleProperties(Effect effect, float progress, float startRadius, float endRadius, float fill, float fade)
        {
            effect.Properties.SetFloat(ProgressId, progress);
            effect.Properties.SetFloat(StartRadiusId, startRadius);
            effect.Properties.SetFloat(EndRadiusId, endRadius);
            effect.Properties.SetFloat(FillId, fill);
            effect.Properties.SetFloat(FadeId, fade);
            effect.Renderer.SetPropertyBlock(effect.Properties);
        }

        // 시간이 정해진 연출(고리)만 진행시키고, 염동탄이 직접 움직이는 연출은 건너뜀
        private void UpdateEffects()
        {
            if (activeEffects.Count == 0) return;

            Quaternion billboard = GetBillboardRotation();
            // 반환 시 끝 항목을 옮기므로 이미 처리한 항목이 다시 갱신되지 않게 역순으로 순회한다.
            for (int index = activeEffects.Count - 1; index >= 0; index--)
            {
                Effect effect = activeEffects[index];
                if (effect.Timed == false) continue;

                effect.Elapsed += Time.deltaTime;
                float progress = effect.Elapsed / effect.Duration;
                if (progress >= 1f)
                {
                    Release(effect);
                    continue;
                }

                if (effect.Billboard) effect.Transform.rotation = billboard;
                effect.Properties.SetFloat(ProgressId, progress);
                effect.Properties.SetFloat(FadeId, 1f - progress * progress);
                effect.Renderer.SetPropertyBlock(effect.Properties);
            }
        }

        private Effect RentEffect(Mesh mesh, Material material, string effectName)
        {
            if (material == null) return null;

            Effect effect = effectPool.Count > 0 ? effectPool.Pop() : CreateEffect(mesh, material, effectName);
            effect.Timed = true;
            effect.Billboard = false;
            effect.Elapsed = 0f;
            effect.Duration = 0f;
            effect.ActiveIndex = activeEffects.Count;
            activeEffects.Add(effect);
            effect.Transform.gameObject.SetActive(true);
            return effect;
        }

        private Effect CreateEffect(Mesh mesh, Material material, string effectName)
        {
            GameObject effectObject = new GameObject(effectName);
            effectObject.transform.SetParent(GetEffectRoot(), false);
            effectObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = effectObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return new Effect
            {
                Transform = effectObject.transform,
                Renderer = meshRenderer,
                Properties = new MaterialPropertyBlock()
            };
        }

        private void Release(Effect effect)
        {
            if (effect.ActiveIndex < 0) return;

            int index = effect.ActiveIndex;
            int lastIndex = activeEffects.Count - 1;
            Effect last = activeEffects[lastIndex];
            activeEffects[index] = last;
            last.ActiveIndex = index;
            activeEffects.RemoveAt(lastIndex);
            effect.ActiveIndex = -1;
            if (effect.Transform != null) effect.Transform.gameObject.SetActive(false);
            effectPool.Push(effect);
        }

        private static Quaternion GetBillboardRotation()
        {
            Camera camera = Camera.main;
            return camera != null ? camera.transform.rotation : Quaternion.identity;
        }

        private Transform GetEffectRoot()
        {
            if (effectRoot == null) effectRoot = new GameObject($"{name} Force Wave Effects").transform;
            return effectRoot;
        }

        #endregion

        #region MESHES

        private Mesh GetQuadMesh()
        {
            if (quadMesh != null) return quadMesh;

            quadMesh = new Mesh { name = "Force Wave Quad" };
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

        #endregion
    }
}
