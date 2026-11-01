using TeamOdd.Boss;
using TeamOdd.Combat;
using TMPro;
using UnityEngine;

namespace TeamOdd.UI
{
    // 보스 체력(CombatEntity)과 무력화 수치(BossGroggy)를 VectorBar로 보여주는 HUD
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
        [Tooltip("보스 무력화 수치, 보스에 BossGroggy가 없으면 비워 둔다.")]
        [SerializeField] private VectorBar groggyBar;
        [Tooltip("켜면 무력화 바의 잔상(줄 때)과 차오름(찰 때)을 바 설정과 상관없이 켠다. 끄면 바(VectorBar)의 설정을 따른다.")]
        [SerializeField] private bool smoothGroggyBar = true;
        [Tooltip("체력·무력화 바가 줄 때 남는 잔상 색, 바(VectorBar)의 설정보다 우선한다. 기본값은 보스 이름 텍스트와 같은 회백색")]
        [SerializeField] private Color trailColor = new(0.847f, 0.824f, 0.769f, 1f);

        [Header("Clone")]
        [Tooltip("휴머노이드 보스 분신(HumanoidClone) 체력, 분신이 맵에 있을 때만 보인다. 분신이 없는 보스면 비워 둔다.")]
        [SerializeField] private VectorBar cloneHpBar;
        [Tooltip("분신 체력바가 나타나고 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float cloneBarFadeDuration = 0.3f;

        [Header("Occlusion Outline")]
        [Tooltip("보스에 가려진 플레이어 외곽선 두께(픽셀), 색은 보스 이름 텍스트 색을 따른다.")]
        [SerializeField, Range(1f, 8f)] private float occlusionOutlineWidth = 2f;

        private bool findsBoss;
        private Player.Player player;
        private BossGroggy groggy;
        private HumanoidClone clone;

        private CanvasGroup canvasGroup;
        private CanvasGroup cloneBarGroup;

        private void Awake()
        {
            findsBoss = boss == null;
            // RequireComponent는 이미 씬에 배치된 HUD에는 붙지 않으므로 없으면 직접 추가
            if (TryGetComponent(out canvasGroup) == false) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 무력화는 계속 깎였다 차는 값이라 씬마다 바를 따로 맞추지 않도록 여기서 켬
            if (smoothGroggyBar && groggyBar != null)
            {
                groggyBar.UseTrail = true;
                groggyBar.UseRise = true;
            }

            // 잔상 색도 씬마다 맞추지 않도록 두 바에 같이 적용
            if (hpBar != null) hpBar.TrailColor = trailColor;
            if (groggyBar != null) groggyBar.TrailColor = trailColor;

            // 분신 바는 따로 페이드하므로 바 오브젝트에 CanvasGroup을 두고 처음엔 숨김
            if (cloneHpBar != null)
            {
                cloneHpBar.TrailColor = trailColor;
                if (cloneHpBar.TryGetComponent(out cloneBarGroup) == false) cloneBarGroup = cloneHpBar.gameObject.AddComponent<CanvasGroup>();
                cloneBarGroup.alpha = 0f;
            }
        }

        private void OnEnable()
        {
            UpdateOcclusionTargets();
        }

        private void OnDisable()
        {
            BossOcclusionOutlineFeature.ClearTargets();
        }

        private void Start()
        {
            if (findsBoss) boss = FindActiveBoss();
            BindBoss();
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
                    BindBoss();
                }
            }

            // 보스를 다시 찾은 뒤에 정해야 바뀐 프레임에 한 번 깜빡이지 않음
            canvasGroup.alpha = boss == null ? 0f : 1f;
            UpdateFill(hpBar, GetHpRatio());
            UpdateFill(groggyBar, GetGroggyRatio());
            UpdateCloneBar();

            // 플레이어가 늦게 생길 수 있어 찾을 때까지 다시 시도
            if (player == null)
            {
                player = FindAnyObjectByType<Player.Player>();
                if (player != null) UpdateOcclusionTargets();
            }
            // 외곽선 색은 이름 텍스트 색을 매 프레임 따라감
            Color outlineColor = nameText != null ? nameText.color : trailColor;
            outlineColor.a = 1f;
            BossOcclusionOutlineFeature.SetStyle(outlineColor, occlusionOutlineWidth);
        }

        // 새 보스의 체력·무력화 수치로 바를 바로 맞춤
        private void BindBoss()
        {
            groggy = boss != null ? boss.GetComponent<BossGroggy>() : null;
            if (hpBar != null) hpBar.SetFillImmediate(GetHpRatio());
            if (groggyBar != null) groggyBar.SetFillImmediate(GetGroggyRatio());
            UpdateName();
            UpdateOcclusionTargets();
        }

        private void UpdateOcclusionTargets()
        {
            if (boss == null || player == null)
            {
                BossOcclusionOutlineFeature.ClearTargets();
                return;
            }

            BossOcclusionOutlineFeature.SetTargets(boss.gameObject, player.gameObject);
        }

        private void UpdateFill(VectorBar bar, float ratio)
        {
            if (bar == null) return;

            bar.FillAmount = ratio;
        }

        // 분신이 바뀌면 새 분신 체력으로 바로 맞추고, 나와 있는 동안 보이다가 사라지기 시작하면 감춤
        // 죽어서 사라질 때는 비워진 바와 잔상이 보이도록 사라진 분신의 마지막 체력을 그대로 둠
        private void UpdateCloneBar()
        {
            if (cloneHpBar == null) return;

            HumanoidClone active = HumanoidClone.Active;
            if (active != null && active != clone)
            {
                clone = active;
                cloneHpBar.SetFillImmediate(GetCloneHpRatio());
            }

            bool visible = active != null && active.IsVanishing == false;
            float step = Time.unscaledDeltaTime / cloneBarFadeDuration;
            cloneBarGroup.alpha = Mathf.MoveTowards(cloneBarGroup.alpha, visible ? 1f : 0f, step);

            if (clone != null) UpdateFill(cloneHpBar, GetCloneHpRatio());
        }

        private float GetCloneHpRatio()
        {
            CombatEntity entity = clone != null ? clone.CombatEntity : null;
            if (entity == null || entity.CurrentStats == null) return 0f;

            int maxHp = entity.CurrentStats.MaxHp;
            return maxHp > 0 ? (float)entity.Hp / maxHp : 0f;
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

        private float GetGroggyRatio() => groggy != null ? groggy.Ratio : 0f;

        private static CombatEntity FindActiveBoss()
        {
            BossMonster monster = FindAnyObjectByType<BossMonster>();
            return monster != null ? monster.GetComponent<CombatEntity>() : null;
        }
    }
}
