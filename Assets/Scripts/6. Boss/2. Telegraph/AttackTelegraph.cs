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
    // 바닥에 눕힌 Quad 하나에 BossTelegraph 셰이더로 모양을 그리고, 모양과 진행도는 MaterialPropertyBlock으로 넘김
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AttackTelegraph : MonoBehaviour, IObjectPoolItem
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        // 모든 예고 표시가 공유하는 XZ 평면의 1x1 Quad, uv의 y가 forward 방향
        private static Mesh quadMesh;

        [SerializeField] private string poolKey;

        [Tooltip("예고와 공격에 함께 쓰는 색, 알파는 셰이더의 각 영역 알파에 곱해진다.")]
        [SerializeField] private Color color = new Color(1f, 0.25f, 0.1f, 1f);
        [Tooltip("바닥에 묻히지 않도록 띄우는 높이(m)")]
        [SerializeField, Min(0f)] private float heightOffset = 0.05f;

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

        public string Key { get => poolKey; set => poolKey = value; }
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = GetQuadMesh();
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            isInPool = false;
        }

        private void OnDisable()
        {
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

            if (isFilling == false) return;

            float elapsed = fillDuration > 0f ? Mathf.Clamp01((Now - fillStartTime) / fillDuration) : 1f;
            properties.SetFloat(ProgressId, Mathf.Lerp(fillFrom, 1f, elapsed));
            meshRenderer.SetPropertyBlock(properties);

            if (resolvesWhenFilled && elapsed >= 1f) Resolve(resolveFadeDuration);
        }

        #region PUBLIC_METHODS

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

        // 차오르는 진행도는 그대로 두고 위치와 모양만 바꿈
        public void SetPlacement(TelegraphShape shape, Pose origin)
        {
            Vector3 forward = TelegraphShape.FlatForward(origin.rotation);
            Vector3 position = origin.position + Vector3.up * heightOffset;
            Vector2 size;

            if (shape.kind == TelegraphShape.ShapeKind.Rectangle)
            {
                // Quad는 중심 기준이라 사각형의 시작점이 origin에 오도록 길이의 절반만큼 앞으로 옮김
                position += forward * (shape.length * 0.5f);
                size = new Vector2(shape.width, shape.length);
            }
            else
            {
                size = new Vector2(shape.radius * 2f, shape.radius * 2f);
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = new Vector3(size.x, 1f, size.y);

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

        private static Mesh GetQuadMesh()
        {
            if (quadMesh != null) return quadMesh;

            quadMesh = new Mesh { name = "AttackTelegraphQuad" };
            quadMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f)
            });
            quadMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            quadMesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            quadMesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            quadMesh.RecalculateBounds();
            return quadMesh;
        }
    }
}
