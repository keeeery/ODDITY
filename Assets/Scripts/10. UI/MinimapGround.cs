using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 구워 둔 이동 가능 범위 마스크를 원형으로 잘라서, 바닥은 단색으로 채우고 경계에 외곽선을 그리는 Graphic
    [RequireComponent(typeof(CanvasRenderer))]
    public class MinimapGround : MaskableGraphic
    {
        private const int CircleSegments = 72;
        // Resources 아래 경로, 씬에서 참조하지 않아도 빌드에 포함된다.
        private const string ShaderPath = "Minimap/MinimapGround";

        private static readonly int backgroundColorId = Shader.PropertyToID("_BackgroundColor");
        private static readonly int fillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int outlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int maskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");
        private static readonly int viewId = Shader.PropertyToID("_View");
        private static readonly int viewScaleId = Shader.PropertyToID("_ViewScale");

        private Texture texture;
        private Material groundMaterial;

        private Color backgroundColor;
        private Color fillColor;
        private Color outlineColor;
        private float outlineWidth;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public Texture Texture
        {
            get => texture;
            set
            {
                if (texture == value) return;
                texture = value;
                ApplyStyle();
                SetMaterialDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (Application.isPlaying == false) return;

            Shader shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[MinimapGround] Resources/{ShaderPath} 셰이더를 찾지 못해 마스크가 그대로 보입니다.", this);
                return;
            }

            groundMaterial = new Material(shader) { name = "MinimapGround" };
            material = groundMaterial;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (groundMaterial != null) Destroy(groundMaterial);
        }

        // 바뀐 값이 있을 때만 머티리얼을 갱신한다. 마스킹용 복사본도 함께 다시 만들어지도록 SetMaterialDirty를 부른다.
        public void SetStyle(Color background, Color fill, Color outline, float width)
        {
            if (backgroundColor == background && fillColor == fill && outlineColor == outline && outlineWidth.Equals(width)) return;

            backgroundColor = background;
            fillColor = fill;
            outlineColor = outline;
            outlineWidth = width;
            ApplyStyle();
            SetMaterialDirty();
        }

        private void ApplyStyle()
        {
            if (groundMaterial == null) return;

            groundMaterial.SetColor(backgroundColorId, backgroundColor);
            groundMaterial.SetColor(fillColorId, fillColor);
            groundMaterial.SetColor(outlineColorId, outlineColor);
            groundMaterial.SetFloat(outlineWidthId, outlineWidth);
            if (texture != null)
                groundMaterial.SetVector(maskTexelSizeId, new Vector4(1f / texture.width, 1f / texture.height, 0f, 0f));
        }

        // 마스크는 북쪽이 위로 고정돼 있으므로 미니맵 Yaw(도)만큼 돌리고, 플레이어 위치(마스크 UV)를 중심으로
        // 미니맵 지름이 마스크에서 차지하는 비율(scale)만큼 잘라 샘플링한다.
        // 매 프레임 바뀌는 값이라 머티리얼을 다시 만들지 않고 실제로 그리는 머티리얼에 바로 넣는다.
        public void SetView(float yaw, Vector2 center, Vector2 scale)
        {
            if (groundMaterial == null) return;

            float radian = yaw * Mathf.Deg2Rad;
            Vector4 view = new Vector4(Mathf.Sin(radian), Mathf.Cos(radian), center.x, center.y);
            Vector4 viewScale = new Vector4(scale.x, scale.y, 0f, 0f);
            groundMaterial.SetVector(viewId, view);
            groundMaterial.SetVector(viewScaleId, viewScale);
            Material rendering = materialForRendering;
            if (rendering == groundMaterial) return;

            rendering.SetVector(viewId, view);
            rendering.SetVector(viewScaleId, viewScale);
        }

        // 원 안의 점을 0~1 UV로 넘기고, 실제 마스크 좌표는 셰이더가 SetView 값으로 변환한다.
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f) return;

            Color32 vertexColor = color;
            vh.AddVert(center, vertexColor, new Vector2(0.5f, 0.5f));

            for (int index = 0; index <= CircleSegments; index++)
            {
                float angle = (float)index / CircleSegments * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(center + direction * radius, vertexColor, new Vector2(0.5f, 0.5f) + direction * 0.5f);
            }

            for (int index = 0; index < CircleSegments; index++)
                vh.AddTriangle(0, index + 1, index + 2);
        }
    }
}
