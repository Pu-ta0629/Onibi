using Unity.Cinemachine;
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
                if (shape.InnerRange > 0f) DrawCircle(origin, shape.InnerRange, color, duration);
                break;

            case HitShapeKind.Sector:
                DrawArc(origin, f, shape.Angle, shape.Range, color, duration);
                if (shape.InnerRange > 0f) DrawArc(origin, f, shape.Angle, shape.InnerRange, color, duration);
                // 扇形の両端の線
                var left = Quaternion.AngleAxis(-shape.Angle * 0.5f, Vector3.up) * f;
                var rightDir = Quaternion.AngleAxis(shape.Angle * 0.5f, Vector3.up) * f;
                Debug.DrawLine(origin + left * shape.InnerRange, origin + left * shape.Range, color, duration);
                Debug.DrawLine(origin + rightDir * shape.InnerRange, origin + rightDir * shape.Range, color, duration);
                break;

            case HitShapeKind.Box:
                var side = Vector3.Cross(Vector3.up, f) * (shape.Width * 0.5f);
                var start = f * shape.InnerRange;
                var end = f * shape.Range;
                Debug.DrawLine(origin + start - side, origin + start + side, color, duration);
                Debug.DrawLine(origin + start + side, origin + end + side, color, duration);
                Debug.DrawLine(origin + end + side, origin + end - side, color, duration);
                Debug.DrawLine(origin + end - side, origin + start - side, color, duration);
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

    public static void DrawHitCollier(HitCollider collider,Vector3 position,float yaw,float duration = 0f)
    {
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var circle in collider.Circles)
        {
            var center = position + rotation * new Vector3(circle.Offset.x, 0f, circle.Offset.y);
            DrawCircle(center, circle.Radius, ColliderColors.Hit, duration);

            var cross = Mathf.Min(circle.Radius, 0.2f);
            Debug.DrawLine(center - Vector3.right * cross, center + Vector3.right * cross, ColliderColors.Hit, duration);
            Debug.DrawLine(center - Vector3.forward * cross, center + Vector3.forward * cross, ColliderColors.Hit, duration);
        }
    }

    public static void DrawAttackCollider(AttackCollider attack, Vector3 position, float yaw, float duration = 0f)
    {
        attack.Resolve(position, yaw, out var origin, out var forward);
        Draw(attack.Shape,origin,forward,ColliderColors.Attack,duration);

        var tip = origin + forward * 0.6f;
        var back = -forward * 0.2f;
        var side = Vector3.Cross(Vector3.up, forward) * 0.12f;
        Debug.DrawLine(origin, tip, ColliderColors.Attack, duration);
        Debug.DrawLine(tip,tip + back + side, ColliderColors.Attack, duration);
        Debug.DrawLine(tip,tip + back - side, ColliderColors.Attack, duration);
    }

}