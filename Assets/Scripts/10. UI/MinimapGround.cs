using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // 미니맵 카메라가 그린 RenderTexture를 원형으로 잘라서 보여주는 Graphic
    [RequireComponent(typeof(CanvasRenderer))]
    public class MinimapGround : MaskableGraphic
    {
        private const int CircleSegments = 72;

        private Texture texture;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public Texture Texture
        {
            get => texture;
            set
            {
                if (texture == value) return;
                texture = value;
                SetMaterialDirty();
            }
        }

        // 원 안의 점을 텍스처 전체(0~1)에 대응시켜 정사각형 RenderTexture를 원형으로 자른다.
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
