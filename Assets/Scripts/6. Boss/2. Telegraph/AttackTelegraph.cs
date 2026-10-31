using DG.Tweening;
using TeamOdd.TimeControl;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamOdd.Boss
{
    // 보스 공격의 피격 범위를 바닥에 미리 표시하는 풀 항목
    // - Show: 외곽선과 함께 안쪽이 warnDuration 동안 차오름, 실제 판정 시점은 패턴의 공격 신호가 정함
    // - Resolve: 공격이 들어가는 순간 안쪽을 가득 채운 뒤 사라지며 풀로 귀환
    // - Cancel: 패턴이 끊겼을 때 바로 귀환
    // - SetPlacement: 차오르는 진행도는 유지한 채 위치와 모양만 갱신(대상을 따라가는 예고 등)
    // - ResolveWhenFilled: 다 차오르면 스스로 Resolve, 공격 신호 없이 시간으로 끝나는 예고(낙하, 투척 비행)에 사용
    // - Show에 시계(TimeFreezable)를 넘기면 그 시계가 멈춘 동안 차오름과 사라짐도 멈춤
    // 바닥 높이 근처를 덮는 상자 하나에 BossTelegraph 셰이더로 모양을 그리고, 모양과 진행도는 MaterialPropertyBlock으로 넘김
    // 셰이더가 상자 안에 보이는 바닥면에 모양을 투영(화면 공간 데칼)하므로 단차 위에도 틈 없이 붙고, 플레이어 몸에는 그리지 않음
    // 보스·플레이어 렌더러가 보이는 픽셀은 TelegraphMaskFeature가 스텐실로 표시해 건너뜀
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AttackTelegraph : MonoBehaviour, IObjectPoolItem
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        // 예고를 그리지 않을 플레이어 영역(xyz 발바닥 중심, w 반경)과 높이, 모든 예고가 공유
        private static readonly int ExcludeId = Shader.PropertyToID("_TelegraphExclude");
        private static readonly int ExcludeHeightId = Shader.PropertyToID("_TelegraphExcludeHeight");
        private const float EXCLUDE_RADIUS_SCALE = 1.5f; // 팔·무기가 캡슐 밖으로 나오는 만큼
        private const float EXCLUDE_EXTRA_HEIGHT = 1f; // 모델이 캡슐보다 큰 만큼

        // 모든 예고 표시가 공유하는 바닥이 y=0인 1x1x1 상자, z가 forward 방향
        private static Mesh boxMesh;
        private static int excludeFrame = -1;

        [SerializeField] private string poolKey;

        [Tooltip("예고와 공격에 함께 쓰는 색, 알파는 셰이더의 각 영역 알파에 곱해진다.")]
        [SerializeField] private Color color = new Color(1f, 0.25f, 0.1f, 1f);
        [Tooltip("origin보다 이만큼 낮은 바닥까지 그림(m), 더 깊은 곳(구멍 아래)에는 그리지 않음")]
        [SerializeField, Min(0f)] private float depthBelow = 0.5f;
        [Tooltip("origin보다 이만큼 높은 바닥(단차)까지 그림(m), BossMovement의 stepHeight보다 조금 크게")]
        [SerializeField, Min(0f)] private float heightAbove = 1.2f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private Tween fadeTween;
        // 차오름은 fillStartTime부터 fillDuration 동안 fillFrom에서 1까지 진행
        private float fillStartTime;
        private float fillDuration;
        private float fillFrom;
        private float resolveFadeDuration;
        private bool isFilling;
        private bool resolvesWhenFilled;
        private bool isInPool;
        private TimeFreezable clock;

        private float Now => clock != null ? clock.LocalTime : Time.time;

        // 지금 켜진 예고 수, 0이면 TelegraphMaskFeature가 마스크를 그리지 않음
        public static int ActiveCount { get; private set; }

        public string Key { get => poolKey; set => poolKey = value; }
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = GetBoxMesh();
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            properties = new MaterialPropertyBlock();
        }

        // 도메인 리로드 없이 플레이 모드에 들어가도 지난 플레이의 수가 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveCount() => ActiveCount = 0;

        private void OnEnable()
        {
            isInPool = false;
            ActiveCount++;
        }

        private void OnDisable()
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            fadeTween?.Kill();
            isFilling = false;
            resolvesWhenFilled = false;
            clock = null;
        }

        private void Update()
        {
            if (fadeTween != null && fadeTween.IsActive())
            {
                if (clock != null && clock.IsFrozen) fadeTween.Pause();
                else fadeTween.Play();
            }

            UpdateExcludeArea();

            if (isFilling == false) return;

            float elapsed = fillDuration > 0f ? Mathf.Clamp01((Now - fillStartTime) / fillDuration) : 1f;
            properties.SetFloat(ProgressId, Mathf.Lerp(fillFrom, 1f, elapsed));
            meshRenderer.SetPropertyBlock(properties);

            if (resolvesWhenFilled && elapsed >= 1f) Resolve(resolveFadeDuration);
        }

        #region PUBLIC_METHODS

        // Show 뒤에 부르면 이번 표시만 다른 색으로 그림, 풀로 돌아가 다시 Show하면 기본 색으로 돌아감
        public void SetColor(Color value)
        {
            properties.SetColor(BaseColorId, value);
            meshRenderer.SetPropertyBlock(properties);
        }

        // origin은 높이까지 포함한 바닥 위치, 부채꼴과 사각형은 origin의 수평 forward 방향으로 펼쳐짐
        public void Show(TelegraphShape shape, Pose origin, float warnDuration, TimeFreezable clock = null)
        {
            fadeTween?.Kill();
            this.clock = clock;
            SetPlacement(shape, origin);

            properties.SetColor(BaseColorId, color);
            properties.SetFloat(ProgressId, 0f);
            properties.SetFloat(FadeId, 1f);
            meshRenderer.SetPropertyBlock(properties);

            fillStartTime = Now;
            fillDuration = warnDuration;
            fillFrom = 0f;
            isFilling = true;
            resolvesWhenFilled = false;
            gameObject.SetActive(true);
        }

        // 색·위치는 그대로 두고 안쪽을 처음부터 다시 warnDuration 동안 차오르게 함, 콤보처럼 한 표시로 여러 번 때릴 때 다음 타격까지의 박자를 보여 줌
        public void Restart(float warnDuration)
        {
            fadeTween?.Kill();
            properties.SetFloat(ProgressId, 0f);
            properties.SetFloat(FadeId, 1f);
            meshRenderer.SetPropertyBlock(properties);

            fillStartTime = Now;
            fillDuration = warnDuration;
            fillFrom = 0f;
            isFilling = true;
            resolvesWhenFilled = false;
        }

        // 차오르는 진행도는 그대로 두고 위치와 모양만 바꿈
        public void SetPlacement(TelegraphShape shape, Pose origin)
        {
            Vector3 forward = TelegraphShape.FlatForward(origin.rotation);
            Vector3 position = origin.position + Vector3.down * depthBelow;
            Vector2 size;

            if (shape.kind == TelegraphShape.ShapeKind.Rectangle)
            {
                // 상자는 중심 기준이라 사각형의 시작점이 origin에 오도록 길이의 절반만큼 앞으로 옮김
                position += forward * (shape.length * 0.5f);
                size = new Vector2(shape.width, shape.length);
            }
            else
            {
                size = new Vector2(shape.radius * 2f, shape.radius * 2f);
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = new Vector3(size.x, depthBelow + heightAbove, size.y);

            float halfAngle = shape.kind == TelegraphShape.ShapeKind.Sector ? shape.angle * 0.5f * Mathf.Deg2Rad : Mathf.PI;

            properties.SetFloat(ShapeId, (float)shape.kind);
            properties.SetVector(SizeId, size);
            properties.SetFloat(HalfAngleId, halfAngle);
            meshRenderer.SetPropertyBlock(properties);
        }

        // 지금까지 찬 만큼에서 이어서 remainingDuration 동안 끝까지 차오른 뒤 스스로 Resolve
        // 이미 다 찼어도 remainingDuration이 지나야 Resolve, 이후 스스로 풀로 돌아가므로 호출한 쪽은 참조를 버려야 함
        public void ResolveWhenFilled(float remainingDuration, float fadeDuration)
        {
            fillFrom = properties.GetFloat(ProgressId);
            fillStartTime = Now;
            fillDuration = remainingDuration;
            resolveFadeDuration = fadeDuration;
            resolvesWhenFilled = true;
            isFilling = true;
        }

        public void Resolve(float fadeDuration)
        {
            isFilling = false;
            resolvesWhenFilled = false;
            fadeTween?.Kill();

            properties.SetFloat(ProgressId, 1f);
            properties.SetFloat(FadeId, 1f);
            meshRenderer.SetPropertyBlock(properties);

            fadeTween = DOTween.To(() => 1f, SetFade, 0f, fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(ReturnToPool);
        }

        public void Cancel() => ReturnToPool();

        public void ReturnToPool()
        {
            if (isInPool) return;

            fadeTween?.Kill();
            isFilling = false;

            // 풀을 거치지 않고 만든 오브젝트는 그대로 제거
            if (ObjectPoolManager.Instance == null || string.IsNullOrEmpty(poolKey))
            {
                Destroy(gameObject);
                return;
            }

            isInPool = true;
            ObjectPoolManager.Instance.ReturnToPool(this);
        }

        #endregion

        private void SetFade(float fade)
        {
            properties.SetFloat(FadeId, fade);
            meshRenderer.SetPropertyBlock(properties);
        }

        // 플레이어 발바닥 위 캡슐 영역은 예고를 그리지 않도록 셰이더에 넘김, 프레임마다 한 번만
        private static void UpdateExcludeArea()
        {
            if (excludeFrame == Time.frameCount) return;

            excludeFrame = Time.frameCount;
            Player.Player player = Player.Player.Instance;
            if (player == null || player.CharacterController == null)
            {
                Shader.SetGlobalVector(ExcludeId, Vector4.zero);
                return;
            }

            TelegraphMaskFeature.Add(player.gameObject);

            Bounds bounds = player.CharacterController.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * EXCLUDE_RADIUS_SCALE;
            Shader.SetGlobalVector(ExcludeId, new Vector4(bounds.center.x, bounds.min.y, bounds.center.z, radius));
            Shader.SetGlobalFloat(ExcludeHeightId, bounds.size.y + EXCLUDE_EXTRA_HEIGHT);
        }

        private static Mesh GetBoxMesh()
        {
            if (boxMesh != null) return boxMesh;

            // 셰이더가 뒷면을 그리므로 법선·UV는 쓰지 않음
            Vector3[] vertices =
            {
                new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(0.5f, 0f, 0.5f), new(-0.5f, 0f, 0.5f),
                new(-0.5f, 1f, -0.5f), new(0.5f, 1f, -0.5f), new(0.5f, 1f, 0.5f), new(-0.5f, 1f, 0.5f),
            };
            int[] triangles =
            {
                0, 1, 2, 0, 2, 3, // 아래
                4, 6, 5, 4, 7, 6, // 위
                0, 4, 5, 0, 5, 1, // 뒤
                1, 5, 6, 1, 6, 2, // 오른쪽
                2, 6, 7, 2, 7, 3, // 앞
                3, 7, 4, 3, 4, 0, // 왼쪽
            };

            boxMesh = new Mesh { name = "AttackTelegraphBox" };
            boxMesh.SetVertices(vertices);
            boxMesh.SetTriangles(triangles, 0);
            boxMesh.RecalculateBounds();
            return boxMesh;
        }
    }
}
