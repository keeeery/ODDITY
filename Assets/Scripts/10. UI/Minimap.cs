using System.Collections.Generic;
using TeamOdd.Boss;
using TeamOdd.Psychokinesis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        // 범위 밖의 보스를 테두리에 붙여서 표시할지 여부
        [SerializeField] private bool clampBossToEdge = true;

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
        [SerializeField, Min(0f)] private float objectDotSize = 6f;

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

        private readonly List<Vector2> shapePoints = new();

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

            // 실행 중에는 미니맵 카메라가 배경을 칠하므로 에디터 배치 확인용으로만 그린다.
            if (Application.isPlaying == false)
                AddCircle(vh, mapCenter, mapRadius, backgroundColor, BackgroundSegments);

            AddMarkers(vh);
            AddRing(vh, mapCenter, mapRadius, outlineWidth, outlineColor);
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
            {
                if (TryWorldToMap(objects[index].transform.position, objectRadius, false, out Vector2 point))
                    AddCircle(vh, point, objectRadius, objectColor, CircleSegments);
            }

            float bossRadius = bossDotSize * 0.5f;
            if (boss != null && boss.isActiveAndEnabled &&
                TryWorldToMap(boss.transform.position, bossRadius, clampBossToEdge, out Vector2 bossPoint))
                AddCircle(vh, bossPoint, bossRadius, bossColor, CircleSegments);

            // 맵은 카메라 기준으로 돌고, 플레이어 점의 끝은 플레이어 프리팹이 바라보는 방향을 가리킨다.
            float facingAngle = Mathf.DeltaAngle(mapYaw, player.transform.eulerAngles.y);
            AddPointedCircle(vh, mapCenter, playerDotSize * 0.5f, playerTipLength, facingAngle, playerColor);
        }

        // 플레이어 기준 XZ 오프셋을 카메라 Yaw만큼 되돌려서 미니맵 좌표로 변환 (Y축 회전이라 높이는 섞이지 않음)
        private bool TryWorldToMap(Vector3 worldPosition, float dotRadius, bool clampToEdge, out Vector2 mapPosition)
        {
            Vector3 offset = toMapRotation * (worldPosition - mapOrigin);
            Vector2 local = new Vector2(offset.x, offset.z) * mapScale;
            // 점이 외곽선에 가려지지 않도록 외곽선 안쪽까지만 표시
            float limit = Mathf.Max(0f, mapRadius - outlineWidth - dotRadius);
            mapPosition = mapCenter + local;

            if (local.sqrMagnitude <= limit * limit) return true;
            if (clampToEdge == false) return false;

            mapPosition = mapCenter + local.normalized * limit;
            return true;
        }

        private void AddCircle(VertexHelper vh, Vector2 center, float radius, Color fillColor, int segments)
        {
            shapePoints.Clear();
            AddArc(center, radius, 0f, 360f, segments);
            AddShape(vh, center, fillColor);
        }

        // 원의 한쪽 끝을 뾰족하게 늘린 물방울 형태, 끝에서 원에 접하는 두 점 사이로 이어서 매끄럽게 만든다.
        private void AddPointedCircle(VertexHelper vh, Vector2 center, float radius, float tipLength, float directionAngle,
            Color fillColor)
        {
            if (radius <= 0f) return;

            float tangentAngle = Mathf.Acos(1f / Mathf.Max(1.001f, tipLength)) * Mathf.Rad2Deg;

            shapePoints.Clear();
            AddArc(center, radius, directionAngle + tangentAngle, directionAngle + 360f - tangentAngle, CircleSegments);
            shapePoints.Add(center + AngleToDirection(directionAngle) * (radius * tipLength));
            shapePoints.Add(shapePoints[0]);
            AddShape(vh, center, fillColor);
        }

        // 바깥 반지름에서 안쪽으로 width만큼의 고리
        private void AddRing(VertexHelper vh, Vector2 center, float outerRadius, float width, Color fillColor)
        {
            float innerRadius = Mathf.Max(0f, outerRadius - width);
            if (outerRadius <= innerRadius) return;

            Color32 vertexColor = fillColor * color;
            int startIndex = vh.currentVertCount;

            for (int index = 0; index <= BackgroundSegments; index++)
            {
                Vector2 direction = AngleToDirection((float)index / BackgroundSegments * 360f);
                vh.AddVert(center + direction * outerRadius, vertexColor, Vector2.zero);
                vh.AddVert(center + direction * innerRadius, vertexColor, Vector2.zero);
            }

            for (int index = 0; index < BackgroundSegments; index++)
            {
                int outer = startIndex + index * 2;
                vh.AddTriangle(outer, outer + 2, outer + 1);
                vh.AddTriangle(outer + 1, outer + 2, outer + 3);
            }
        }

        private void AddArc(Vector2 center, float radius, float startAngle, float endAngle, int segments)
        {
            for (int index = 0; index <= segments; index++)
                shapePoints.Add(center + AngleToDirection(Mathf.Lerp(startAngle, endAngle, (float)index / segments)) * radius);
        }

        // 위쪽(0도)에서 시계 방향으로 잰 각도
        private static Vector2 AngleToDirection(float angle)
        {
            float radian = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radian), Mathf.Cos(radian));
        }

        // 중심에서 shapePoints 외곽선으로 삼각형 팬을 만든다.
        private void AddShape(VertexHelper vh, Vector2 center, Color fillColor)
        {
            Color32 vertexColor = fillColor * color;
            int startIndex = vh.currentVertCount;
            vh.AddVert(center, vertexColor, Vector2.zero);

            for (int index = 0; index < shapePoints.Count; index++)
                vh.AddVert(shapePoints[index], vertexColor, Vector2.zero);

            for (int index = 0; index < shapePoints.Count - 1; index++)
                vh.AddTriangle(startIndex, startIndex + index + 1, startIndex + index + 2);
        }
    }
}
