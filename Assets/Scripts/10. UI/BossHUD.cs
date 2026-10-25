using TeamOdd.Boss;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.UI
{
    // 보스 체력(CombatEntity)을 VectorBar로 보여주는 HUD
    // 보스를 지정하지 않으면 씬에서 활성화된 보스를 찾아 연결하므로, 보스를 켜고 끄며 바꿔도 따라감
    // 보스가 없거나 사라지면 바를 비움
    public class BossHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비어 있으면 씬에서 활성화된 보스(BossMonster)의 CombatEntity를 찾는다.")]
        [SerializeField] private CombatEntity boss;

        [Header("Bars")]
        [SerializeField] private VectorBar hpBar;
        // TODO: 보스 무력화 수치가 생기면 연결
        [SerializeField] private VectorBar groggyBar;

        [Header("Display")]
        // 값이 바뀔 때 Fill이 따라가는 속도, 0이면 즉시 반영
        [SerializeField, Min(0f)] private float fillLerpSpeed = 12f;

        private bool findsBoss;

        private void Awake()
        {
            findsBoss = boss == null;
        }

        private void Start()
        {
            if (findsBoss) boss = FindActiveBoss();
            if (hpBar != null) hpBar.FillAmount = GetHpRatio();
        }

        private void Update()
        {
            // 연결된 보스가 꺼지거나 사라지면 활성화된 다른 보스로 바꾸고, 바는 새 보스의 체력으로 바로 맞춤
            if (findsBoss && (boss == null || boss.isActiveAndEnabled == false))
            {
                CombatEntity found = FindActiveBoss();
                if (found != boss)
                {
                    boss = found;
                    if (hpBar != null) hpBar.FillAmount = GetHpRatio();
                }
            }

            UpdateFill(hpBar, GetHpRatio());
        }

        private void UpdateFill(VectorBar bar, float ratio)
        {
            if (bar == null) return;

            bar.FillAmount = fillLerpSpeed > 0f
                ? Mathf.Lerp(bar.FillAmount, ratio, 1f - Mathf.Exp(-fillLerpSpeed * Time.deltaTime))
                : ratio;
        }

        private float GetHpRatio()
        {
            if (boss == null || boss.CurrentStats == null) return 0f;

            int maxHp = boss.CurrentStats.MaxHp;
            return maxHp > 0 ? (float)boss.Hp / maxHp : 0f;
        }

        private static CombatEntity FindActiveBoss()
        {
            BossMonster monster = FindAnyObjectByType<BossMonster>();
            return monster != null ? monster.GetComponent<CombatEntity>() : null;
        }
    }
}
