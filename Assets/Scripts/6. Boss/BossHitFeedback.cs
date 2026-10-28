using System;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 피격 연출: 모든 피격은 윤곽 번쩍임, 중간 피격부터 충격·점광원·입자와 히트스탑, 강한 피격은 더 큰 충격과 카메라 흔들림도 더함
    // 루트 캡슐 기준 맞은 지점은 허공일 수 있어, 충격은 부위 콜라이더 표면으로 옮겨서 터뜨린다.
    [DisallowMultipleComponent]
    public sealed class BossHitFeedback : MonoBehaviour
    {
        [Title("Hit Flash")]
        [Tooltip("윤곽이 번쩍였다가 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;

        [Title("Medium Hit")]
        [Tooltip("실제로 받은 피해(방어력을 뺀 값)가 이 값 이상이면 중간 피격, 강한 피격 조건을 먼저 적용한다.")]
        [SerializeField, Min(1)] private int mediumHitDamage = 20;
        [Tooltip("중간 피격의 히트스탑 시간(초), 0이면 쓰지 않음")]
        [SerializeField, Min(0f)] private float mediumHitStop = 0.03f;

        [Title("Strong Hit")]
        [Tooltip("실제로 받은 피해(방어력을 뺀 값)가 이 값 이상이거나 Knockdown 공격이면 강한 피격으로 본다.")]
        [SerializeField, Min(1)] private int strongHitDamage = 60;
        [Tooltip("강한 피격의 히트스탑 시간(초), 0이면 쓰지 않음")]
        [SerializeField, Min(0f)] private float strongHitStop = 0.07f;
        [Tooltip("강한 피격의 카메라 trauma(0~1), 실제 세기는 trauma², 0이면 쓰지 않음")]
        [SerializeField, Range(0f, 1f)] private float strongShakeTrauma = 0.45f;
        [Tooltip("강한 피격의 카메라 흔들림이 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float strongShakeFadeTime = 0.3f;
        [Tooltip("강한 피격의 충격 크기 배율, 중간 피격의 Impact Size 기준")]
        [SerializeField, Min(1f)] private float strongImpactSizeMultiplier = 1.5f;
        [Tooltip("강한 피격의 입자 수 배율, Debris Count 기준")]
        [SerializeField, Min(1f)] private float strongDebrisCountMultiplier = 2f;
        [Tooltip("강한 피격의 입자 속도 배율, 높일수록 더 멀리 퍼진다.")]
        [SerializeField, Min(1f)] private float strongDebrisSpeedMultiplier = 1.2f;
        [Tooltip("TeamOdd/Hit Impact 셰이더 머티리얼, 빛줄기 굵기·베기 섬광 세기·타원 정도·몸에 가려지는 정도는 머티리얼에서 조정한다.")]
        [SerializeField] private Material impactMaterial;
        [Tooltip("중간 피격의 충격 크기(m), 빛줄기가 뻗는 지름, 강한 피격은 배율을 곱한다.")]
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
        [Tooltip("입자가 사라질 때 물드는 색, 충격 머티리얼의 Tip Color와 맞추면 자연스럽다.")]
        [SerializeField] private Color debrisTipColor = new(0.6f, 0.9f, 1f);

        private ICombatable self;
        private HitFlash flash;
        private CombatManager combatManager;
        private Collider[] bodyParts;
        private HitImpact[] impacts;
        private HitImpact[] strongImpacts;
        private int nextImpact;
        private int nextStrongImpact;

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

            DestroyImpacts(impacts);
            DestroyImpacts(strongImpacts);
        }

        private static void DestroyImpacts(HitImpact[] impacts)
        {
            if (impacts == null) return;
            foreach (HitImpact impact in impacts)
                if (impact != null) Destroy(impact.gameObject);
        }

        private void HandleDamageExecuted(DamageEvent damageEvent, int receivedDamage)
        {
            if (receivedDamage <= 0 || self == null || ReferenceEquals(damageEvent.Receiver, self) == false) return;

            flash.Play(flashDuration);
            if (receivedDamage >= strongHitDamage || damageEvent.Info.Reaction == HitReaction.Knockdown)
            {
                PlayImpact(SnapToBody(damageEvent.Info.HitPoint), true);
                HitStop.Request(strongHitStop);
                ShakeCamera(strongShakeTrauma);
            }
            else if (receivedDamage >= mediumHitDamage)
            {
                PlayImpact(SnapToBody(damageEvent.Info.HitPoint), false);
                HitStop.Request(mediumHitStop);
            }
        }

        private void ShakeCamera(float trauma)
        {
            if (trauma <= 0f) return;

            Player.Player player = Player.Player.Instance;
            if (player != null && player.CameraController != null) player.CameraController.AddTrauma(trauma, strongShakeFadeTime);
        }

        private void PlayImpact(Vector3 position, bool isStrong)
        {
            if (impactMaterial == null) return;

            HitImpact impact;
            if (isStrong)
            {
                strongImpacts ??= CreateImpacts(true);
                impact = strongImpacts[nextStrongImpact];
                nextStrongImpact = (nextStrongImpact + 1) % strongImpacts.Length;
            }
            else
            {
                impacts ??= CreateImpacts(false);
                impact = impacts[nextImpact];
                nextImpact = (nextImpact + 1) % impacts.Length;
            }

            float size = isStrong ? impactSize * strongImpactSizeMultiplier : impactSize;
            impact.Play(position, size, impactDuration, impactTowardCamera, impactRollRange);
        }

        private HitImpact[] CreateImpacts(bool isStrong)
        {
            int count = isStrong ? Mathf.RoundToInt(debrisCount * strongDebrisCountMultiplier) : debrisCount;
            Vector2 speed = isStrong ? debrisSpeed * strongDebrisSpeedMultiplier : debrisSpeed;
            float size = isStrong ? impactSize * strongImpactSizeMultiplier : impactSize;
            HitImpact[] result = new HitImpact[impactPoolSize];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = HitImpact.Create(impactMaterial, $"{name} Hit Impact");
                result[index].SetLight(impactLightColor, impactLightIntensity, impactLightRange);
                result[index].SetDebris(debrisMaterial, count, speed, debrisLifetime, debrisSize, size * debrisSpawnRadius, debrisTipColor);
            }
            return result;
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
