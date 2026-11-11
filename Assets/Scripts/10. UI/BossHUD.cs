using System.Collections.Generic;
using TeamOdd.Arena;
using TeamOdd.Boss;
using TeamOdd.Combat;
using TeamOdd.Psychokinesis;
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

        [Header("Occlusion Silhouette")]
        [Tooltip("플레이어가 아닌 다른 물체에 가려진 보스 부분을 채우는 색, 알파로 진하기를 정한다.")]
        [SerializeField] private Color occlusionFillColor = new(0.85f, 0.1f, 0.08f, 0.5f);
        [Tooltip("보스 앞면과 가린 물체 사이가 이 거리(m)보다 멀어야 채우기 시작한다. 땅에 묻힌 발 같은 맞닿은 부분이 채워지면 올린다.")]
        [SerializeField, Min(0f)] private float occlusionFillMinGap = 0.3f;
        [Tooltip("화면에 보이는 보스 실루엣 중 이 비율(0~1) 이상이 가려져야 채우기를 보여 준다.")]
        [SerializeField, Range(0f, 1f)] private float occlusionFillMinRatio = 0.2f;
        [Tooltip("채우기가 나타나고 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float occlusionFillFadeDuration = 0.2f;
        [Tooltip("염동력 오브젝트·벽 파편 중 가장 긴 변이 이 길이(m)보다 짧으면 작은 파편으로 보고, 보스를 가려도 채우지 않는다.")]
        [SerializeField, Min(0f)] private float occlusionDebrisMaxSize = 1.5f;

        private bool findsBoss;
        private Player.Player player;
        private BossGroggy groggy;
        private TrainingDummy dummy;
        private HumanoidClone clone;

        private CanvasGroup canvasGroup;
        private CanvasGroup cloneBarGroup;
        private LayerMask wallLayers;
        private LayerMask groundLayers;
        private bool showsFill;
        private float fillOpacity;
        private readonly List<Renderer> smallOccluders = new();
        private readonly List<Renderer> rendererBuffer = new();

        private void Awake()
        {
            findsBoss = boss == null;
            wallLayers = LayerMask.GetMask("Wall");
            groundLayers = LayerMask.GetMask("Ground");
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
            // 연결된 보스가 꺼지거나 사라지면(수련장 허수아비는 플레이어가 수련장을 나가면) 활성화된 다른 보스로 바꾸고, 바는 새 보스의 체력으로 바로 맞춤
            if (findsBoss && (boss == null || boss.isActiveAndEnabled == false || IsLeftDummy()))
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
            UpdateOcclusionFill();
        }

        // 쓰러진 보스는 가려져도 실루엣을 채우지 않음
        // 바닥(Ground 레이어)과 작은 파편에 가린 부분은 채우지 않고, 수련장 허수아비는 벽(Wall 레이어) 뒤에 가린 부분도 채우지 않음
        // 채울 부분이 보스 실루엣의 일정 비율을 넘을 때만 서서히 보여 줌
        private void UpdateOcclusionFill()
        {
            bool alive = boss != null && boss.IsDead == false;
            LayerMask ignored = groundLayers | (dummy != null ? wallLayers : (LayerMask)0);
            BossOcclusionOutlineFeature.SetFillStyle(alive ? occlusionFillColor : Color.clear, occlusionFillMinGap, ignored);

            CollectSmallOccluders();
            BossOcclusionOutlineFeature.SetSmallOccluders(smallOccluders);

            // 경계값 근처에서 깜빡이지 않도록 사라질 때는 조금 더 낮은 비율까지 버팀
            float ratio = BossOcclusionOutlineFeature.HiddenRatio;
            if (alive == false) showsFill = false;
            else if (ratio >= occlusionFillMinRatio) showsFill = true;
            else if (ratio < occlusionFillMinRatio * 0.7f) showsFill = false;

            float step = Time.unscaledDeltaTime / occlusionFillFadeDuration;
            fillOpacity = Mathf.MoveTowards(fillOpacity, showsFill ? 1f : 0f, step);
            BossOcclusionOutlineFeature.SetFillOpacity(fillOpacity);
        }

        // 염동력 오브젝트(벽 파편 포함) 중 전체 크기가 작은 것의 렌더러를 모음
        private void CollectSmallOccluders()
        {
            smallOccluders.Clear();
            if (boss == null) return;

            IReadOnlyList<PsychokinesisObject> objects = PsychokinesisObject.ActiveObjects;
            for (int index = 0; index < objects.Count; index++)
            {
                PsychokinesisObject target = objects[index];
                if (target == null) continue;

                target.GetComponentsInChildren(false, rendererBuffer);
                bool found = false;
                Bounds bounds = default;
                foreach (Renderer renderer in rendererBuffer)
                {
                    if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                    if (found) bounds.Encapsulate(renderer.bounds);
                    else bounds = renderer.bounds;
                    found = true;
                }
                if (found == false) continue;

                Vector3 size = bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) >= occlusionDebrisMaxSize) continue;
                foreach (Renderer renderer in rendererBuffer)
                    if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) smallOccluders.Add(renderer);
            }
            rendererBuffer.Clear();
        }

        // 새 보스의 체력·무력화 수치로 바를 바로 맞춤
        private void BindBoss()
        {
            groggy = boss != null ? boss.GetComponent<BossGroggy>() : null;
            dummy = boss != null ? boss.GetComponent<TrainingDummy>() : null;
            if (hpBar != null) hpBar.SetFillImmediate(GetHpRatio());
            if (groggyBar != null) groggyBar.SetFillImmediate(GetGroggyRatio());
            UpdateName();
            UpdateOcclusionTargets();
        }

        private void UpdateOcclusionTargets()
        {
            if (boss == null)
            {
                BossOcclusionOutlineFeature.ClearTargets();
                return;
            }

            // 플레이어가 아직 없어도 보스 실루엣 채우기는 그릴 수 있도록 보스만 먼저 넘김
            BossOcclusionOutlineFeature.SetTargets(boss.gameObject, player != null ? player.gameObject : null);
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

        // 보스가 없는 곳(로비)에서는 플레이어가 들어가 있는 수련장의 허수아비를 보여 줌
        private static CombatEntity FindActiveBoss()
        {
            BossMonster monster = FindAnyObjectByType<BossMonster>();
            if (monster != null) return monster.GetComponent<CombatEntity>();

            return TrainingDummy.Focused != null ? TrainingDummy.Focused.CombatEntity : null;
        }

        private bool IsLeftDummy() => dummy != null && TrainingDummy.Focused != dummy;
    }
}
