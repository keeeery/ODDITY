using TeamOdd.Boss;
using TeamOdd.Combat;
using TMPro;
using UnityEngine;

namespace TeamOdd.UI
{
    // 보스 체력(CombatEntity)을 VectorBar로 보여주는 HUD
    // 보스를 지정하지 않으면 씬에서 활성화된 보스를 찾아 연결하므로, 보스를 켜고 끄며 바꿔도 따라감
    // 보스가 없거나 사라지면 바를 비우고 HUD를 숨김
    [RequireComponent(typeof(CanvasGroup))]
    public class BossHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비어 있으면 씬에서 활성화된 보스(BossMonster)의 CombatEntity를 찾는다.")]
        [SerializeField] private CombatEntity boss;

        [Header("Name")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("비어 있으면 보스 오브젝트 이름을 쓴다.")]
        [SerializeField] private string bossName;

        [Header("Bars")]
        [SerializeField] private VectorBar hpBar;
        // TODO: 보스 무력화 수치가 생기면 연결
        [SerializeField] private VectorBar groggyBar;

        private bool findsBoss;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            findsBoss = boss == null;
            // RequireComponent는 이미 씬에 배치된 HUD에는 붙지 않으므로 없으면 직접 추가
            if (TryGetComponent(out canvasGroup) == false) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (findsBoss) boss = FindActiveBoss();
            if (hpBar != null) hpBar.SetFillImmediate(GetHpRatio());
            UpdateName();
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
                    if (hpBar != null) hpBar.SetFillImmediate(GetHpRatio());
                    UpdateName();
                }
            }

            // 보스를 다시 찾은 뒤에 정해야 바뀐 프레임에 한 번 깜빡이지 않음
            canvasGroup.alpha = boss == null ? 0f : 1f;
            UpdateFill(hpBar, GetHpRatio());
        }

        private void UpdateFill(VectorBar bar, float ratio)
        {
            if (bar == null) return;

            bar.FillAmount = ratio;
        }

        private void UpdateName()
        {
            if (nameText == null) return;

            nameText.text = string.IsNullOrEmpty(bossName) == false ? bossName
                : boss != null ? boss.gameObject.name
                : string.Empty;
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
