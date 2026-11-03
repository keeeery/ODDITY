using TeamOdd.Boss;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Arena
{
    // 수련장 허수아비: 플레이어가 수련장 안에 있는 동안 보스 HUD가 이 허수아비의 체력·무력화 수치를 보여 주고(Focused),
    // 피해를 받지 않은 채 recoverDelay초가 지나면 체력과 무력화 수치를 가득 채운다.
    // 수련용이라 죽을 피해를 받으면 즉시 최대 체력으로 돌아가고, 무력화 수치가 0이 되어도 따로 쓰러지지 않는다(무력화 상태머신이 없음).
    [RequireComponent(typeof(CombatEntity))]
    public sealed class TrainingDummy : MonoBehaviour, IDamageGuard
    {
        [Tooltip("수련장 바닥 영역, 플레이어가 이 범위 안(위에서 본 넓이)에 있을 때만 보스 HUD에 나온다. 비어 있으면 허수아비에서 showRadius 안에 있을 때 나온다.")]
        [SerializeField] private Collider area;
        [Tooltip("area가 비어 있을 때 보스 HUD에 나오는 거리")]
        [SerializeField, Min(0f)] private float showRadius = 15f;
        [Tooltip("마지막으로 피해를 받은 뒤 체력·무력화 수치가 가득 차기까지의 시간(초)")]
        [SerializeField, Min(0f)] private float recoverDelay = 2f;

        // 보스 HUD가 연결할 허수아비, 플레이어가 어느 수련장에도 없으면 null
        public static TrainingDummy Focused { get; private set; }

        public CombatEntity CombatEntity { get; private set; }

        private BossGroggy groggy;
        private Transform player;
        private float lastHitTime = float.NegativeInfinity;

        private void Awake()
        {
            CombatEntity = GetComponent<CombatEntity>();
            groggy = GetComponent<BossGroggy>();
        }

        private void OnEnable()
        {
            CombatEntity.Guard = this;
            CombatEntity.Hit += HandleHit;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(CombatEntity.Guard, this)) CombatEntity.Guard = null;
            CombatEntity.Hit -= HandleHit;
            if (Focused == this) Focused = null;
        }

        private void Update()
        {
            UpdateFocus();

            if (Time.time - lastHitTime < recoverDelay) return;

            if (CombatEntity.Hp < CombatEntity.CurrentStats.MaxHp) CombatEntity.SetHp(CombatEntity.CurrentStats.MaxHp);
            if (groggy != null && groggy.Ratio < 1f) groggy.Restore();
        }

        #region IDamageGuard

        public bool BlockDamage(DamageEvent damageEvent) => false;

        // 죽을 피해를 받으면 죽지 않고 바로 최대 체력으로 돌아감
        public bool PreventDeath()
        {
            CombatEntity.SetHp(CombatEntity.CurrentStats.MaxHp);
            return true;
        }

        #endregion

        private void HandleHit(DamageEvent damageEvent)
        {
            lastHitTime = Time.time;
        }

        // 플레이어가 늦게 생길 수 있어 찾을 때까지 다시 시도
        private void UpdateFocus()
        {
            if (player == null)
            {
                Player.Player found = FindAnyObjectByType<Player.Player>();
                if (found != null) player = found.transform;
            }

            bool inside = player != null && IsInside(player.position);
            if (inside) Focused = this;
            else if (Focused == this) Focused = null;
        }

        private bool IsInside(Vector3 position)
        {
            if (area == null)
            {
                Vector3 offset = position - transform.position;
                offset.y = 0f;
                return offset.sqrMagnitude <= showRadius * showRadius;
            }

            // 높이는 보지 않고 바닥 넓이로만 판정
            Bounds bounds = area.bounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x
                && position.z >= bounds.min.z && position.z <= bounds.max.z;
        }
    }
}
