using System;
using Sirenix.OdinInspector;
using TeamOdd.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd
{
    // 메인 메뉴 배경 보스의 맥박 루프 (Arcanist 타이틀 Glow_Flash의 박동 곡선을 옮김)
    // - 쉬다가 약하게 한 번, 강하게 한 번 두근거리고 천천히 가라앉는 7초 루프
    // - 홀로그램 보스(원본 프리팹에 로비 홀로그램 머티리얼, T-포즈)는 늘 떠서 천천히 돌고 박동에 맞춰 밝아짐, 무작위 간격으로 짧게 글리치
    // - 스캔 중인 대상처럼 발밑에 도는 링을 두고, 강한 박동마다 스캔 링이 발밑에서 머리까지 훑고 올라가 사라짐 (보스 회전·튐과 따로 움직임)
    //   (셰이더의 띠 어긋남 _RandomGlitchConstant·색 빠짐 _ColorGlitchAffect를 올리고, 옆으로 튀며 투명도가 끊김)
    // - 강한 박동 순간마다 스캔이 시작되고 제목 글자가 짧게 지직거림(이때 글자 하나가 잠깐 좌우로 뒤집힘), 제목은 Title_Floating처럼 6초 주기로 살짝 떠오르며 커진다
    // - 글리치 중 가끔 아주 잠깐 보스 대신 같은 자리·같은 회전·같은 키의 플레이어 홀로그램이 비쳤다가 돌아옴
    // - 메뉴가 다 나타난 뒤(Visibility 1)부터 깜빡임, 메뉴는 시간 정지와 무관하므로 실제 시간으로 진행
    public class TitleHologramPulse : MonoBehaviour
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GlitchConstantId = Shader.PropertyToID("_RandomGlitchConstant");
        private static readonly int GlitchAmountId = Shader.PropertyToID("_RandomGlitchAmount");
        private static readonly int ColorGlitchId = Shader.PropertyToID("_ColorGlitchAffect");
        private static readonly int NormalAffectId = Shader.PropertyToID("_NormalAffect");

        [Title("Beat")]
        [Tooltip("박동 세기(0~1), 가로축은 초. 마지막 키 시간이 한 루프 길이")]
        [SerializeField] private AnimationCurve beat = CreateDefaultBeat();
        [Tooltip("메뉴가 뜬 뒤 첫 박동까지 너무 오래 쉬지 않도록 루프 중간에서 시작")]
        [SerializeField, Min(0f)] private float startTime = 1.8f;

        [Title("Hologram")]
        [SerializeField, Range(0f, 1f)] private float restAlpha = 0.45f; // 박동 사이 불투명도
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.8f; // 강한 박동 때 불투명도
        [SerializeField] private float floatHeight = 0.06f; // 제목과 같은 주기로 둥실거리는 높이(m)
        [SerializeField] private float turntableSpeed = 18f; // 회전 속도(도/초), 18이면 20초에 한 바퀴
        [Tooltip("노멀맵 밝기가 가장자리 빛에 더해지는 정도(로비 머티리얼 0.589), 낮추면 파편 같은 반짝임은 줄지만 0이면 각진 면이 그대로 드러남 (공유 머티리얼은 그대로)")]
        [SerializeField, Range(0f, 1f)] private float normalAffect = 0.589f;

        [Title("Scan")]
        [SerializeField] private Transform baseRing; // 발밑에 깔려 도는 링
        [SerializeField] private Transform scanRing; // 박동마다 몸을 아래에서 위로 훑는 링
        [SerializeField] private float ringPadding = 1.15f; // 링 지름 = 보스 가로 크기 × 이 값
        [SerializeField] private float baseRingSpeed = -30f; // 발밑 링 회전 속도(도/초), 보스와 반대로
        [SerializeField, Min(0.1f)] private float scanDuration = 2.2f; // 스캔 링이 발밑에서 머리까지 올라가는 시간(초)
        [SerializeField, Range(0f, 1f)] private float scanFadeStart = 0.55f; // 올라가는 진행도 중 이 지점부터 사라지기 시작
        [SerializeField] private Color ringColor = new(0.55f, 0.85f, 1f, 0.6f); // 메뉴와 함께 서서히 나타남

        [Title("Hologram Glitch")]
        [SerializeField] private Vector2 glitchInterval = new(1.5f, 4f); // 다음 글리치까지 무작위 간격(초)
        [SerializeField] private Vector2 glitchDuration = new(0.15f, 0.4f); // 한 번 지직거리는 시간(초)
        [SerializeField, Min(0.01f)] private float glitchStep = 0.05f; // 값이 바뀌는 간격(초), 디지털 신호처럼 끊겨 보이게
        [SerializeField] private Vector2 glitchConstant = new(0.3f, 0.7f); // 띠 어긋남 세기 범위
        [Tooltip("띠가 옆으로 밀리는 정점 변위, 모델이 3.25배라 작게 둬야 찢어지지 않음")]
        [SerializeField, Range(0f, 1f)] private float glitchAmount = 0.06f;
        [Tooltip("글리치가 없을 때의 정점 변위, 로비 머티리얼은 0.122로 늘 흔들지만 메뉴에선 글리치 때만 흔들리게 0")]
        [SerializeField, Range(0f, 1f)] private float idleGlitchAmount = 0f;
        [SerializeField, Range(0f, 1f)] private float glitchColor = 1f; // 색 빠짐 세기
        [SerializeField, Range(0f, 1f)] private float glitchDropChance = 0.3f; // 칸마다 투명도가 뚝 떨어질 확률
        [SerializeField] private float jitter = 0.15f; // 위치가 옆으로 튀는 거리(m)

        [Title("Swap")]
        [SerializeField, Range(0f, 1f)] private float swapChance = 0.35f; // 글리치 한 번에 플레이어가 비칠 확률
        [SerializeField, Min(0.01f)] private float swapDuration = 0.15f; // 플레이어가 비치는 시간(초)

        [Title("Title Glitch")]
        [Tooltip("제목 글자에 붙인 UIGlitch, 떠오름이 움직이는 Title이 아니라 그 안의 글자에 붙여야 위치가 겹치지 않음")]
        [SerializeField] private UIGlitch titleGlitch;
        [SerializeField, Min(0f)] private float titleGlitchDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float titleGlitchStrength = 0.45f;
        [Tooltip("글리치가 튈 때 좌우로 뒤집혔다 돌아오는 글자(ODDITY의 세 번째 D), 피벗을 글자 가운데에 둬야 제자리에서 뒤집힘")]
        [SerializeField] private RectTransform flipLetter;
        [SerializeField, Range(0f, 1f)] private float flipChance = 1f; // 글리치 한 번에 뒤집힐 확률
        [SerializeField, Min(0f)] private float flipHold = 0.3f; // 뒤집힌 채로 있는 시간(초)

        [Title("Title Floating")]
        [SerializeField] private RectTransform title;
        [SerializeField] private float titleFloatOffset = 6f;
        [SerializeField] private float titleFloatScale = 0.007f;
        [SerializeField, Min(0.1f)] private float floatPeriod = 6f;

        private MaterialPropertyBlock properties;
        // 앞면만 보이게 깊이를 먼저 쓰는 두 번째 머티리얼(같은 홀로그램 셰이더, 투명도 0)용, 글리치 변위 값은 본체와 같게 맞춰 같이 움직이게 함
        private MaterialPropertyBlock depthProperties;
        private Transform hologram; // 씬에 배치한 메뉴 보스
        private Renderer[] hologramRenderers;
        private Vector3 hologramBase;
        private Quaternion hologramBaseRotation;
        private Vector3 spinAxis; // 몸 가운데를 지나는 세로축 위치(높이 무시), 피벗이 몸에서 벗어나 있어도 제자리에서 돌게
        private Vector3 jitterOffset;
        private Transform alternate; // 글리치 때 비칠 플레이어
        private Renderer[] alternateRenderers;
        private Vector3 alternateOffset; // 보스 피벗 기준 플레이어 피벗의 가로 위치(보스 로컬), 몸 가운데끼리 겹치게
        private float alternateFootOffset; // 보스 피벗 기준 플레이어 피벗 높이, 발밑끼리 겹치게
        private Vector3 alternateBaseScale; // 처음 크기, 다시 Bind돼도 키운 값이 겹쳐 곱해지지 않게
        private float swapStart = -1f; // 이번 글리치에서 플레이어가 비치기 시작하는 시점, 음수면 안 바뀜
        private float footHeight; // 보스 발밑·머리 높이(월드), 링이 움직일 범위
        private float headHeight;
        private float turntableAngle;
        private MaterialPropertyBlock baseRingProperties;
        private MaterialPropertyBlock scanRingProperties;
        private float baseGlitchConstant; // 머티리얼 기본값, 글리치가 끝나면 되돌림
        private float baseGlitchAmount;
        private float baseColorGlitch;
        private float glitchTime = -1f;
        private float glitchLength;
        private float nextGlitchAt;
        private float nextStep;
        private float glitchAlpha = 1f;
        private Vector2 titleBase;
        private Vector3 titleBaseScale;
        private float loopLength;
        private float peakTime;
        private float time;
        private float scanTime = -1f;
        private float flipTime = -1f;

        public float Visibility { get; set; }

        // 오른쪽 분석 HUD(TitleScanHud)가 읽는 값
        public event Action Peaked; // 강한 박동 순간
        public bool IsBound => hologram != null;
        public int BindVersion { get; private set; } // 새로 Bind될 때마다 올라감, HUD가 화면 영역을 다시 잼
        public Bounds HologramBounds { get; private set; }
        public bool IsGlitching => glitchTime >= 0f;
        public bool IsSwapped => glitchTime >= 0f && swapStart >= 0f && glitchTime >= swapStart && glitchTime < swapStart + swapDuration;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            depthProperties = new MaterialPropertyBlock();
            depthProperties.SetFloat(AlphaId, 0f);

            loopLength = beat.length > 0 ? Mathf.Max(beat[beat.length - 1].time, 0.1f) : 1f;
            peakTime = FindPeakTime(beat);

            if (title != null)
            {
                titleBase = title.anchoredPosition;
                titleBaseScale = title.localScale;
            }

            time = startTime;
            Apply(0f);
        }

        // 메뉴가 뜰 때 켜진 홀로그램 보스를 넘겨받음, 지금 자리를 둥실거림의 기준으로 삼음
        public void Bind(Transform root, Renderer[] renderers, Bounds bounds)
        {
            hologram = root;
            hologramRenderers = renderers;
            hologramBase = root.position;
            hologramBaseRotation = root.rotation;
            HologramBounds = bounds;
            BindVersion++;
            spinAxis = new Vector3(bounds.center.x, 0f, bounds.center.z);
            turntableAngle = 0f;
            BindScan(bounds);

            Material material = renderers.Length > 0 ? renderers[0].sharedMaterial : null;
            baseGlitchConstant = material != null ? material.GetFloat(GlitchConstantId) : 0f;
            baseGlitchAmount = idleGlitchAmount;
            baseColorGlitch = material != null ? material.GetFloat(ColorGlitchId) : 0f;
            ResetGlitch();
            properties.SetFloat(NormalAffectId, normalAffect);

            time = startTime;
            scanTime = -1f;
        }

        // 보스와 같은 자세로 세워 둘 다 휴머노이드 뼈로 잰 뒤, 키(머리~발목)를 보스에 맞춰 키우고 숨겨 둠, Bind 뒤에 부름
        // 메쉬 크기는 부위를 조합한 플레이어 모델이나 넉넉한 렌더러 bounds 때문에 어긋나므로 뼈로 잼
        // 위치는 보스 피벗 기준으로 저장해 두고 매 프레임 보스를 따라감 (엉덩이 가로 위치·발목 높이가 보스와 겹치게)
        public void BindAlternate(Transform root, Renderer[] renderers)
        {
            alternate = root;
            alternateRenderers = renderers;
            if (alternateBaseScale == Vector3.zero) alternateBaseScale = root.localScale;
            root.localScale = alternateBaseScale;
            root.SetPositionAndRotation(hologramBase, hologramBaseRotation);
            alternateOffset = Vector3.zero;
            alternateFootOffset = 0f;

            if (TryMeasureHuman(hologram, out Vector3 bossHips, out float bossHead, out float bossFoot)
                && TryMeasureHuman(root, out Vector3 playerHips, out float playerHead, out float playerFoot)
                && playerHead - playerFoot > 0.01f)
            {
                float scale = (bossHead - bossFoot) / (playerHead - playerFoot);
                root.localScale = alternateBaseScale * scale;

                // 보스 피벗에서 본 보스 엉덩이(가로)·발목 − 키운 플레이어 피벗에서 본 엉덩이·발목
                Quaternion toLocal = Quaternion.Inverse(hologramBaseRotation);
                alternateOffset = toLocal * Flat(bossHips - hologramBase) - toLocal * Flat(playerHips - hologramBase) * scale;
                alternateFootOffset = (bossFoot - hologramBase.y) - (playerFoot - hologramBase.y) * scale;
            }
            else
            {
                Debug.LogWarning($"[Title] {root.name} 또는 보스 홀로그램의 휴머노이드 뼈를 찾지 못해 크기를 맞추지 못했습니다.");
            }

            SetSwapped(false);
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);

        // 아바타의 뼈 이름표로 엉덩이 위치, 머리 높이, 두 발목 중 낮은 높이를 찾음 (애니메이터가 꺼져 있어도 됨)
        private static bool TryMeasureHuman(Transform root, out Vector3 hips, out float head, out float foot)
        {
            hips = Vector3.zero;
            head = foot = 0f;

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || animator.avatar.isHuman == false) return false;

            var names = new System.Collections.Generic.Dictionary<string, string>();
            foreach (HumanBone bone in animator.avatar.humanDescription.human) names[bone.humanName] = bone.boneName;

            Transform Find(string humanName)
            {
                if (names.TryGetValue(humanName, out string boneName) == false) return null;
                foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
                    if (child.name == boneName) return child;
                return null;
            }

            Transform hipsBone = Find("Hips");
            Transform headBone = Find("Head");
            Transform leftFoot = Find("LeftFoot");
            Transform rightFoot = Find("RightFoot");
            if (hipsBone == null || headBone == null || leftFoot == null || rightFoot == null) return false;

            hips = hipsBone.position;
            head = headBone.position.y;
            foot = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            return true;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            float before = time % loopLength;
            time += deltaTime;
            float loopTime = time % loopLength;

            // 강한 박동 키를 지나는 순간 스캔을 새로 시작
            if (before < peakTime && loopTime >= peakTime)
            {
                scanTime = 0f;
                Peaked?.Invoke();
                // 메뉴가 다 나타난 뒤에만 (나타나는 중엔 투명도를 건드려 어색함)
                if (titleGlitch != null && Visibility >= 1f)
                {
                    titleGlitch.Play(titleGlitchDuration, titleGlitchStrength);
                    if (flipLetter != null && Random.value < flipChance) SetFlip(true);
                }
            }

            UpdateFlip(deltaTime);
            UpdateGlitch(deltaTime);
            if (scanTime >= 0f) scanTime += deltaTime;
            Apply(beat.Evaluate(loopTime));
        }

        // 제목 글리치와 함께 글자 하나를 좌우로 뒤집었다가 flipHold 뒤 되돌림
        private void SetFlip(bool flipped)
        {
            flipLetter.localScale = new Vector3(flipped ? -1f : 1f, 1f, 1f);
            flipTime = flipped ? flipHold : -1f;
        }

        private void UpdateFlip(float deltaTime)
        {
            if (flipTime < 0f) return;

            flipTime -= deltaTime;
            if (flipTime < 0f) SetFlip(false);
        }

        // 메뉴가 다 나타난 뒤 무작위 간격으로 짧게 지직거림
        private void UpdateGlitch(float deltaTime)
        {
            if (hologram == null || Visibility < 1f) return;

            if (glitchTime < 0f)
            {
                nextGlitchAt -= deltaTime;
                if (nextGlitchAt > 0f) return;

                glitchTime = 0f;
                nextStep = 0f;
                glitchLength = Random.Range(glitchDuration.x, glitchDuration.y);
                swapStart = alternate != null && Random.value < swapChance ? Random.Range(0f, Mathf.Max(glitchLength - swapDuration, 0f)) : -1f;
            }

            glitchTime += deltaTime;
            if (glitchTime >= glitchLength)
            {
                ResetGlitch();
                return;
            }

            if (glitchTime < nextStep) return;
            nextStep += glitchStep;

            // 칸마다 세기·위치·투명도를 새로 골라 끊겨 보이게
            properties.SetFloat(GlitchConstantId, Random.Range(glitchConstant.x, glitchConstant.y));
            properties.SetFloat(GlitchAmountId, glitchAmount);
            properties.SetFloat(ColorGlitchId, glitchColor);
            jitterOffset = Random.value < 0.5f ? hologram.right * (Random.Range(-1f, 1f) * jitter) : Vector3.zero;
            glitchAlpha = Random.value < glitchDropChance ? Random.Range(0f, 0.4f) : 1f;
        }

        private void ResetGlitch()
        {
            glitchTime = -1f;
            swapStart = -1f;
            nextGlitchAt = Random.Range(glitchInterval.x, glitchInterval.y);
            jitterOffset = Vector3.zero;
            glitchAlpha = 1f;
            properties.SetFloat(GlitchConstantId, baseGlitchConstant);
            properties.SetFloat(GlitchAmountId, baseGlitchAmount);
            properties.SetFloat(ColorGlitchId, baseColorGlitch);
        }

        private void Apply(float strength)
        {
            float wave = (1f - Mathf.Cos(time * Mathf.PI * 2f / floatPeriod)) * 0.5f;

            ApplyHologram(strength, wave);

            if (title != null)
            {
                title.anchoredPosition = titleBase + Vector2.up * (titleFloatOffset * wave);
                title.localScale = titleBaseScale * (1f + titleFloatScale * wave);
            }
        }

        private void ApplyHologram(float strength, float wave)
        {
            if (hologram == null) return;

            turntableAngle += turntableSpeed * Time.unscaledDeltaTime;
            Quaternion spin = Quaternion.AngleAxis(turntableAngle, Vector3.up);
            Vector3 fromAxis = hologramBase - new Vector3(spinAxis.x, hologramBase.y, spinAxis.z);
            hologram.SetPositionAndRotation(hologramBase - fromAxis + spin * fromAxis + Vector3.up * (floatHeight * wave) + jitterOffset,
                spin * hologramBaseRotation);
            UpdateScan(wave);

            // 플레이어가 비치는 동안엔 투명도가 끊기지 않게 해 짧아도 알아볼 수 있게 함
            bool swapped = IsSwapped;
            properties.SetFloat(AlphaId, Mathf.Lerp(restAlpha, peakAlpha, strength) * (swapped ? 1f : glitchAlpha) * Visibility);
            depthProperties.SetFloat(GlitchConstantId, properties.GetFloat(GlitchConstantId));
            depthProperties.SetFloat(GlitchAmountId, properties.GetFloat(GlitchAmountId));
            foreach (Renderer item in hologramRenderers) SetBlocks(item);

            if (alternate == null) return;

            SetSwapped(swapped);
            Quaternion rotation = hologram.rotation;
            alternate.SetPositionAndRotation(hologram.position + rotation * alternateOffset + Vector3.up * alternateFootOffset, rotation);
            foreach (Renderer item in alternateRenderers) SetBlocks(item);
        }

        // 0번은 보이는 홀로그램, 1번(있으면)은 깊이만 쓰는 같은 셰이더
        private void SetBlocks(Renderer item)
        {
            item.SetPropertyBlock(properties, 0);
            if (item.sharedMaterials.Length > 1) item.SetPropertyBlock(depthProperties, 1);
        }

        private void SetSwapped(bool swapped)
        {
            foreach (Renderer item in hologramRenderers) item.enabled = swapped == false;
            foreach (Renderer item in alternateRenderers) item.enabled = swapped;
        }

        // T-포즈라 팔까지 들어가도록 가로 크기 중 큰 쪽(돌아가며 바뀌므로 대각선)에 맞춤
        private void BindScan(Bounds bounds)
        {
            footHeight = bounds.min.y;
            headHeight = bounds.max.y;
            float diameter = new Vector2(bounds.size.x, bounds.size.z).magnitude * ringPadding;

            foreach (Transform ringTransform in new[] { baseRing, scanRing })
            {
                if (ringTransform == null) continue;
                ringTransform.localScale = new Vector3(diameter, diameter, 1f);
                ringTransform.gameObject.SetActive(true);
            }
        }

        // 링은 보스 회전·튐을 따르지 않고 둥실거림만 같이 함
        // 스캔 링은 발밑에서 짧게 켜져 처음엔 빠르게, 머리에 가까울수록 느려지며 올라가고 후반부터 서서히 사라짐
        private void UpdateScan(float wave)
        {
            Vector3 center = spinAxis;
            float lift = floatHeight * wave;

            if (baseRing != null)
            {
                baseRing.position = center + Vector3.up * (footHeight + lift + 0.02f);
                baseRing.rotation = Quaternion.AngleAxis(baseRingSpeed * time, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                SetRingColor(baseRing, ref baseRingProperties, ringColor.a * Visibility);
            }

            if (scanRing == null) return;

            float progress = scanTime >= 0f ? Mathf.Clamp01(scanTime / scanDuration) : 1f;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float fade = Mathf.SmoothStep(0f, 1f, progress / 0.08f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(scanFadeStart, 1f, progress)));
            scanRing.position = center + Vector3.up * (Mathf.Lerp(footHeight, headHeight, eased) + lift);
            scanRing.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetRingColor(scanRing, ref scanRingProperties, ringColor.a * Visibility * fade);
            if (progress >= 1f) scanTime = -1f;
        }

        private void SetRingColor(Transform ringTransform, ref MaterialPropertyBlock block, float alpha)
        {
            if (ringTransform.TryGetComponent(out Renderer ringRenderer) == false) return;

            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(ringColor.r, ringColor.g, ringColor.b, alpha));
            ringRenderer.SetPropertyBlock(block);
        }

        private static float FindPeakTime(AnimationCurve curve)
        {
            float peak = 0f;
            float max = float.MinValue;

            for (int index = 0; index < curve.length; index++)
            {
                if (curve[index].value <= max) continue;

                max = curve[index].value;
                peak = curve[index].time;
            }

            return peak;
        }

        // Glow_Flash 알파 0.2(쉼) → 0.4 → 0.25 → 0.55 → 0.2 를 0~1 세기로 옮긴 값
        private static AnimationCurve CreateDefaultBeat() => new(
            new Keyframe(0f, 0f),
            new Keyframe(3f, 0f),
            new Keyframe(3.1f, 0.57f),
            new Keyframe(3.2f, 0.14f),
            new Keyframe(3.3f, 1f),
            new Keyframe(7f, 0f));
    }
}
