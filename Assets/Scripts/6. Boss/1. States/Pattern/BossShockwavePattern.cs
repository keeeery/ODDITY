using System;
using DG.Tweening;
using FantasyGarden.FSM;
using Sirenix.OdinInspector;
using TeamOdd.Arena;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Boss
{
    // 점프 착지나 양손 내려찍기 같은 충격 시점에 원형 범위로 펑 터지는 충격파를 일으키는 패턴
    // 터지는 순간 범위 안의 대상은 피격
    // 범위 안의 염동력 오브젝트는 위로 살짝 떠올랐다가 중력으로 떨어짐
    // 사거리 밖이면 근접 패턴처럼 예약 후 추격
    // OnShockwaveWarn 신호에서 터질 위치를 고정하고 바닥에 범위를 예고, OnShockwave 신호에서 고정한 위치로 판정
    public sealed class BossShockwavePattern : BossMeleePattern
    {
        [Serializable]
        private class Wave
        {
            [Tooltip("충격파가 터질 중심, 높이는 무시하고 이 지점 아래 바닥에서 터진다.")]
            public Transform origin;
            [Min(0)] public int damage = 10;
            [Tooltip("피격 대상에게 줄 반응, 실제 경직/다운 처리는 피격 대상 쪽에서 담당")]
            public HitReaction reaction = HitReaction.Stagger;
            [Min(0f)] public float radius = 10f;
        }

        [Title("Waves")]
        // 범위 예고: OnShockwaveWarn 신호, 충격 발생: OnShockwave 신호(둘 다 매개변수는 waves 인덱스, 비어 있으면 0)
        // 종료: OnAttackEnded 신호(BossMonster가 Idle로 전환)
        // 예고 신호가 없으면 터지는 순간의 위치로 판정하고 범위는 터질 때만 잠깐 표시
        [SerializeField] private Wave[] waves;

        [Title("Ground")]
        [Tooltip("충격 지점 아래 바닥을 찾을 때 사용할 레이어")]
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

        [Title("Lift")]
        [Tooltip("파편 풀을 가진 스포너, 풀에서 활성화된 파편 중 범위 안의 오브젝트를 띄운다. 비어 있으면 씬에서 찾는다.")]
        [SerializeField] private WallFragmentSpawner fragmentSpawner;
        [Tooltip("띄울 높이의 무작위 범위(m)")]
        [SerializeField, MinMaxSlider(0f, 3f, true)] private Vector2 liftHeight = new Vector2(0.3f, 1f);
        [Tooltip("떠오르는 데 걸리는 시간의 무작위 범위(초), 다 떠오르면 중력으로 떨어진다.")]
        [SerializeField, MinMaxSlider(0f, 1f, true)] private Vector2 liftDuration = new Vector2(0.15f, 0.3f);

        [Title("Telegraph")]
        [Tooltip("예고 표시의 안쪽이 다 차오르는 데 걸리는 시간(초), OnShockwaveWarn에서 OnShockwave까지의 시간에 맞춘다.")]
        [SerializeField, Min(0f)] private float warnDuration = 0.8f;
        [Tooltip("터진 뒤 범위 표시가 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float rangeFadeDuration = 0.4f;

        // 충격 지점 아래 바닥을 찾을 때 시작 높이와 탐색 거리(m)
        private const float GroundSearchHeight = 2f;
        private const float GroundSearchDistance = 10f;

        // 예고 신호에서 고정한 터질 위치와 표시, waves와 같은 인덱스
        private Vector3[] warnedCenters;
        private bool[] isWarned;
        private AttackTelegraph[] telegraphs;

        public override void Initialize(MonoBehaviourFSM stateMachine)
        {
            base.Initialize(stateMachine);

            // 보스 프리팹은 씬 오브젝트를 참조할 수 없으므로 비어 있으면 씬에서 찾음
            if (fragmentSpawner == null) fragmentSpawner = FindAnyObjectByType<WallFragmentSpawner>();
            warnedCenters = new Vector3[waves.Length];
            isWarned = new bool[waves.Length];
            telegraphs = new AttackTelegraph[waves.Length];
        }

        protected override bool UsesMeleeHits => false;

        protected override void OnPatternExit()
        {
            // 터지기 전에 패턴이 끊기면 예고만 남지 않도록 바로 지움, 이미 터진 표시는 끝까지 사라지게 둠
            for (int index = 0; index < waves.Length; index++)
            {
                if (telegraphs[index] != null) telegraphs[index].Cancel();
                telegraphs[index] = null;
                isWarned[index] = false;
            }
        }

        protected override void OnPatternAnimEventOccurs(string name, string param)
        {
            if (name == "OnShockwaveWarn") Warn(ParseIndex(param));
            else if (name == "OnShockwave") Explode(ParseIndex(param));
        }

        // 피격 판정과 예고 표시가 같은 모양을 쓰도록 한곳에서 만듦
        private static TelegraphShape GetShape(Wave wave) => TelegraphShape.Circle(wave.radius);

        // 원형이라 방향은 의미가 없으므로 회전은 기본값 사용
        private Pose GetGroundPose(Wave wave)
        {
            Vector3 center = wave.origin.position;
            center.y = GetGroundHeight(center);
            return new Pose(center, Quaternion.identity);
        }

        // origin이 애니메이션으로 움직이는 본이어도 예고한 위치에서 터지도록 위치를 고정
        private void Warn(int index)
        {
            Pose pose = GetGroundPose(waves[index]);
            warnedCenters[index] = pose.position;
            isWarned[index] = true;

            if (telegraphs[index] != null) telegraphs[index].Cancel();
            telegraphs[index] = root.ShowTelegraph(GetShape(waves[index]), pose, warnDuration);
        }

        private void Explode(int index)
        {
            Wave wave = waves[index];
            Pose pose = isWarned[index] ? new Pose(warnedCenters[index], Quaternion.identity) : GetGroundPose(wave);
            TelegraphShape shape = GetShape(wave);
            isWarned[index] = false;

            TryHit(shape, pose, wave.damage, wave.reaction);

            LiftFragments(pose, shape);

            // 예고 없이 터졌으면 터지는 순간에만 범위를 보여 줌
            AttackTelegraph telegraph = telegraphs[index] != null ? telegraphs[index] : root.ShowTelegraph(shape, pose, 0f);
            telegraphs[index] = null;
            if (telegraph != null) telegraph.Resolve(rangeFadeDuration);
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

        // 떠오르는 동안 물리와 부딪히지 않도록 kinematic으로 띄우고, 다 떠오르면 되돌려 중력으로 떨어뜨림
        // 크기별 풀(Large/Medium/Small)의 부모 아래에서 활성화된 오브젝트를 찾음
        // 플레이어가 들고 있거나 보스가 띄워 둔(kinematic) 오브젝트는 건드리지 않음
        private void LiftFragments(Pose pose, TelegraphShape shape)
        {
            if (fragmentSpawner == null) return;

            foreach (Transform poolParent in fragmentSpawner.FragmentPoolParents)
            {
                if (poolParent == null) continue;
                foreach (Rigidbody body in poolParent.GetComponentsInChildren<Rigidbody>())
                    Lift(body, pose, shape);
            }
        }

        private void Lift(Rigidbody body, Pose pose, TelegraphShape shape)
        {
            if (body.isKinematic || shape.Contains(pose, body.position) == false) return;
            if (body.TryGetComponent(out PsychokinesisObject psychokinesisObject) && psychokinesisObject.IsHeld) return;

            body.isKinematic = true;
            body.DOMoveY(body.position.y + Random.Range(liftHeight.x, liftHeight.y), Random.Range(liftDuration.x, liftDuration.y))
                .SetEase(Ease.OutQuad)
                .SetLink(body.gameObject)
                .OnKill(() => body.isKinematic = false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false) return;

            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);

            for (int index = 0; index < waves.Length; index++)
            {
                // 예고한 뒤에는 실제로 터질 고정 위치를, 그 전에는 origin의 현재 위치를 그림
                Vector3 center = isWarned != null && isWarned[index] ? warnedCenters[index] : waves[index].origin.position;
                GetShape(waves[index]).DrawHandles(new Pose(center, Quaternion.identity));
            }
        }
#endif
    }
}
