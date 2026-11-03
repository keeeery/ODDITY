using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Player
{
    // 회피 피드백. 회피를 일으킨 쪽이 회피 유형(DodgeType)을 넘기면, 여기서 유형을 보고 무엇을 보여줄지 정한다
    // - 수동 회피(Player.BlockDamage, 직접 구른 구르기로 피함): 하얀 허물 + 세계 전체 슬로모션. 직접 해낸 회피의 보상
    // - 자동 회피(조건 반사 칩, 칩이 시작한 구르기로 피함): 파란 잔상만. 슬로모션은 수동 회피에만 준다
    //   (히트스탑은 넣지 않는다. 구르기 트리거만 걸린 채 세계가 멈춰서, 구르기 전 자세로 굳어 보인다)
    // 프리팹은 SkillEffectFactory가 만든다. 지우는 건 프리팹이 한다 (DodgeShell, DodgeAfterimage)
    public static class DodgeEffect
    {
        private const string ManualPrefabKey = "Skill/Effect/ManualDodgeEffect"; // Resources 경로
        private const string AutoPrefabKey = "Skill/Effect/AutoDodgeEffect";
        private const float SlowScale = 0.25f;   // 수동 회피 슬로모션 배율 (75% 느려짐)
        private const float SlowDuration = 0.1f; // 수동 회피 슬로모션 시간 (실제 시간, 초). 허물이 하얗게 머무는 시간(SkillEffectFactory)과 맞춘다

        private static GameObject manualPrefab; // 처음 쓸 때 한 번 불러 둔다
        private static GameObject autoPrefab;

        public static void Play(Transform player, DodgeType type)
        {
            if (type == DodgeType.Manual)
                PlayManual(player);
            else
                PlayAuto(player);
        }

        // 지금 자세 그대로 하얀 허물을 남기고 세계 전체(플레이어 포함)를 잠깐 느리게 한다. 허물은 느려진 동안 머물다 바스러진다
        private static void PlayManual(Transform player)
        {
            if (manualPrefab == null)
                manualPrefab = Resources.Load<GameObject>(ManualPrefabKey);

            Object.Instantiate(manualPrefab).GetComponent<DodgeShell>().Bake(player);
            SlowMotion.Play(SlowScale, SlowDuration, 0f);
        }

        // 지금 자세 그대로 잔상을 남긴다. 몸은 구르기로 빠져나가고 잔상만 그 자리에 흐려진다
        private static void PlayAuto(Transform player)
        {
            if (autoPrefab == null)
                autoPrefab = Resources.Load<GameObject>(AutoPrefabKey);

            Object.Instantiate(autoPrefab).GetComponent<DodgeAfterimage>().Bake(player);
        }
    }
}
