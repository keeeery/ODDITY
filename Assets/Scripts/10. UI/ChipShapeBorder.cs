using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // ChipShapeGraphic의 칸 테두리만 흰 텍스처로 그리는 자식 그래픽.
    // 칸을 칩 색 스프라이트로 칠하면 같은 그래픽에서 그린 테두리에도 스프라이트 색이 섞이므로, 테두리 색이 그대로 보이도록 따로 그린다.
    // ChipShapeGraphic이 실행 중에 만들고 저장하지 않는다. 칸 바깥에 테두리 두께만큼 액자를 두른다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChipShapeBorder : MaskableGraphic
    {
        private readonly List<Rect> cellRects = new();

        internal ChipShapeGraphic Owner { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Owner == null || Owner.DrawsBorderSeparately == false) return;

            Owner.GetCellRects(cellRects);
            // 부모와 같은 크기로 늘여 두었으므로 부모 좌표에서 이 그래픽 좌표로는 영역 시작점 차이만큼 옮기면 된다
            Vector2 offset = rectTransform.rect.min - Owner.rectTransform.rect.min;
            float width = Owner.BorderWidthUnits;
            Color32 borderColor = Owner.BorderColor * Owner.color;

            // 칸 가장자리가 화면 픽셀 경계에 맞춰져 있으므로 안티앨리어싱 없이 두께만큼 또렷하게 그린다
            foreach (Rect cell in cellRects)
                AddFrame(vh, new Rect(cell.position + offset, cell.size), width, 0f, borderColor);
        }

        // 칸 바깥에 width 두께의 액자를 한 덩어리로 두른다. 띠를 따로 그리면 이음매에서 반투명 가장자리가 겹치거나 비어 선이 고르지 않다.
        // 안쪽·바깥쪽 가장자리만 feather 폭으로 부드럽게 하고, 두께가 feather보다 얇으면 그만큼 옅게 그린다.
        private static void AddFrame(VertexHelper vh, Rect cell, float width, float feather, Color32 frameColor)
        {
            Color32 clear = frameColor;
            clear.a = 0;
            Color32 solid = frameColor;
            float half = feather * 0.5f;
            float innerSolid = half;
            float outerSolid = width - half;
            if (outerSolid < innerSolid)
            {
                innerSolid = outerSolid = width * 0.5f;
                solid.a = (byte)(frameColor.a * Mathf.Clamp01(width / Mathf.Max(feather, 0.0001f)));
            }

            // 칸 가장자리에서 바깥으로 잰 거리와 색, 안쪽부터 네 겹
            float[] distances = { -half, innerSolid, outerSolid, width + half };
            Color32[] colors = { clear, solid, solid, clear };

            int start = vh.currentVertCount;
            for (int ring = 0; ring < distances.Length; ring++)
            {
                float d = distances[ring];
                vh.AddVert(new Vector2(cell.xMin - d, cell.yMin - d), colors[ring], Vector4.zero);
                vh.AddVert(new Vector2(cell.xMin - d, cell.yMax + d), colors[ring], Vector4.zero);
                vh.AddVert(new Vector2(cell.xMax + d, cell.yMax + d), colors[ring], Vector4.zero);
                vh.AddVert(new Vector2(cell.xMax + d, cell.yMin - d), colors[ring], Vector4.zero);
            }

            // 이웃한 두 겹 사이를 네 변마다 사각형 두 개의 삼각형으로 잇는다
            for (int ring = 0; ring < distances.Length - 1; ring++)
            {
                int inner = start + ring * 4;
                int outer = inner + 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    int next = (corner + 1) % 4;
                    vh.AddTriangle(inner + corner, outer + corner, outer + next);
                    vh.AddTriangle(inner + corner, outer + next, inner + next);
                }
            }
        }
    }
}
