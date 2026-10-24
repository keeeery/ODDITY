using System;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 공격 범위의 모양, 피격 판정과 예고 표시(AttackTelegraph)와 Gizmo가 모두 이 값을 써서 보이는 범위와 맞는 범위를 일치시킴
    // 높이는 무시하고 XZ 평면에서 판정, 기준 Pose의 위치가 원/부채꼴의 중심이자 사각형의 시작점
    // 부채꼴과 사각형은 기준 Pose의 forward(수평 성분) 방향으로 펼쳐짐
    [Serializable]
    public struct TelegraphShape
    {
        public enum ShapeKind
        {
            Circle,
            Sector,
            Rectangle
        }

        public ShapeKind kind;
        [Tooltip("원/부채꼴의 반경(m)")]
        [Min(0f)] public float radius;
        [Tooltip("부채꼴의 전체 각도(도)")]
        [Range(0f, 360f)] public float angle;
        [Tooltip("사각형의 폭(m)")]
        [Min(0f)] public float width;
        [Tooltip("사각형의 길이(m), 기준 위치에서 forward 방향으로 뻗음")]
        [Min(0f)] public float length;

        public static TelegraphShape Circle(float radius) =>
            new TelegraphShape { kind = ShapeKind.Circle, radius = radius };

        public static TelegraphShape Sector(float radius, float angle) =>
            new TelegraphShape { kind = ShapeKind.Sector, radius = radius, angle = angle };

        public static TelegraphShape Rectangle(float width, float length) =>
            new TelegraphShape { kind = ShapeKind.Rectangle, width = width, length = length };

        public bool Contains(Pose origin, Vector3 point)
        {
            Vector3 offset = point - origin.position;
            offset.y = 0f;
            Vector3 forward = FlatForward(origin.rotation);

            switch (kind)
            {
                case ShapeKind.Circle:
                    return offset.sqrMagnitude <= radius * radius;

                case ShapeKind.Sector:
                    if (offset.sqrMagnitude > radius * radius) return false;
                    // 중심과 거의 겹치면 방향을 정할 수 없으므로 안쪽으로 취급
                    if (offset.sqrMagnitude < 0.0001f) return true;
                    return Vector3.Angle(forward, offset) <= angle * 0.5f;

                case ShapeKind.Rectangle:
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    float along = Vector3.Dot(offset, forward);
                    float side = Vector3.Dot(offset, right);
                    return along >= 0f && along <= length && Mathf.Abs(side) <= width * 0.5f;

                default:
                    return false;
            }
        }

        // 기준 회전의 수평 방향, 위나 아래를 보고 있어 수평 성분이 없으면 월드 forward 사용
        public static Vector3 FlatForward(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

#if UNITY_EDITOR
        public void DrawHandles(Pose origin)
        {
            Vector3 center = origin.position;
            Vector3 forward = FlatForward(origin.rotation);

            switch (kind)
            {
                case ShapeKind.Circle:
                    UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
                    break;

                case ShapeKind.Sector:
                    Vector3 from = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
                    UnityEditor.Handles.DrawWireArc(center, Vector3.up, from, angle, radius);
                    if (angle < 360f)
                    {
                        Vector3 to = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward;
                        UnityEditor.Handles.DrawLine(center, center + from * radius);
                        UnityEditor.Handles.DrawLine(center, center + to * radius);
                    }
                    break;

                case ShapeKind.Rectangle:
                    Vector3 halfRight = Vector3.Cross(Vector3.up, forward) * (width * 0.5f);
                    Vector3 end = center + forward * length;
                    UnityEditor.Handles.DrawPolyLine(center - halfRight, center + halfRight, end + halfRight, end - halfRight, center - halfRight);
                    break;
            }
        }
#endif
    }
}
