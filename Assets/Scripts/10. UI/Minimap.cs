using System.Collections.Generic;
using TeamOdd.Boss;
using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 플레이어를 중심으로 카메라 Yaw에 맞춰 회전하는 원형 미니맵
    // 바닥은 씬 시작 시 플레이어가 설 수 있는 범위를 물리로 한 번 구워서 단색 실루엣과 외곽선으로 그리고,
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
        [Tooltip("바닥과 장애물로 볼 레이어. 플레이어가 서는 바닥과 막는 벽·상자가 모두 들어가야 합니다.")]
        [SerializeField] private LayerMask walkableLayers;
        [Tooltip("월드 y가 이 높이 아래에 있는 바닥만 찾습니다.")]
        [SerializeField] private float topHeight = 3f;
        [Tooltip("월드 y가 이 높이보다 낮은 바닥은 무시합니다. 아래층이 표시되지 않게 합니다.")]
        [SerializeField] private float bottomHeight = -2f;
        [Tooltip("이동 가능 범위를 조사할 격자 간격(m)")]
        [SerializeField, Min(0.05f)] private float bakeCellSize = 0.25f;
        [Tooltip("맵이 커도 텍스처 한 변이 이 칸 수를 넘지 않도록 격자 간격을 늘립니다.")]
        [SerializeField, Min(16)] private int bakeMaxCellsPerSide = 1024;
        [Tooltip("켜면 플레이어 시작 위치에서 걸어서 이어진 곳만 표시합니다.")]
        [SerializeField] private bool reachableOnly = true;

        [Header("Dot Size")]
        [SerializeField, Min(0f)] private float playerDotSize = 20f;
        // 플레이어 점의 뾰족한 끝이 중심에서 떨어진 거리 (점 반지름 대비 배율)
        [SerializeField, Range(1f, 3f)] private float playerTipLength = 1.7f;
        [SerializeField, Min(0f)] private float bossDotSize = 14f;
        [SerializeField, Min(0f)] private float objectDotSize = 8f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.5f);
        // 지형은 세부 없이 바닥 실루엣만 단색으로 채우고 경계에 외곽선을 두른다.
        [SerializeField] private Color groundFillColor = new(0.396f, 0.389f, 0.374f, 0.8f);
        [SerializeField] private Color groundOutlineColor = new(0.9f, 0.87f, 0.82f, 1f);
        // 지형 외곽선 두께(굽기 격자 칸)
        [SerializeField, Range(0f, 4f)] private float groundOutlineWidth = 1.5f;
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

        // CharacterController가 없을 때 쓰는 플레이어 크기
        private const float DefaultPlayerRadius = 0.5f;
        private const float DefaultPlayerHeight = 1.8f;
        private const float DefaultStepOffset = 0.3f;

        private MinimapWalkableBaker.Result walkable;

        private readonly List<Vector2> objectPositions = new();
        private bool markerStateValid;
        private Vector2 lastPlayerPosition;
        private float lastMapYaw;
        private float lastFacingAngle;
        private bool lastBossVisible;
        private Vector2 lastBossPosition;
        private bool groundWasEnabled;
        private bool groundSuspended;
        private float playerFacingAngle;

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
            if (walkableLayers == 0) walkableLayers = LayerMask.GetMask("Ground", "Wall");

            if (ground == null) ground = CreateGround();
            BakeWalkable();
            UpdateGroundView();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (walkable != null) Destroy(walkable.Texture);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            markerStateValid = false;
            if (Application.isPlaying == false) return;

            if (groundSuspended && ground != null) ground.enabled = groundWasEnabled;
            groundSuspended = false;
            UpdateGroundView();
        }

        protected override void OnDisable()
        {
            if (Application.isPlaying && ground != null)
            {
                groundWasEnabled = ground.enabled;
                groundSuspended = true;
                ground.enabled = false;
            }
            base.OnDisable();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying == false || player == null) return;

            UpdateGroundView();
            float facingYaw = player.PlayerController != null
                ? player.PlayerController.MinimapFacingYaw : player.transform.eulerAngles.y;
            playerFacingAngle = Mathf.DeltaAngle(mapYaw, facingYaw);
            if (UpdateMarkerState()) SetVerticesDirty();
        }

        // 구운 마스크는 북쪽(+z)이 위인 채로 고정돼 있으므로, 바닥 셰이더가 플레이어 위치와 카메라 Yaw에 맞춰 돌려서 읽는다.
        private void UpdateGroundView()
        {
            if (player == null) return;

            mapYaw = viewCamera != null ? viewCamera.eulerAngles.y : player.transform.eulerAngles.y;
            if (ground == null || walkable == null) return;

            ground.SetStyle(backgroundColor, groundFillColor, groundOutlineColor, groundOutlineWidth);
            Vector3 position = player.transform.position;
            Vector2 center = new Vector2(
                (position.x - walkable.Origin.x) / walkable.Size.x,
                (position.z - walkable.Origin.y) / walkable.Size.y);
            Vector2 scale = new Vector2(worldRadius * 2f / walkable.Size.x, worldRadius * 2f / walkable.Size.y);
            ground.SetView(mapYaw, center, scale);
        }

        private void BakeWalkable()
        {
            CharacterController body = player != null ? player.GetComponent<CharacterController>() : null;
            float scale = body != null ? Mathf.Max(body.transform.lossyScale.x, body.transform.lossyScale.z) : 1f;
            float radius = body != null ? body.radius * scale : DefaultPlayerRadius;
            float height = body != null ? body.height * body.transform.lossyScale.y : DefaultPlayerHeight;
            float stepOffset = body != null ? body.stepOffset : DefaultStepOffset;

            // 시작 직후라 아직 물리에 반영되지 않은 배치가 있을 수 있다.
            Physics.SyncTransforms();
            MinimapWalkableBaker.Settings settings = new MinimapWalkableBaker.Settings(walkableLayers, topHeight, bottomHeight,
                bakeCellSize, bakeMaxCellsPerSide, radius, height, stepOffset, reachableOnly);
            Vector3 start = player != null ? player.transform.position : transform.position;
            walkable = MinimapWalkableBaker.Bake(settings, start);
            if (walkable == null)
            {
                Debug.LogWarning("[Minimap] 이동 가능 범위를 찾지 못해 바닥을 그리지 않습니다. walkableLayers와 높이 범위를 확인하세요.", this);
                ground.enabled = false;
                return;
            }

            ground.Texture = walkable.Texture;
        }

        private bool UpdateMarkerState()
        {
            Vector3 position = player.transform.position;
            Vector2 playerPosition = new Vector2(position.x, position.z);
            bool bossVisible = boss != null && boss.isActiveAndEnabled;
            Vector3 bossWorldPosition = bossVisible ? boss.transform.position : Vector3.zero;
            Vector2 bossPosition = new Vector2(bossWorldPosition.x, bossWorldPosition.z);
            IReadOnlyList<PsychokinesisObject> objects = PsychokinesisObject.ActiveObjects;
            bool changed = markerStateValid == false || lastPlayerPosition.Equals(playerPosition) == false ||
                lastMapYaw.Equals(mapYaw) == false || lastFacingAngle.Equals(playerFacingAngle) == false ||
                lastBossVisible != bossVisible || (bossVisible && lastBossPosition.Equals(bossPosition) == false) ||
                objectPositions.Count != objects.Count;

            for (int index = 0; index < objects.Count; index++)
            {
                Vector3 objectPosition = objects[index].transform.position;
                Vector2 horizontalPosition = new Vector2(objectPosition.x, objectPosition.z);
                if (index >= objectPositions.Count)
                {
                    objectPositions.Add(horizontalPosition);
                    continue;
                }
                if (objectPositions[index].Equals(horizontalPosition)) continue;

                objectPositions[index] = horizontalPosition;
                changed = true;
            }
            if (objectPositions.Count > objects.Count)
                objectPositions.RemoveRange(objects.Count, objectPositions.Count - objects.Count);

            markerStateValid = true;
            lastPlayerPosition = playerPosition;
            lastMapYaw = mapYaw;
            lastFacingAngle = playerFacingAngle;
            lastBossVisible = bossVisible;
            lastBossPosition = bossPosition;
            return changed;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
            walkableLayers = LayerMask.GetMask("Ground", "Wall");
        }
#endif

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
            createdGround.color = Color.white;
            return createdGround;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            mapCenter = rect.center;
            mapRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            antiAlias = VectorShape.PixelsToUnits(canvas, antiAliasPixels);

            // 실행 중에는 바닥 셰이더가 배경을 칠하므로 에디터 배치 확인용으로만 그린다.
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

            // 홀딩 자세를 위한 몸 회전은 제외하고 조준 방향을 표시한다.
            float facingAngle = Application.isPlaying ? playerFacingAngle
                : Mathf.DeltaAngle(mapYaw, player.transform.eulerAngles.y);
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
