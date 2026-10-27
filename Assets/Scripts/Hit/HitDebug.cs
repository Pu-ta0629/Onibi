using UnityEngine;

// 当たりの形を、Debug.DrawLine の線で表示する(Scene ビューに出る。Game ビューは、Gizmos をオンにすると出る)
public static class HitDebug
{
    const int Segments = 32;

    public static void Draw(HitShape shape, Vector3 origin, Vector3 forward, Color color, float duration = 0f)
    {
        var f = new Vector3(forward.x, 0f, forward.z);
        f = f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;

        switch (shape.Kind)
        {
            case HitShapeKind.Circle:
                DrawCircle(origin, shape.Range, color, duration);
                break;

            case HitShapeKind.Sector:
                DrawArc(origin, f, shape.Angle, shape.Range, color, duration);
                // 扇形の両端の線
                Debug.DrawLine(origin, origin + Quaternion.AngleAxis(-shape.Angle * 0.5f, Vector3.up) * f * shape.Range, color, duration);
                Debug.DrawLine(origin, origin + Quaternion.AngleAxis(shape.Angle * 0.5f, Vector3.up) * f * shape.Range, color, duration);
                break;

            case HitShapeKind.Box:
                var right = Vector3.Cross(Vector3.up, f) * (shape.Width * 0.5f);
                var end = f * shape.Range;
                Debug.DrawLine(origin - right, origin + right, color, duration);
                Debug.DrawLine(origin + right, origin + right + end, color, duration);
                Debug.DrawLine(origin + right + end, origin - right + end, color, duration);
                Debug.DrawLine(origin - right + end, origin - right, color, duration);
                break;
        }
    }

    public static void DrawCircle(Vector3 center, float radius, Color color, float duration = 0f)
    {
        DrawArc(center, Vector3.forward, 360f, radius, color, duration);
    }

    // forward を中心に、totalAngle(度)の円弧を描く
    static void DrawArc(Vector3 origin, Vector3 forward, float totalAngle, float radius, Color color, float duration)
    {
        var start = -totalAngle * 0.5f;
        var previous = origin + Quaternion.AngleAxis(start, Vector3.up) * forward * radius;

        for (var i = 1; i <= Segments; i++)
        {
            var angle = start + totalAngle * i / Segments;
            var next = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
            Debug.DrawLine(previous, next, color, duration);
            previous = next;
        }
    }
}