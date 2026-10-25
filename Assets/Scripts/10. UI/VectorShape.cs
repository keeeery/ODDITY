using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOdd.UI
{
    // UI Graphic의 OnPopulateMesh에서 볼록 도형을 벡터 이미지처럼 깔끔하게 그리기 위한 도우미
    // 외곽선 점을 만든 뒤 Fill로 채우며, 가장자리를 feather 폭만큼 투명하게 풀어 계단 현상을 없앰
    public sealed class VectorShape
    {
        private const float DuplicateDistance = 0.001f;

        private static readonly float[] arcRadii = new float[4];
        private static readonly Color32[] arcColors = new Color32[4];

        private readonly List<Vector2> points = new();

        public void Clear() => points.Clear();

        // 바로 앞 점과 겹치는 점은 법선이 망가지므로 건너뜀
        public void Add(Vector2 point)
        {
            if (points.Count > 0 && (points[^1] - point).sqrMagnitude < DuplicateDistance * DuplicateDistance) return;
            points.Add(point);
        }

        public void SetCircle(Vector2 center, float radius, int segments)
        {
            points.Clear();
            for (int index = 0; index < segments; index++)
                Add(center + AngleToDirection((float)index / segments * 360f) * radius);
        }

        // 원의 한쪽 끝을 뾰족하게 늘린 물방울 형태, 끝에서 원에 접하는 두 점 사이로 이어서 매끄럽게 만듦
        public void SetPointedCircle(Vector2 center, float radius, float tipLength, float directionAngle, int segments)
        {
            float tangentAngle = Mathf.Acos(1f / Mathf.Max(1.001f, tipLength)) * Mathf.Rad2Deg;
            float startAngle = directionAngle + tangentAngle;
            float endAngle = directionAngle + 360f - tangentAngle;

            points.Clear();
            for (int index = 0; index <= segments; index++)
                Add(center + AngleToDirection(Mathf.Lerp(startAngle, endAngle, (float)index / segments)) * radius);
            Add(center + AngleToDirection(directionAngle) * (radius * tipLength));
        }

        public void SetTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            points.Clear();
            Add(a);
            Add(b);
            Add(c);
        }

        // 모서리를 radius만큼 둥글린 사각형, radius가 높이의 절반이면 알약 모양
        public void SetRoundedRect(Rect rect, float radius, int cornerSegments)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            points.Clear();
            AddCorner(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, cornerSegments);
            AddCorner(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 90f, cornerSegments);
            AddCorner(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, cornerSegments);
            AddCorner(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 270f, cornerSegments);

            if (points.Count > 1 && (points[^1] - points[0]).sqrMagnitude < DuplicateDistance * DuplicateDistance)
                points.RemoveAt(points.Count - 1);
        }

        private void AddCorner(Vector2 center, float radius, float startAngle, int segments)
        {
            for (int index = 0; index <= segments; index++)
                Add(center + AngleToDirection(startAngle + 90f * index / segments) * radius);
        }

        // 위쪽(0도)에서 시계 방향으로 잰 각도
        public static Vector2 AngleToDirection(float angle)
        {
            float radian = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radian), Mathf.Cos(radian));
        }

        // 캔버스 스케일을 반영해 화면 기준 픽셀 폭을 UI 단위로 변환
        public static float PixelsToUnits(Canvas canvas, float pixels)
        {
            float scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            return pixels / scaleFactor;
        }

        // 도형을 expand만큼 바깥으로 넓혀서 채우고, 가장자리 feather 폭은 투명해지도록 그림
        public void Fill(VertexHelper vh, Vector2 center, float expand, float feather, Color32 fillColor)
        {
            int count = points.Count;
            if (count < 3) return;

            Color32 clear = fillColor;
            clear.a = 0;
            float inner = expand - feather * 0.5f;
            float outer = expand + feather * 0.5f;
            float orientation = GetSignedArea() >= 0f ? 1f : -1f;

            int startIndex = vh.currentVertCount;
            vh.AddVert(center, fillColor, Vector2.zero);

            for (int index = 0; index < count; index++)
            {
                Vector2 point = points[index];
                Vector2 normal = GetMiterNormal(index, orientation);
                vh.AddVert(point + normal * inner, fillColor, Vector2.zero);
                vh.AddVert(point + normal * outer, clear, Vector2.zero);
            }

            for (int index = 0; index < count; index++)
            {
                int current = startIndex + 1 + index * 2;
                int next = startIndex + 1 + (index + 1) % count * 2;
                vh.AddTriangle(startIndex, current, next);
                vh.AddTriangle(current, current + 1, next + 1);
                vh.AddTriangle(current, next + 1, next);
            }
        }

        // 반지름 radius를 중심으로 width 두께의 호(고리 일부), 안팎 가장자리를 feather 폭만큼 투명하게 풀어 그린다.
        // 각도는 위쪽(0도)에서 시계 방향, 0~360이면 닫힌 고리가 된다.
        public static void FillArc(VertexHelper vh, Vector2 center, float radius, float width, float startAngle,
            float endAngle, int segmentsPerCircle, float feather, Color32 fillColor)
        {
            if (width <= 0f || Mathf.Approximately(startAngle, endAngle)) return;

            float half = width * 0.5f;
            float solidHalf = Mathf.Max(0f, half - feather * 0.5f);
            // 두께가 feather보다 얇으면 가운데 선 하나로 모으고 그만큼 흐리게 한다.
            if (half < feather * 0.5f) fillColor.a = (byte)(fillColor.a * (width / feather));

            Color32 clear = fillColor;
            clear.a = 0;
            // 매 프레임 그려지므로 배열을 새로 만들지 않고 재사용한다.
            float[] radii = arcRadii;
            Color32[] colors = arcColors;
            radii[0] = Mathf.Max(0f, radius - half - feather * 0.5f);
            radii[1] = radius - solidHalf;
            radii[2] = radius + solidHalf;
            radii[3] = radius + half + feather * 0.5f;
            colors[0] = clear;
            colors[1] = fillColor;
            colors[2] = fillColor;
            colors[3] = clear;

            int segments = Mathf.Max(2, Mathf.CeilToInt(segmentsPerCircle * Mathf.Abs(endAngle - startAngle) / 360f));
            int startIndex = vh.currentVertCount;

            for (int index = 0; index <= segments; index++)
            {
                Vector2 direction = AngleToDirection(Mathf.Lerp(startAngle, endAngle, (float)index / segments));
                for (int row = 0; row < radii.Length; row++)
                    vh.AddVert(center + direction * radii[row], colors[row], Vector2.zero);
            }

            for (int index = 0; index < segments; index++)
            {
                int current = startIndex + index * radii.Length;
                int next = current + radii.Length;
                for (int row = 0; row < radii.Length - 1; row++)
                {
                    vh.AddTriangle(current + row, next + row, current + row + 1);
                    vh.AddTriangle(current + row + 1, next + row, next + row + 1);
                }
            }
        }

        // 양쪽 변의 바깥 법선을 합친 방향, 모서리에서도 두께가 유지되도록 길이를 늘림(뾰족한 끝은 과하게 튀지 않게 제한)
        private Vector2 GetMiterNormal(int index, float orientation)
        {
            int count = points.Count;
            Vector2 previous = points[(index - 1 + count) % count];
            Vector2 point = points[index];
            Vector2 next = points[(index + 1) % count];

            Vector2 previousNormal = GetEdgeNormal(previous, point, orientation);
            Vector2 nextNormal = GetEdgeNormal(point, next, orientation);
            Vector2 normal = (previousNormal + nextNormal).normalized;
            return normal / Mathf.Max(0.5f, Vector2.Dot(normal, nextNormal));
        }

        // 반시계(양수 넓이) 도형 기준 오른쪽이 바깥
        private static Vector2 GetEdgeNormal(Vector2 from, Vector2 to, float orientation)
        {
            Vector2 direction = (to - from).normalized;
            return new Vector2(direction.y, -direction.x) * orientation;
        }

        private float GetSignedArea()
        {
            float area = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 a = points[index];
                Vector2 b = points[(index + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area;
        }
    }
}
