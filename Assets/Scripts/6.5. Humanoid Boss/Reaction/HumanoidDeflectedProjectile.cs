using Sirenix.OdinInspector;
using TeamOdd.Arena;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using TeamOdd.TimeControl;
using UnityEngine;

namespace TeamOdd
{
    // PsychokinesisObject가 없어 염동력 조준·잡기 대상에 들어가지 않음 → 되돌려 보낸 물체는 다시 잡을 수 없음
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HumanoidDeflectedProjectile : MonoBehaviour, IObjectPoolItem
    {
        [Title("Hit")]
        [SerializeField, Min(0)] private int damage = 20;
        [SerializeField] private HitReaction reaction = HitReaction.Knockback;
        [SerializeField] private HitPush push = HitPush.Create(3f, 0f, 0.5f, HitPush.DirectionMode.FromHitCenter);
        [SerializeField, Min(0f)] private float hitPadding = 0.3f; // 대상 몸에 부딪혀 튕겨도 겹침이 잡히도록 박스 바깥에 더하는 여유

        [Title("Flight")]
        [SerializeField, Min(0f)] private float minSpeed = 5f; // 이보다 느려지면 떨어진 것으로 보고 부숨
        [SerializeField, Min(0f)] private float maxFlightTime = 3f;
        [SerializeField, Min(0f)] private float spinSpeed = 3f;

        private Rigidbody body;
        private BoxCollider box;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private TimeFreezable clock;
        private WallFragmentSpawner debrisSpawner;
        private Material debrisMaterial; // 원래 물체가 보스에 맞았을 때와 같은 조각으로 부서지도록 복사

        private Transform target;
        private Collider[] ownerColliders;
        private float flightTime;
        private bool isFlying;
        private readonly Collider[] overlaps = new Collider[16];

        public string Key { get; set; }
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            clock = TimeFreezable.GetOrAdd(gameObject);
        }

        private void OnDisable()
        {
            isFlying = false;
            SetOwnerCollisionsIgnored(false);
        }

        // 풀에서 꺼낸 직후 호출, 원래 물체와 같은 모양·질량으로 바꿔 그 자리에 멈춰 둠 (가드에 막힌 순간)
        public void Catch(PsychokinesisObject source, Collider[] owners, Transform aimTarget)
        {
            CopyShape(source);
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            gameObject.layer = source.gameObject.layer;

            target = aimTarget;
            ownerColliders = owners;
            flightTime = 0f;
            isFlying = false;

            gameObject.SetActive(true);
            SetOwnerCollisionsIgnored(true);
            body.isKinematic = true;
        }

        // 반격 모션까지 보스 앞에 붙잡아 둠
        public void Hold(Vector3 position, float maxDistanceDelta)
        {
            body.MovePosition(Vector3.MoveTowards(body.position, position, maxDistanceDelta));
        }

        public void Fire(Vector3 velocity)
        {
            body.isKinematic = false;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.onUnitSphere * spinSpeed;
            isFlying = true;
        }

        // 발사 전에 반격이 끊기면 붙잡아 둔 채로 남지 않도록 부숨
        public void Shatter() => Break(body.worldCenterOfMass);

        public void ReturnToPool()
        {
            if (ObjectPoolManager.Instance == null || string.IsNullOrEmpty(Key))
            {
                Destroy(gameObject);
                return;
            }

            ObjectPoolManager.Instance.ReturnToPool(this);
        }

        // 대상에 부딪히면 속도가 줄어드므로 물리 스텝 전에 겹침을 먼저 봄
        private void FixedUpdate()
        {
            if (isFlying == false || clock.IsFrozen) return;

            flightTime += Time.fixedDeltaTime;

            if (TryFindTargetOverlap(out Collider targetCollider))
            {
                Hit(targetCollider);
                return;
            }

            if (flightTime >= maxFlightTime || body.linearVelocity.magnitude < minSpeed)
                Break(body.worldCenterOfMass - body.linearVelocity.normalized);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isFlying == false) return;

            if (target != null && collision.collider.transform.IsChildOf(target))
            {
                Hit(collision.collider);
                return;
            }

            Break(collision.GetContact(0).point);
        }

        private void CopyShape(PsychokinesisObject source)
        {
            // 맵 배치 물체는 메시가 자식에 있을 수 있음
            meshFilter.sharedMesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            meshRenderer.sharedMaterials = source.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            debrisMaterial = source.DebrisMaterial;

            if (source.TryGetComponent(out BoxCollider sourceBox))
            {
                box.center = sourceBox.center;
                box.size = sourceBox.size;
            }

            // 풀 부모는 스케일이 1이라 원래 물체의 월드 크기를 그대로 씀
            transform.localScale = source.transform.lossyScale;
            body.mass = source.Body.mass;
            body.ResetCenterOfMass();
            body.ResetInertiaTensor();
        }

        // 보스 투척과 같은 판정, 무적(구르기 등)이면 피해 없이 부서지기만 함
        private void Hit(Collider targetCollider)
        {
            Vector3 center = body.worldCenterOfMass;
            Vector3 hitPoint = targetCollider.ClosestPoint(center);

            Player.PlayerController player = Player.Player.Instance.PlayerController;
            if (player.IsInvincible == false)
            {
                player.ApplyHitReaction(reaction, push.GetDirection(transform, center, target.position), push, center);
                SendDamage(hitPoint);
            }

            Break(hitPoint);
        }

        private void SendDamage(Vector3 hitPoint)
        {
            ICombatable receiver = target.GetComponentInParent<ICombatable>();
            if (receiver == null) return;

            DamageInfo info = new DamageInfo(damage, hitPoint, receiver.Collider, reaction);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(null, receiver, info));
        }

        // 바닥에 남지 않도록 원래 물체와 같은 파편 연출만 남기고 풀로 돌아감
        private void Break(Vector3 point)
        {
            isFlying = false;

            if (debrisSpawner == null) debrisSpawner = FindAnyObjectByType<WallFragmentSpawner>();
            if (debrisSpawner != null)
            {
                Vector3 center = body.worldCenterOfMass;
                Vector3 normal = center - point;
                // 원본 Collider를 먼저 꺼야 생성된 파편이 원본과 겹치지 않음 (PsychokinesisObject.Shatter와 같은 순서)
                gameObject.SetActive(false);
                debrisSpawner.SpawnImpactDebris(center, normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up, body.mass, debrisMaterial);
            }

            ReturnToPool();
        }

        private bool TryFindTargetOverlap(out Collider targetCollider)
        {
            targetCollider = null;
            if (target == null) return false;

            Vector3 scale = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)))
                                  + Vector3.one * hitPadding;
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(box.center), halfExtents, overlaps,
                transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int index = 0; index < count; index++)
            {
                if (overlaps[index].transform.IsChildOf(target) == false) continue;

                targetCollider = overlaps[index];
                return true;
            }

            return false;
        }

        private void SetOwnerCollisionsIgnored(bool ignored)
        {
            if (ownerColliders == null) return;

            foreach (Collider owner in ownerColliders)
                if (owner != null) Physics.IgnoreCollision(box, owner, ignored);

            if (ignored == false) ownerColliders = null;
        }
    }
}
