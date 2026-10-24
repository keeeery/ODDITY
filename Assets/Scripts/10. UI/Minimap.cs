using System.Collections.Generic;
using TeamOdd.Boss;
using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 플레이어를 중심으로 카메라 Yaw에 맞춰 회전하는 원형 미니맵
    // 바닥은 위에서 내려다보는 전용 카메라로 Ground 레이어만 그리고(Wall 등은 비어 보임),
    // 보스와 염동력 오브젝트는 점으로, 플레이어는 바라보는 방향이 뾰족한 점으로 그린다. y 값은 무시한다.
    // 메인 카메라가 LateUpdate에서 회전한 뒤에 따라가도록 실행 순서를 늦춘다.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CanvasRenderer))]
    public class Minimap : MaskableGraphic
    {
        private const int CircleSegments = 24;
        private const int BackgroundSegments = 72; // 배경은 크게 그려지므로 각지지 않게 더 잘게 나눈다

        [Header("References")]
        [SerializeField] private Player.Player player;
        [SerializeField] private BossMonster boss;
        [SerializeField] private Transform viewCamera;
        // 비워두면 실행 시 이 오브젝트 바로 뒤(아래)에 형제로 생성
        [SerializeField] private MinimapGround ground;

        [Header("Range")]
        // 미니맵 반지름이 나타내는 월드 거리(m)
        [SerializeField, Min(1f)] private float worldRadius = 30f;

        [Header("Offscreen Indicator")]
        // 범위 밖의 대상을 외곽선 위의 테두리 원과 바깥쪽 화살표로 표시할지 여부
        [FormerlySerializedAs("clampBossToEdge")]
        [SerializeField] private bool showBossOffscreen = true;
        [SerializeField] private bool showObjectsOffscreen;
        [SerializeField, Min(0f)] private float edgeMarkerSize = 16f;
        [SerializeField, Min(0f)] private float edgeArrowSize = 9f;
        // 외곽선(또는 점) 바깥에서 화살표까지 띄우는 간격
        [SerializeField, Min(0f)] private float edgeArrowGap = 2f;

        [Header("Ground")]
        // 미니맵 바닥에 그릴 레이어, 여기에 없는 레이어(Wall 등)는 배경색만 보인다.
        [SerializeField] private LayerMask groundLayers;
        // 바닥 위에 배경색으로 덮어서 비어 보이게 할 레이어
        [SerializeField] private LayerMask wallLayers;
        // 빌드에 포함되도록 참조, 비워두면 이름으로 찾는다.
        [SerializeField] private Shader wallShader;
        [SerializeField, Min(32)] private int textureSize = 256;
        // 미니맵 카메라가 플레이어 위로 떠 있는 높이
        [SerializeField, Min(1f)] private float cameraHeight = 50f;
        // 플레이어 아래로 어디까지 그릴지
        [SerializeField, Min(1f)] private float cameraDepthBelow = 50f;

        [Header("Dot Size")]
        [SerializeField, Min(0f)] private float playerDotSize = 20f;
        // 플레이어 점의 뾰족한 끝이 중심에서 떨어진 거리 (점 반지름 대비 배율)
        [SerializeField, Range(1f, 3f)] private float playerTipLength = 1.7f;
        [SerializeField, Min(0f)] private float bossDotSize = 14f;
        [SerializeField, Min(0f)] private float objectDotSize = 8f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color groundColor = Color.white;
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private Color bossColor = Color.red;
        [SerializeField] private Color objectColor = Color.cyan;

        [Header("Outline")]
        [SerializeField] private Color outlineColor = Color.white;
        // 미니맵 테두리 안쪽으로 그려지는 외곽선 두께
        [SerializeField, Min(0f)] private float outlineWidth = 4f;

        [Header("Marker Style")]
        // 모든 마커(점, 화살표)에 두르는 테두리
        [SerializeField] private Color markerBorderColor = Color.white;
        [SerializeField, Min(0f)] private float markerBorderWidth = 2f;
        // 바닥 색과 상관없이 눈에 띄도록 마커와 외곽선 바깥에 까는 부드러운 그림자
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
        [SerializeField, Min(0f)] private float shadowSize = 3f;
        // 가장자리를 부드럽게 하는 폭(화면 픽셀), 계단 현상 없이 벡터 이미지처럼 보이게 한다.
        [SerializeField, Range(0f, 3f)] private float antiAliasPixels = 1f;

        private readonly VectorShape shape = new();
        private readonly List<EdgeMarker> edgeMarkers = new();

        // 외곽선 위에 나중에 그릴 범위 밖 대상
        private readonly struct EdgeMarker
        {
            public readonly Vector2 Direction;
            public readonly Color Color;

            public EdgeMarker(Vector2 direction, Color color)
            {
                Direction = direction;
                Color = color;
            }
        }

        private float antiAlias;

        private const string WallShaderName = "Hidden/TeamOdd/MinimapWall";
        private static readonly int wallColorId = Shader.PropertyToID("_Color");

        private Camera mapCamera;
        private UniversalAdditionalCameraData mapCameraData;
        private RenderTexture mapTexture;
        private Material wallMaterial;
        private MinimapWallPass wallPass;

        private float mapYaw;
        private Vector2 mapCenter;
        private float mapRadius;
        private float mapScale;
        private Vector3 mapOrigin;
        private Quaternion toMapRotation;

        protected override void Start()
        {
            base.Start();
            if (Application.isPlaying == false) return;

            if (player == null) player = FindAnyObjectByType<Player.Player>();
            if (boss == null) boss = FindAnyObjectByType<BossMonster>();
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
            if (groundLayers == 0) groundLayers = LayerMask.GetMask("Ground");
            if (wallLayers == 0) wallLayers = LayerMask.GetMask("Wall");
            if (wallShader == null) wallShader = Shader.Find(WallShaderName);

            CreateMapCamera();
            CreateWallPass();
            if (ground == null) ground = CreateGround();
            ground.Texture = mapTexture;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RenderPipelineManager.beginCameraRendering -= EnqueueWallPass;
            if (wallMaterial != null) Destroy(wallMaterial);
            if (mapCamera != null) Destroy(mapCamera.gameObject);
            if (mapTexture != null)
            {
                mapTexture.Release();
                Destroy(mapTexture);
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying == false || player == null) return;

            mapYaw = viewCamera != null ? viewCamera.eulerAngles.y : player.transform.eulerAngles.y;
            UpdateMapCamera();
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
            groundLayers = LayerMask.GetMask("Ground");
            wallLayers = LayerMask.GetMask("Wall");
            wallShader = Shader.Find(WallShaderName);
        }
#endif

        private void CreateMapCamera()
        {
            mapTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32) { name = "MinimapTexture" };

            mapCamera = new GameObject("MinimapCamera").AddComponent<Camera>();
            mapCamera.orthographic = true;
            // Wall도 컬링 결과에 있어야 패스에서 다시 그릴 수 있으므로 함께 포함
            mapCamera.cullingMask = groundLayers | wallLayers;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.targetTexture = mapTexture;
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;
            mapCamera.depth = -100f;

            mapCameraData = mapCamera.GetUniversalAdditionalCameraData();
            mapCameraData.renderPostProcessing = false;
            mapCameraData.renderShadows = false;
        }

        private void CreateWallPass()
        {
            if (wallShader == null)
            {
                Debug.LogWarning($"[Minimap] {WallShaderName} 셰이더를 찾지 못해 Wall이 바닥처럼 그려집니다.", this);
                return;
            }

            wallMaterial = new Material(wallShader) { name = "MinimapWall" };
            wallPass = new MinimapWallPass(wallMaterial);
            RenderPipelineManager.beginCameraRendering += EnqueueWallPass;
        }

        private void EnqueueWallPass(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != mapCamera) return;

            wallPass.WallLayers = wallLayers;
            mapCameraData.scriptableRenderer.EnqueuePass(wallPass);
        }

        // 위에서 내려다보면서 화면 위쪽이 카메라 Yaw 방향이 되도록 회전해서 점 좌표계와 맞춘다.
        private void UpdateMapCamera()
        {
            // Wall이 배경과 같은 색으로 보이도록 같은 값을 쓴다.
            mapCamera.backgroundColor = backgroundColor;
            if (wallMaterial != null) wallMaterial.SetColor(wallColorId, backgroundColor);

            mapCamera.orthographicSize = worldRadius;
            mapCamera.nearClipPlane = 0.1f;
            mapCamera.farClipPlane = cameraHeight + cameraDepthBelow;
            mapCamera.transform.SetPositionAndRotation(
                player.transform.position + Vector3.up * cameraHeight,
                Quaternion.Euler(90f, mapYaw, 0f));
        }

        // 바닥이 점 아래에 그려지도록 같은 크기의 형제 오브젝트로 바로 앞 순서에 둔다.
        private MinimapGround CreateGround()
        {
            RectTransform groundRect = new GameObject("MinimapGround", typeof(RectTransform)).GetComponent<RectTransform>();
            groundRect.SetParent(rectTransform.parent, false);
            groundRect.SetSiblingIndex(rectTransform.GetSiblingIndex());
            groundRect.anchorMin = rectTransform.anchorMin;
            groundRect.anchorMax = rectTransform.anchorMax;
            groundRect.pivot = rectTransform.pivot;
            groundRect.anchoredPosition = rectTransform.anchoredPosition;
            groundRect.sizeDelta = rectTransform.sizeDelta;
            groundRect.localRotation = rectTransform.localRotation;
            groundRect.localScale = rectTransform.localScale;

            MinimapGround createdGround = groundRect.gameObject.AddComponent<MinimapGround>();
            createdGround.raycastTarget = false;
            createdGround.color = groundColor;
            return createdGround;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            mapCenter = rect.center;
            mapRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);

            // 실행 중에는 미니맵 카메라가 배경을 칠하므로 에디터 배치 확인용으로만 그린다.
            if (Application.isPlaying == false)
            {
                shape.SetCircle(mapCenter, mapRadius, BackgroundSegments);
                shape.Fill(vh, mapCenter, 0f, antiAlias, backgroundColor * color);
            }

            edgeMarkers.Clear();
            AddMarkers(vh);
            AddMapOutline(vh);
            AddEdgeMarkers(vh);
        }

        private void AddMarkers(VertexHelper vh)
        {
            if (player == null) return;

            mapOrigin = player.transform.position;
            mapScale = mapRadius / worldRadius;
            toMapRotation = Quaternion.Euler(0f, -mapYaw, 0f);

            IReadOnlyList<PsychokinesisObject> objects = PsychokinesisObject.ActiveObjects;
            float objectRadius = objectDotSize * 0.5f;
            for (int index = 0; index < objects.Count; index++)
                AddMarker(vh, objects[index].transform.position, objectRadius, objectColor, showObjectsOffscreen);

            if (boss != null && boss.isActiveAndEnabled)
                AddMarker(vh, boss.transform.position, bossDotSize * 0.5f, bossColor, showBossOffscreen);

            // 맵은 카메라 기준으로 돌고, 플레이어 점의 끝은 플레이어 프리팹이 바라보는 방향을 가리킨다.
            float facingAngle = Mathf.DeltaAngle(mapYaw, player.transform.eulerAngles.y);
            shape.SetPointedCircle(mapCenter, playerDotSize * 0.5f, playerTipLength, facingAngle, CircleSegments);
            AddBadge(vh, mapCenter, playerColor);
        }

        // 범위 안이면 바로 점을 그리고, 범위 밖이면 외곽선을 그린 뒤에 표시하도록 모아둔다.
        private void AddMarker(VertexHelper vh, Vector3 worldPosition, float dotRadius, Color dotColor, bool showOffscreen)
        {
            if (TryWorldToMap(worldPosition, dotRadius + markerBorderWidth, out Vector2 point, out Vector2 direction))
            {
                shape.SetCircle(point, dotRadius, CircleSegments);
                AddBadge(vh, point, dotColor);
            }
            else if (showOffscreen)
                edgeMarkers.Add(new EdgeMarker(direction, dotColor));
        }

        // 플레이어 기준 XZ 오프셋을 카메라 Yaw만큼 되돌려서 미니맵 좌표로 변환 (Y축 회전이라 높이는 섞이지 않음)
        private bool TryWorldToMap(Vector3 worldPosition, float markerRadius, out Vector2 mapPosition, out Vector2 direction)
        {
            Vector3 offset = toMapRotation * (worldPosition - mapOrigin);
            Vector2 local = new Vector2(offset.x, offset.z) * mapScale;
            // 마커가 외곽선에 가려지지 않도록 외곽선 안쪽까지만 표시
            float limit = Mathf.Max(0f, mapRadius - outlineWidth - markerRadius);
            mapPosition = mapCenter + local;
            direction = local.sqrMagnitude > 0.0001f ? local.normalized : Vector2.up;

            return local.sqrMagnitude <= limit * limit;
        }

        // 외곽선 바깥에 그림자를 깔고, 안팎 가장자리를 부드럽게 처리한 고리를 그린다.
        private void AddMapOutline(VertexHelper vh)
        {
            float outer = mapRadius;
            float inner = Mathf.Max(0f, mapRadius - outlineWidth);

            AddRadialBand(vh, mapCenter, outer, outer + shadowSize, shadowColor, Color.clear);
            if (outer <= inner) return;

            Color clearOutline = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0f);
            float half = Mathf.Min(antiAlias * 0.5f, (outer - inner) * 0.5f);
            AddRadialBand(vh, mapCenter, inner - half, inner + half, clearOutline, outlineColor);
            AddRadialBand(vh, mapCenter, inner + half, outer - half, outlineColor, outlineColor);
            AddRadialBand(vh, mapCenter, outer - half, outer + half, outlineColor, clearOutline);
        }

        // 외곽선 위에 테두리 원을 올리고, 그 바깥에 대상 방향 화살표를 붙인다.
        private void AddEdgeMarkers(VertexHelper vh)
        {
            float edgeRadius = mapRadius - outlineWidth * 0.5f;
            float markerRadius = edgeMarkerSize * 0.5f;

            for (int index = 0; index < edgeMarkers.Count; index++)
            {
                EdgeMarker marker = edgeMarkers[index];
                Vector2 point = mapCenter + marker.Direction * edgeRadius;

                shape.SetCircle(point, markerRadius, CircleSegments);
                AddBadge(vh, point, marker.Color);

                if (edgeArrowSize <= 0f) continue;

                float arrowBase = Mathf.Max(mapRadius, edgeRadius + markerRadius + markerBorderWidth) + edgeArrowGap;
                Vector2 baseCenter = mapCenter + marker.Direction * (arrowBase + markerBorderWidth);
                Vector2 side = new Vector2(marker.Direction.y, -marker.Direction.x) * (edgeArrowSize * 0.5f);
                Vector2 tip = baseCenter + marker.Direction * edgeArrowSize;

                shape.SetTriangle(tip, baseCenter + side, baseCenter - side);
                AddBadge(vh, (tip + baseCenter * 2f) / 3f, marker.Color);
            }
        }

        // 현재 shape 도형을 그림자 → 테두리 → 채움 순으로 겹쳐 그린다.
        private void AddBadge(VertexHelper vh, Vector2 center, Color fillColor)
        {
            if (shadowSize > 0f)
                shape.Fill(vh, center, markerBorderWidth + shadowSize * 0.5f, shadowSize, shadowColor * color);
            if (markerBorderWidth > 0f)
                shape.Fill(vh, center, markerBorderWidth, antiAlias, markerBorderColor * color);
            shape.Fill(vh, center, 0f, antiAlias, fillColor * color);
        }

        // 중심 기준 반지름 from~to 사이의 고리, 안쪽과 바깥쪽 색을 보간한다.
        private void AddRadialBand(VertexHelper vh, Vector2 center, float fromRadius, float toRadius, Color fromColor,
            Color toColor)
        {
            fromRadius = Mathf.Max(0f, fromRadius);
            if (toRadius <= fromRadius) return;

            Color32 innerColor = fromColor * color;
            Color32 outerColor = toColor * color;
            int startIndex = vh.currentVertCount;

            for (int index = 0; index <= BackgroundSegments; index++)
            {
                Vector2 direction = VectorShape.AngleToDirection((float)index / BackgroundSegments * 360f);
                vh.AddVert(center + direction * fromRadius, innerColor, Vector2.zero);
                vh.AddVert(center + direction * toRadius, outerColor, Vector2.zero);
            }

            for (int index = 0; index < BackgroundSegments; index++)
            {
                int current = startIndex + index * 2;
                vh.AddTriangle(current, current + 2, current + 1);
                vh.AddTriangle(current + 1, current + 2, current + 3);
            }
        }
    }
}
