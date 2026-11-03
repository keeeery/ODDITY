using System;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 피격 연출: 모든 피격은 윤곽 번쩍임과 충격·점광원·입자, 중간 피격은 더 큰 충격과 히트스탑·카메라 흔들림을 더하고
    // (약한 피격은 기본 공격처럼 자주 들어와 히트스탑을 걸지 않음)
    // 강한 피격은 가장 큰 충격·밝은 점광원·긴 히트스탑·센 흔들림에 카메라 줌 펀치까지 더함
    // 구간은 받은 피해를 공격자의 현재 공격력과 비교해 정하므로, 공격력 칩을 끼거나 보스가 달라도 같은 공격은 같은 구간이 된다.
    // 구간마다 피격음도 따로 틂, 마무리 일격 소리는 슬로모션과 상관없이 실제 시간으로 나옴
    // 숨을 끊는 피격(마무리 일격)은 가장 큰 충격과 많은 입자를 슬로모션 배율대로 느리게 퍼뜨림, 히트스탑·번쩍임·흔들림은 사망 상태가 맡음
    // 루트 캡슐 기준 맞은 지점은 허공일 수 있어, 충격은 부위 콜라이더 표면으로 옮겨서 터뜨린다.
    [DisallowMultipleComponent]
    public sealed class BossHitFeedback : MonoBehaviour
    {
        private enum HitTier
        {
            Weak,
            Medium,
            Strong
        }

        [Title("Hit Flash")]
        [Tooltip("윤곽이 번쩍였다가 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;

        [Title("Tier Threshold")]
        [Tooltip("실제로 받은 피해(방어력을 뺀 값)가 공격자 현재 공격력 × 이 값 이상이면 중간 피격, 강한 피격 조건을 먼저 적용한다.")]
        [SerializeField, Min(0.1f)] private float mediumHitAtkScale = 2.5f;
        [Tooltip("실제로 받은 피해가 공격자 현재 공격력 × 이 값 이상이거나 Knockdown 공격이면 강한 피격으로 본다.")]
        [SerializeField, Min(0.1f)] private float strongHitAtkScale = 6f;
        [Tooltip("공격자의 공격력을 알 수 없을 때(공격자가 사라졌거나 CombatEntity가 아닐 때) 대신 쓰는 공격력")]
        [SerializeField, Min(1)] private int fallbackAtk = 10;

        [Title("Medium Hit")]
        [Tooltip("중간 피격의 히트스탑 시간(초), 0이면 쓰지 않음")]
        [SerializeField, Min(0f)] private float mediumHitStop = 0.07f;
        [Tooltip("중간 피격의 카메라 trauma(0~1), 실제 세기는 trauma², 0이면 쓰지 않음")]
        [SerializeField, Range(0f, 1f)] private float mediumShakeTrauma = 0.45f;
        [Tooltip("중간 피격의 카메라 흔들림이 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float mediumShakeFadeTime = 0.3f;
        [Tooltip("중간 피격의 충격 크기 배율, 약한 피격의 Impact Size 기준")]
        [SerializeField, Min(1f)] private float mediumImpactSizeMultiplier = 1.5f;
        [Tooltip("중간 피격의 입자 수 배율, Debris Count 기준")]
        [SerializeField, Min(1f)] private float mediumDebrisCountMultiplier = 2f;
        [Tooltip("중간 피격의 입자 속도 배율, 높일수록 더 멀리 퍼진다.")]
        [SerializeField, Min(1f)] private float mediumDebrisSpeedMultiplier = 1.2f;

        [Title("Strong Hit")]
        [Tooltip("강한 피격의 히트스탑 시간(초), 0이면 쓰지 않음")]
        [SerializeField, Min(0f)] private float strongHitStop = 0.12f;
        [Tooltip("강한 피격의 카메라 trauma(0~1), 실제 세기는 trauma², 0이면 쓰지 않음")]
        [SerializeField, Range(0f, 1f)] private float strongShakeTrauma = 0.7f;
        [Tooltip("강한 피격의 카메라 흔들림이 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float strongShakeFadeTime = 0.45f;
        [Tooltip("강한 피격의 충격 크기 배율, 약한 피격의 Impact Size 기준")]
        [SerializeField, Min(1f)] private float strongImpactSizeMultiplier = 2.3f;
        [Tooltip("강한 피격의 충격이 터졌다가 사라지는 시간(초), 약한·중간 피격은 Impact Duration을 쓴다.")]
        [SerializeField, Min(0.05f)] private float strongImpactDuration = 0.45f;
        [Tooltip("강한 피격의 입자 수 배율, Debris Count 기준")]
        [SerializeField, Min(1f)] private float strongDebrisCountMultiplier = 3.5f;
        [Tooltip("강한 피격의 입자 속도 배율, 높일수록 더 멀리 퍼진다.")]
        [SerializeField, Min(1f)] private float strongDebrisSpeedMultiplier = 1.45f;
        [Tooltip("강한 피격의 입자 크기 배율, Debris Size 기준")]
        [SerializeField, Min(1f)] private float strongDebrisSizeMultiplier = 1.5f;
        [Tooltip("강한 피격의 입자가 남아 있는 시간 배율, Debris Lifetime 기준")]
        [SerializeField, Min(1f)] private float strongDebrisLifetimeMultiplier = 1.5f;
        [Tooltip("강한 피격 점광원의 세기·닿는 거리 배율, Impact Light 기준")]
        [SerializeField, Min(1f)] private float strongLightMultiplier = 1.6f;
        [Tooltip("강한 피격에 카메라 FOV를 좁히는 각도(도), 0이면 줌 펀치를 하지 않는다.")]
        [SerializeField, Range(0f, 15f)] private float strongZoomFov = 3f;
        [Tooltip("강한 피격 줌 펀치에서 FOV를 좁히는 데 걸리는 시간(실제 초)")]
        [SerializeField, Min(0.01f)] private float strongZoomInDuration = 0.05f;
        [Tooltip("강한 피격 줌 펀치에서 FOV를 되돌리는 시간(실제 초), 히트스탑이 끝난 뒤부터 되돌린다.")]
        [SerializeField, Min(0.01f)] private float strongZoomReleaseDuration = 0.25f;

        [Title("Finishing Hit")]
        [Tooltip("마무리 일격의 충격 크기 배율, 약한 피격의 Impact Size 기준")]
        [SerializeField, Min(1f)] private float finishImpactSizeMultiplier = 3.8f;
        [Tooltip("마무리 일격의 입자 수 배율, Debris Count 기준")]
        [SerializeField, Min(1f)] private float finishDebrisCountMultiplier = 10f;
        [Tooltip("마무리 일격의 입자 속도 배율, 슬로모션으로 느려지므로 강한 피격보다 멀리 퍼지게")]
        [SerializeField, Min(1f)] private float finishDebrisSpeedMultiplier = 1.7f;
        [Tooltip("마무리 일격의 입자 크기 배율, Debris Size 기준")]
        [SerializeField, Min(1f)] private float finishDebrisSizeMultiplier = 2.5f;
        [Tooltip("마무리 일격의 입자가 남아 있는 시간 배율, Debris Lifetime 기준(게임 초라 슬로모션 중에는 더 길게 보인다)")]
        [SerializeField, Min(1f)] private float finishDebrisLifetimeMultiplier = 4f;
        [Tooltip("마무리 일격의 충격이 터졌다가 사라지는 시간(게임 초), 슬로모션 중에는 실제로 더 길게 보인다.")]
        [SerializeField, Min(0.05f)] private float finishImpactDuration = 1f;
        [Tooltip("마무리 일격 충격의 가로/세로 비율, 1보다 크면 가로로 긴 타원이 되어 둥근 해처럼 보이지 않는다.")]
        [SerializeField, Range(1f, 3f)] private float finishImpactAspect = 1.8f;
        [Tooltip("마무리 일격 입자가 화면 위로 퍼지는 비율(0~1), 가로(1) 기준으로 작을수록 덜 퍼진다.")]
        [SerializeField, Range(0f, 1f)] private float finishDebrisUpSpread = 0.65f;
        [Tooltip("마무리 일격 입자가 화면 아래로 퍼지는 비율(0~1), 가로(1) 기준으로 작을수록 덜 퍼진다.")]
        [SerializeField, Range(0f, 1f)] private float finishDebrisDownSpread = 0.45f;
        [Tooltip("마무리 일격 충격을 화면 가로에서 무작위로 기울이는 최대 각도(도), 작을수록 늘 가로로 눕는다.")]
        [SerializeField, Range(0f, 180f)] private float finishImpactRollRange = 8f;
        [Tooltip("마무리 일격이 터질 자리, 0: 맞은 자리, 1: 카메라에서 몸 중심을 바라봤을 때 보이는 몸 표면. 사이 값은 둘을 섞은 곳을 바라본다.")]
        [SerializeField, Range(0f, 1f)] private float finishCenterBias = 1f;
        [Tooltip("마무리 일격에서 가로 줄기만 길고 세로 줄기는 짧게 하는 정도(0~1), 클수록 사방으로 뻗는 해 모양에서 멀어진다. 머티리얼의 Slash Axis Bias 대신 쓴다.")]
        [SerializeField, Range(0f, 1f)] private float finishAxisBias = 0.75f;
        [Tooltip("마무리 일격의 빛줄기 수(0~1), 머티리얼의 Ray Density 대신 쓴다.")]
        [SerializeField, Range(0f, 1f)] private float finishRayDensity = 0.2f;
        [Tooltip("마무리 일격 가운데 둥근 섬광 크기, 머티리얼의 Core Size 대신 쓴다.")]
        [SerializeField, Range(0f, 0.5f)] private float finishCoreSize = 0.16f;

        [Title("Impact")]
        [Tooltip("TeamOdd/Hit Impact 셰이더 머티리얼, 빛줄기 굵기·베기 섬광 세기·타원 정도·몸에 가려지는 정도는 머티리얼에서 조정한다.")]
        [SerializeField] private Material impactMaterial;
        [Tooltip("약한 피격의 충격 크기(m), 빛줄기가 뻗는 지름, 중간·강한 피격은 배율을 곱한다.")]
        [SerializeField, Min(0.1f)] private float impactSize = 3f;
        [Tooltip("충격이 터졌다가 사라지는 시간(초)")]
        [SerializeField, Min(0.05f)] private float impactDuration = 0.3f;
        [Tooltip("맞은 표면에 반쯤 묻히지 않도록 카메라 쪽으로 당기는 거리(m), 너무 크면 몸에서 떠 보인다.")]
        [SerializeField, Min(0f)] private float impactTowardCamera = 0.15f;
        [Tooltip("터질 때마다 화면 가로에서 양쪽으로 무작위로 기울이는 최대 각도(도), 0이면 항상 가로, 180이면 완전히 무작위")]
        [SerializeField, Range(0f, 180f)] private float impactRollRange = 25f;
        [Tooltip("연달아 맞아도 끊기지 않도록 미리 만들어 돌려 쓰는 개수")]
        [SerializeField, Min(1)] private int impactPoolSize = 3;

        [Title("Surface Snap")]
        [Tooltip("맞은 지점에서 이 거리(m) 안에 카메라에서 보이는 몸 표면이 있으면 그 자리로 옮긴다. 없으면 가장 가까운 부위 표면으로 옮긴다.")]
        [SerializeField, Min(0f)] private float surfaceSnapDistance = 1.5f;

        [Title("Impact Light")]
        [Tooltip("충격과 함께 잠깐 켜져 보스 표면을 밝히는 점광원 색")]
        [SerializeField] private Color impactLightColor = new(0.85f, 0.93f, 1f);
        [Tooltip("점광원이 가장 밝을 때의 세기, 0이면 점광원을 쓰지 않는다.")]
        [SerializeField, Min(0f)] private float impactLightIntensity = 8f;
        [Tooltip("점광원이 닿는 거리(m)")]
        [SerializeField, Min(0.1f)] private float impactLightRange = 4f;

        [Title("Impact Debris")]
        [Tooltip("TeamOdd/Hit Debris 셰이더 머티리얼, 비워 두면 입자를 뿜지 않는다.")]
        [SerializeField] private Material debrisMaterial;
        [Tooltip("한 번에 뿜는 입자 수, 0이면 입자를 뿜지 않는다.")]
        [SerializeField, Min(0)] private int debrisCount = 12;
        [Tooltip("입자가 튀어 나가는 속도(m/s), x: 최소, y: 최대. 끌림이 있어 빠르게 감속한다.")]
        [SerializeField] private Vector2 debrisSpeed = new(6f, 12f);
        [Tooltip("입자가 남아 있는 시간(초), x: 최소, y: 최대")]
        [SerializeField] private Vector2 debrisLifetime = new(0.3f, 0.6f);
        [Tooltip("입자 크기(m), 빠르게 날 때는 이보다 길게 늘어난다.")]
        [SerializeField, Min(0.01f)] private float debrisSize = 0.08f;
        [Tooltip("입자가 시작하는 구의 반지름, 충격 크기(Impact Size)에 대한 비율. 클수록 충격 바깥쪽에서 시작한다.")]
        [SerializeField, Range(0f, 0.5f)] private float debrisSpawnRadius = 0.15f;
        [Tooltip("입자가 뿜는 반지름 안을 채우는 정도(0~1), 0이면 둥근 테두리에서만 나오고 1이면 안쪽 곳곳에서 제각각 퍼진다.")]
        [SerializeField, Range(0f, 1f)] private float debrisSpawnFill = 0.85f;
        [Tooltip("입자가 사라질 때 물드는 색, 충격 머티리얼의 Tip Color와 맞추면 자연스럽다.")]
        [SerializeField] private Color debrisTipColor = new(0.6f, 0.9f, 1f);

        [Title("Hit Sound")]
        [Tooltip("약한 피격 소리, 기본 공격마다 나오므로 중간·강한 피격보다 작게 둔다.")]
        [SerializeField] private SoundData weakHitSound;
        [Tooltip("중간 피격 소리")]
        [SerializeField] private SoundData mediumHitSound;
        [Tooltip("강한 피격 소리")]
        [SerializeField] private SoundData strongHitSound;
        [Tooltip("마무리 일격 소리, 슬로모션과 상관없이 실제 시간으로 재생된다.")]
        [SerializeField] private SoundData finishHitSound;

        private ICombatable self;
        private HitFlash flash;
        private CombatManager combatManager;
        private Collider[] bodyParts;
        // 구간마다 크기·입자 설정이 달라 따로 돌려 씀, HitTier 순서
        private readonly HitImpact[][] impactPools = new HitImpact[3][];
        private readonly int[] nextImpacts = new int[3];
        private HitImpact finishImpact;

        private void Awake()
        {
            self = GetComponent<ICombatable>();
            flash = HitFlash.GetOrAdd(gameObject);
            if (self == null) Debug.LogWarning($"[{nameof(BossHitFeedback)}] {name}에 ICombatable이 없어 피격 연출을 보여주지 않습니다.", this);

            // 루트 캡슐·트리거는 몸 표면이 아니고, 오목한 MeshCollider는 ClosestPoint를 못 써서 뺌
            Collider ownCollider = self?.Collider;
            bodyParts = Array.FindAll(GetComponentsInChildren<Collider>(true),
                part => part.isTrigger == false && part != ownCollider && part is not MeshCollider { convex: false });
        }

        private void Start()
        {
            combatManager = CombatManager.Instance;
            combatManager.OnDamageEventExecuted += HandleDamageExecuted;
        }

        private void OnDestroy()
        {
            if (combatManager != null) combatManager.OnDamageEventExecuted -= HandleDamageExecuted;

            foreach (HitImpact[] pool in impactPools) DestroyImpacts(pool);
            if (finishImpact != null) finishImpact.DestroyAfterPlaying();
        }

        private static void DestroyImpacts(HitImpact[] impacts)
        {
            if (impacts == null) return;
            foreach (HitImpact impact in impacts)
                if (impact != null) impact.DestroyAfterPlaying();
        }

        private void HandleDamageExecuted(DamageEvent damageEvent, int receivedDamage)
        {
            if (receivedDamage <= 0 || self == null || ReferenceEquals(damageEvent.Receiver, self) == false) return;

            if (self is CombatEntity { IsDead: true })
            {
                PlayFinishImpact(GetFinishPoint(damageEvent.Info.HitPoint));
                finishHitSound.Play();
                return;
            }

            flash.Play(flashDuration);
            HitTier tier = GetTier(damageEvent, receivedDamage);
            PlayImpact(SnapToBody(damageEvent.Info.HitPoint), tier);
            switch (tier)
            {
                case HitTier.Strong:
                    strongHitSound.Play();
                    HitStop.Request(strongHitStop);
                    ShakeCamera(strongShakeTrauma, strongShakeFadeTime);
                    PunchZoom();
                    break;
                case HitTier.Medium:
                    mediumHitSound.Play();
                    HitStop.Request(mediumHitStop);
                    ShakeCamera(mediumShakeTrauma, mediumShakeFadeTime);
                    break;
                default:
                    weakHitSound.Play();
                    break;
            }
        }

        // 공격력 칩·정당한 보복으로 오른 공격력까지 반영하도록 맞을 때마다 공격자의 현재 공격력을 읽음
        private HitTier GetTier(DamageEvent damageEvent, int receivedDamage)
        {
            if (damageEvent.Info.Reaction == HitReaction.Knockdown) return HitTier.Strong;

            int atk = damageEvent.Sender is CombatEntity attacker && attacker != null ? attacker.CurrentStats.Atk : 0;
            if (atk <= 0) atk = fallbackAtk;

            if (receivedDamage >= atk * strongHitAtkScale) return HitTier.Strong;
            if (receivedDamage >= atk * mediumHitAtkScale) return HitTier.Medium;
            return HitTier.Weak;
        }

        private static void ShakeCamera(float trauma, float fadeTime)
        {
            if (trauma <= 0f) return;

            Player.Player player = Player.Player.Instance;
            if (player != null && player.CameraController != null) player.CameraController.AddTrauma(trauma, fadeTime);
        }

        // 히트스탑 동안 좁힌 채로 머물렀다가 풀리면 되돌림
        private void PunchZoom()
        {
            if (strongZoomFov <= 0f) return;

            Player.Player player = Player.Player.Instance;
            if (player == null || player.CameraController == null) return;

            float hold = Mathf.Max(0f, strongHitStop - strongZoomInDuration);
            player.CameraController.PunchFov(strongZoomFov, strongZoomInDuration, hold, strongZoomReleaseDuration);
        }

        private void PlayImpact(Vector3 position, HitTier tier)
        {
            if (impactMaterial == null) return;

            int poolIndex = (int)tier;
            HitImpact[] pool = impactPools[poolIndex] ??= CreateImpacts(tier);
            HitImpact impact = pool[nextImpacts[poolIndex]];
            nextImpacts[poolIndex] = (nextImpacts[poolIndex] + 1) % pool.Length;

            float duration = tier == HitTier.Strong ? strongImpactDuration : impactDuration;
            impact.Play(position, GetImpactSize(tier), duration, impactTowardCamera, impactRollRange);
        }

        private float GetImpactSize(HitTier tier) => tier switch
        {
            HitTier.Strong => impactSize * strongImpactSizeMultiplier,
            HitTier.Medium => impactSize * mediumImpactSizeMultiplier,
            _ => impactSize
        };

        // 한 번뿐이라 풀 없이 처음 쓸 때 하나만 만듦
        private void PlayFinishImpact(Vector3 position)
        {
            if (impactMaterial == null) return;

            float size = impactSize * finishImpactSizeMultiplier;
            if (finishImpact == null)
            {
                finishImpact = HitImpact.Create(impactMaterial, $"{name} Finish Impact");
                finishImpact.SetUnscaledTime(false);
                finishImpact.SetShape(finishImpactAspect, finishDebrisUpSpread, finishDebrisDownSpread);
                finishImpact.SetLook(finishAxisBias, finishRayDensity, finishCoreSize);
                finishImpact.SetLight(impactLightColor, impactLightIntensity * 1.5f, impactLightRange * 1.5f);
                finishImpact.SetDebris(debrisMaterial, Mathf.RoundToInt(debrisCount * finishDebrisCountMultiplier),
                    debrisSpeed * finishDebrisSpeedMultiplier, debrisLifetime * finishDebrisLifetimeMultiplier,
                    debrisSize * finishDebrisSizeMultiplier, size * debrisSpawnRadius, debrisTipColor, debrisSpawnFill);
            }

            finishImpact.Play(position, size, finishImpactDuration, impactTowardCamera, finishImpactRollRange);
        }

        private HitImpact[] CreateImpacts(HitTier tier)
        {
            float countScale = 1f, speedScale = 1f, particleSizeScale = 1f, lifetimeScale = 1f, lightScale = 1f;
            if (tier == HitTier.Medium)
            {
                countScale = mediumDebrisCountMultiplier;
                speedScale = mediumDebrisSpeedMultiplier;
            }
            else if (tier == HitTier.Strong)
            {
                countScale = strongDebrisCountMultiplier;
                speedScale = strongDebrisSpeedMultiplier;
                particleSizeScale = strongDebrisSizeMultiplier;
                lifetimeScale = strongDebrisLifetimeMultiplier;
                lightScale = strongLightMultiplier;
            }

            int count = Mathf.RoundToInt(debrisCount * countScale);
            float size = GetImpactSize(tier);
            HitImpact[] result = new HitImpact[impactPoolSize];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = HitImpact.Create(impactMaterial, $"{name} {tier} Hit Impact");
                result[index].SetLight(impactLightColor, impactLightIntensity * lightScale, impactLightRange * lightScale);
                result[index].SetDebris(debrisMaterial, count, debrisSpeed * speedScale, debrisLifetime * lifetimeScale,
                    debrisSize * particleSizeScale, size * debrisSpawnRadius, debrisTipColor, debrisSpawnFill);
            }
            return result;
        }

        // 카메라에서 몸 중심(맞은 자리와 섞은 곳)을 바라봤을 때 처음 닿는 몸 표면, 늘 몸통 한가운데의 보이는 쪽에서 터지도록
        // 시선이 몸에 닿지 않으면 그 자리에서 가장 가까운 표면
        private Vector3 GetFinishPoint(Vector3 hitPoint)
        {
            if (bodyParts.Length == 0 || TryGetBodyCenter(out Vector3 center) == false) return SnapToBody(hitPoint);

            Vector3 target = Vector3.Lerp(hitPoint, center, finishCenterBias);
            Camera camera = Camera.main;
            if (camera == null) return SnapToBody(target);

            Vector3 origin = camera.transform.position;
            Vector3 toTarget = target - origin;
            float targetDistance = toTarget.magnitude;
            if (targetDistance <= 0.001f) return SnapToBody(target);

            var ray = new Ray(origin, toTarget / targetDistance);
            float bestDistance = float.MaxValue;
            foreach (Collider part in bodyParts)
            {
                if (part == null || part.enabled == false || part.gameObject.activeInHierarchy == false) continue;
                if (part.Raycast(ray, out RaycastHit hit, targetDistance + surfaceSnapDistance) && hit.distance < bestDistance)
                    bestDistance = hit.distance;
            }

            return bestDistance < float.MaxValue ? ray.GetPoint(bestDistance) : SnapToBody(target);
        }

        // 켜진 부위 콜라이더를 모두 감싸는 바운드의 중심
        private bool TryGetBodyCenter(out Vector3 center)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Collider part in bodyParts)
            {
                if (part == null || part.enabled == false || part.gameObject.activeInHierarchy == false) continue;

                if (hasBounds) bounds.Encapsulate(part.bounds);
                else bounds = part.bounds;
                hasBounds = true;
            }

            center = bounds.center;
            return hasBounds;
        }

        // 시선이 맞은 지점 근처에서 몸에 닿으면 그 자리, 아니면 가장 가까운 부위 표면
        private Vector3 SnapToBody(Vector3 point)
        {
            if (bodyParts.Length == 0) return point;

            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 origin = camera.transform.position;
                Vector3 toPoint = point - origin;
                float pointDistance = toPoint.magnitude;
                if (pointDistance > 0.001f)
                {
                    var ray = new Ray(origin, toPoint / pointDistance);
                    float bestDistance = float.MaxValue;
                    foreach (Collider part in bodyParts)
                    {
                        if (part == null || part.enabled == false || part.gameObject.activeInHierarchy == false) continue;
                        if (part.Raycast(ray, out RaycastHit hit, pointDistance + surfaceSnapDistance) && hit.distance < bestDistance)
                            bestDistance = hit.distance;
                    }

                    if (Mathf.Abs(bestDistance - pointDistance) <= surfaceSnapDistance) return ray.GetPoint(bestDistance);
                }
            }

            Vector3 closest = point;
            float closestDistance = float.MaxValue;
            foreach (Collider part in bodyParts)
            {
                if (part == null || part.enabled == false || part.gameObject.activeInHierarchy == false) continue;

                Vector3 candidate = part.ClosestPoint(point);
                float distance = (candidate - point).sqrMagnitude;
                if (distance >= closestDistance) continue;

                closest = candidate;
                closestDistance = distance;
            }
            return closest;
        }
    }
}
