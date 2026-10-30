using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd
{
    // 분신 소환 (A16): 맵에 분신이 없고, 마지막 분신이 사라진 뒤 respawnCooldown이 지났으면 검을 세워 들며 분신을 부름
    // 페이즈 2 패턴 목록(HumanoidIdleState.phase2Melees)과 페이즈 전환의 연계(followPatterns)에 넣어 씀
    // 분신은 씬에 꺼 둔 원본(template)을 복제, 소환 순간 끊기지 않게 다음 분신을 미리 복제해 꺼 둠
    // 분신은 본체의 자식이 아니어야 함 (본체가 GetComponentsInChildren으로 상태·페이즈 알림을 훑으므로)
    public class HumanoidCloneSummon : BossMeleePattern
    {
        [Title("Clone")]
        [SerializeField] private HumanoidClone template;
        [SerializeField] private float respawnCooldown = 30f; // 분신이 사라진 뒤부터 셈
        [SerializeField, Range(0.01f, 1f)] private float hpRatio = 0.15f; // 분신 최대 체력 = 본체 최대 체력 × 이 값, 원본의 maxHpOverride는 무시됨

        // 본체 좌우 중 무작위 한쪽에 세움, 막히면 거리를 줄이고 그래도 안 되면 반대쪽
        [Title("Spawn")]
        [SerializeField] private float sideDistance = 4f;
        [SerializeField] private float minSpawnDistance = 3f;
        [SerializeField] private float bodyRadius = 1.5f;
        [SerializeField] private LayerMask blockMask;
        [SerializeField] private LayerMask groundMask;

        [Title("Runtime State")]
        [ShowInInspector, ReadOnly] private HumanoidClone activeClone;
        [ShowInInspector, ReadOnly] private float vanishedTime = float.NegativeInfinity;

        private HumanoidClone standby;
        private CombatEntity combatEntity;

        public override bool CanUse => base.CanUse && IsReady;

        protected override bool UsesMeleeHits => false;

        private bool IsReady => template != null
                                && activeClone == null
                                && combatEntity.IsDead == false
                                && Clock.LocalTime - vanishedTime >= respawnCooldown;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            combatEntity = root.GetComponent<CombatEntity>();
            combatEntity.Died += HandleBossDied;
            PrepareStandby();
        }

        private void OnDestroy()
        {
            if (combatEntity != null) combatEntity.Died -= HandleBossDied;
        }

        // instant: 세상을 멈추는 연출(A15)처럼 사라지는 연출을 기다릴 수 없을 때
        public void DismissClone(bool instant)
        {
            if (activeClone != null) activeClone.Vanish(instant);
        }

        // 애니메이션 트리거(cloneSummon)는 BossMeleePattern이 켬, 검이 정점일 때 OnCloneSummon
        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (string.Equals(name, "OnCloneSummon")) Summon();
        }

        private void Summon()
        {
            if (IsReady == false) return;

            PrepareStandby();
            activeClone = standby;
            standby = null;

            Pose pose = GetSpawnPose();
            activeClone.transform.SetPositionAndRotation(pose.position, pose.rotation);
            activeClone.Vanished += HandleCloneVanished;
            activeClone.gameObject.SetActive(true);
            ApplyCloneHp(activeClone);

            Debug.Log("[HumanoidCloneSummon] 분신 소환", activeClone);
        }

        // 켜서 CombatEntity.Awake가 체력을 채운 뒤 최대 체력 보너스로 맞춤 (늘면 늘어난 만큼, 줄면 최대치로 현재 체력도 맞춰짐)
        private void ApplyCloneHp(HumanoidClone clone)
        {
            CombatEntity cloneEntity = clone.GetComponent<CombatEntity>();
            int maxHp = Mathf.Max(1, Mathf.RoundToInt(combatEntity.CurrentStats.MaxHp * hpRatio));
            cloneEntity.CurrentStats.Add(StatType.MaxHp, maxHp - cloneEntity.CurrentStats.MaxHp);
        }

        private void PrepareStandby()
        {
            if (standby != null || template == null) return;

            standby = Instantiate(template, template.transform.parent);
            standby.name = template.name;
            standby.gameObject.SetActive(false);
        }

        private void HandleCloneVanished(HumanoidClone clone)
        {
            clone.Vanished -= HandleCloneVanished;
            if (clone != activeClone) return;

            activeClone = null;
            vanishedTime = Clock.LocalTime;
            if (combatEntity.IsDead == false) PrepareStandby();
        }

        private void HandleBossDied()
        {
            DismissClone(false);
            if (standby != null) Destroy(standby.gameObject);
            standby = null;
        }

        private Pose GetSpawnPose()
        {
            Vector3 origin = root.transform.position;
            Vector3 side = Movement.Rotation * Vector3.right;
            if (Random.value < 0.5f) side = -side;

            if (TryCast(origin, side, sideDistance, out Vector3 position) == false && TryCast(origin, -side, sideDistance, out position) == false)
                position = GroundAt(origin + side * minSpawnDistance, origin.y);

            Vector3 look = Target.position - position;
            look.y = 0f;
            return new Pose(position, look == Vector3.zero ? root.transform.rotation : Quaternion.LookRotation(look));
        }

        // 벽에 막히면 막힌 거리까지 줄임, minSpawnDistance보다 짧아지면 실패
        private bool TryCast(Vector3 origin, Vector3 direction, float distance, out Vector3 position)
        {
            if (Physics.SphereCast(origin + Vector3.up, bodyRadius, direction, out RaycastHit block, distance, blockMask, QueryTriggerInteraction.Ignore))
                distance = block.distance;

            position = GroundAt(origin + direction * distance, origin.y);
            return distance >= minSpawnDistance;
        }

        private Vector3 GroundAt(Vector3 position, float fallbackY)
        {
            position.y = Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 20f, groundMask, QueryTriggerInteraction.Ignore)
                ? ground.point.y
                : fallbackY;
            return position;
        }
    }
}
