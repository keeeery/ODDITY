using System;
using DG.Tweening;
using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 점프 착지나 양손 내려찍기 같은 충격 시점에 원형 범위로 펑 터지는 충격파를 일으키는 패턴
    // 터지는 순간 범위 안의 대상이 바닥 근처에 있으면 피격, 점프해서 떠 있으면 회피
    // 범위 안의 염동력 오브젝트는 위로 살짝 떠올랐다가 중력으로 떨어짐
    // 사거리 밖이면 근접 패턴처럼 예약 후 추격
    public sealed class BossShockwavePattern : BossStateBase, IRangedPattern
    {
        [Serializable]
        private class Wave
        {
            [Tooltip("충격파가 터질 중심, 높이는 무시하고 이 지점 아래 바닥에서 터진다.")]
            public Transform origin;
            [Min(0)] public int damage = 10;
            [Min(0f)] public float radius = 10f;
        }

        [Title("Use Distance")]
        [Tooltip("이 거리 밖이면 추격 후 사용(m), 보스 콜라이더 때문에 약 6.75m보다 커야 추격이 끝난다.")]
        [SerializeField, Min(0f)] private float useRange = 9f;

        [Title("Waves")]
        // 충격 발생: OnShockwave 신호(매개변수는 waves 인덱스, 비어 있으면 0), 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)
        [SerializeField] private Wave[] waves;

        [Title("Hit")]
        [Tooltip("대상 발밑에서 바닥까지 이만큼 이상 떠 있으면 회피(m)")]
        [SerializeField, Min(0f)] private float jumpAvoidHeight = 0.5f;
        [Tooltip("바닥 판정에 사용할 레이어")]
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

        // 임시용: 나중에 플레이어 피격 판정을 담당하는 공용 인터페이스로 빼던가
        // 보스 공격 State 최상위 클래스에 프로퍼티 이동하던가 할 듯
        [field: Tooltip("피격 대상에게 줄 반응, 실제 경직/다운 처리는 피격 대상 쪽에서 담당")]
        [field: SerializeField] public HitReaction Reaction { get; private set; } = HitReaction.Stagger;

        [Title("Lift")]
        [Tooltip("염동력 오브젝트 풀의 부모(WallFragmentObjectsRoot), 활성화된 자식 중 범위 안의 오브젝트를 띄운다.")]
        [SerializeField] private Transform fragmentRoot;
        [Tooltip("띄울 높이의 무작위 범위(m)")]
        [SerializeField, MinMaxSlider(0f, 3f, true)] private Vector2 liftHeight = new Vector2(0.3f, 1f);
        [Tooltip("떠오르는 데 걸리는 시간의 무작위 범위(초), 다 떠오르면 중력으로 떨어진다.")]
        [SerializeField, MinMaxSlider(0f, 1f, true)] private Vector2 liftDuration = new Vector2(0.15f, 0.3f);

        [Title("Visual")]
        [Tooltip("이펙트 연결 전 확인용, 켜면 터지는 범위를 Game 뷰에 LineRenderer로 표시")]
        [SerializeField] private bool showRange = true;
        [SerializeField] private Color rangeColor = new Color(1f, 0.5f, 0f, 0.5f);
        [Tooltip("범위 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float rangeFadeDuration = 0.4f;

        // 발밑 레이캐스트가 대상 콜라이더 안쪽에서 시작하도록 띄우는 높이
        private const float GroundProbeOffset = 0.1f;
        // 충격 지점 아래 바닥을 찾을 때 시작 높이와 탐색 거리(m)
        private const float GroundSearchHeight = 2f;
        private const float GroundSearchDistance = 10f;
        // 범위 표시가 바닥에 묻히지 않도록 띄우는 높이와 원의 분할 수
        private const float RangeLineHeight = 0.05f;
        private const int RangeSegments = 64;

        private Collider targetCollider;
        private Material rangeMaterial;

        public float RequiredRange => useRange;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);
            rangeMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        protected override void OnEnter()
        {
            if (root.IsTargetInRange(useRange) == false)
            {
                StateMachine.ReserveState(this);
                StateMachine.ChangeState(typeof(BossChaseState), true);
                return;
            }

            targetCollider = Target.GetComponent<Collider>();
            root.AnimEventListener.OnOccursAnimStateEvent += OnAnimEvent;
            Animator.SetTrigger(BossAnimParams.SHOCKWAVE);
        }

        protected override void OnExit()
        {
            root.AnimEventListener.OnOccursAnimStateEvent -= OnAnimEvent;
            Animator.ResetTrigger(BossAnimParams.SHOCKWAVE);
        }

        private void OnAnimEvent(string eventName, string eventParam)
        {
            if (eventName != "OnShockwave") return;

            int index = string.IsNullOrEmpty(eventParam) ? 0 : int.Parse(eventParam);
            Explode(waves[index]);
        }

        private void Explode(Wave wave)
        {
            Vector3 center = wave.origin.position;
            center.y = GetGroundHeight(center);

            if (IsInRange(Target.position, center, wave.radius) && IsTargetGrounded()) Hit(wave);

            LiftFragments(center, wave.radius);

            if (showRange) ShowRange(center, wave.radius);
        }

        private static bool IsInRange(Vector3 position, Vector3 center, float radius)
        {
            Vector3 offset = position - center;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        // 대상 피벗이 발이 아닐 수 있어(CharacterController 중앙 등) 콜라이더 바닥에서 아래로 바닥을 찾음
        private bool IsTargetGrounded()
        {
            Bounds bounds = targetCollider.bounds;
            Vector3 feet = new Vector3(bounds.center.x, bounds.min.y + GroundProbeOffset, bounds.center.z);

            return Physics.Raycast(feet, Vector3.down, jumpAvoidHeight + GroundProbeOffset, groundLayers, QueryTriggerInteraction.Ignore);
        }

        // 보스 몸에 걸리지 않도록 보스 콜라이더는 제외하고 가장 가까운 바닥을 찾음, 없으면 보스 높이 사용
        private float GetGroundHeight(Vector3 position)
        {
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * GroundSearchHeight, Vector3.down,
                GroundSearchDistance, groundLayers, QueryTriggerInteraction.Ignore);
            float height = root.transform.position.y;
            float nearest = float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(root.transform) || hit.distance >= nearest) continue;

                nearest = hit.distance;
                height = hit.point.y;
            }

            return height;
        }

        private void Hit(Wave wave)
        {
            Debug.Log($"[Shockwave] 피격, 피해 {wave.damage}, 반응 {Reaction}", this);

            // 피해를 받을 수 있는 대상일 때만 전달, 보스는 ICombatable이 아니라 보낸 쪽은 비워 둠
            ICombatable receiver = Target.GetComponentInParent<ICombatable>();
            if (receiver == null) return;

            DamageInfo info = new DamageInfo(wave.damage, Target.position, receiver.Collider);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(null, receiver, info));
        }

        // 떠오르는 동안 물리와 부딪히지 않도록 kinematic으로 띄우고, 다 떠오르면 되돌려 중력으로 떨어뜨림
        // 풀 오브젝트는 크기별 그룹(Large/Medium/Small) 아래에 있어 하위 전체에서 활성화된 오브젝트를 찾음
        // 플레이어가 들고 있거나 보스가 띄워 둔(kinematic) 오브젝트는 건드리지 않음
        private void LiftFragments(Vector3 center, float radius)
        {
            foreach (Rigidbody body in fragmentRoot.GetComponentsInChildren<Rigidbody>())
            {
                if (body.isKinematic || IsInRange(body.position, center, radius) == false) continue;
                if (body.TryGetComponent(out PsychokinesisObject psychokinesisObject) && psychokinesisObject.IsHeld) continue;

                body.isKinematic = true;
                body.DOMoveY(body.position.y + Random.Range(liftHeight.x, liftHeight.y), Random.Range(liftDuration.x, liftDuration.y))
                    .SetEase(Ease.OutQuad)
                    .SetLink(body.gameObject)
                    .OnKill(() => body.isKinematic = false);
            }
        }

        #region RANGE_VISUALIZATION

        // 바닥에 눕힌 원으로 그리기 위해 Z축이 아래를 향하도록 돌려 TransformZ 정렬 사용
        // 선 두께를 반경과 같게 해 중심까지 채운 원으로 표시하고, 패턴이 끝나도 끝까지 사라지도록 트윈으로 처리
        private void ShowRange(Vector3 center, float radius)
        {
            LineRenderer line = new GameObject("ShockwaveRange").AddComponent<LineRenderer>();
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.alignment = LineAlignment.TransformZ;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = RangeSegments;
            line.material = rangeMaterial;
            line.widthMultiplier = radius;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            Vector3 lineCenter = center + Vector3.up * RangeLineHeight;

            for (int index = 0; index < RangeSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / RangeSegments;
                line.SetPosition(index, lineCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.5f);
            }

            Color clear = rangeColor;
            clear.a = 0f;

            line.DOColor(new Color2(rangeColor, rangeColor), new Color2(clear, clear), rangeFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(line.gameObject)
                .OnComplete(() => Destroy(line.gameObject));
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false) return;

            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);

            foreach (Wave wave in waves)
                UnityEditor.Handles.DrawWireDisc(wave.origin.position, Vector3.up, wave.radius);
        }
#endif
    }
}
