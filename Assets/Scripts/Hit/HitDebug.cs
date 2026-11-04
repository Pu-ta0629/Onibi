using UnityEngine;

// コライダーを、線で表示する(Scene ビューに出る。Game ビューは、Gizmos をオンにすると出る)
// 色は、役割ごとに決まる(ColliderColors)
public static class HitDebug
{
    private const int Segments = 32;

    // コライダー1つを、役割の色で描く
    public static void DrawCollider(ColliderDef def, Vector3 position, float yaw, float duration = 0f)
    {
        def.Resolve(position, yaw, out var origin, out var forward);
        var color = ColliderColors.Of(def.Role);

        DrawShape(def.Shape, origin, forward, color, duration);
        DrawRoleMarker(def.Role, origin, forward, color, duration);
    }

    // 一覧のコライダーを、すべて描く(体は緑、攻撃はオレンジ系の赤、索敵は青)
    public static void DrawColliders(ColliderSet set, Vector3 position, float yaw, float duration = 0f)
    {
        foreach (var def in set.All)
        {
            if (def != null) DrawCollider(def, position, yaw, duration);
        }
    }

    // 形だけを描く
    public static void DrawShape(ColliderShape shape, Vector3 origin, Vector3 forward, Color color, float duration = 0f)
    {
        var f = new Vector3(forward.x, 0f, forward.z);
        f = f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;
        var right = Vector3.Cross(Vector3.up, f);

        switch (shape.Kind)
        {
            case ColliderShapeKind.Circle:
                DrawCircle(origin, shape.Radius, color, duration);
                if (shape.InnerRange > 0f) DrawCircle(origin, shape.InnerRange, color, duration);   // 手前の当たらない範囲
                break;

            case ColliderShapeKind.Capsule:
                var half = f * (shape.Length * 0.5f);
                var r = shape.Radius;
                DrawCircle(origin - half, r, color, duration);
                DrawCircle(origin + half, r, color, duration);
                Debug.DrawLine(origin - half + right * r, origin + half + right * r, color, duration);
                Debug.DrawLine(origin - half - right * r, origin + half - right * r, color, duration);
                break;

            case ColliderShapeKind.Box:
                var hl = f * (shape.Length * 0.5f);
                var hw = right * (shape.Width * 0.5f);
                Debug.DrawLine(origin - hl - hw, origin - hl + hw, color, duration);
                Debug.DrawLine(origin - hl + hw, origin + hl + hw, color, duration);
                Debug.DrawLine(origin + hl + hw, origin + hl - hw, color, duration);
                Debug.DrawLine(origin + hl - hw, origin - hl - hw, color, duration);
                break;

            case ColliderShapeKind.Sector:
                DrawArc(origin, f, shape.Angle, shape.Radius, color, duration);
                if (shape.InnerRange > 0f) DrawArc(origin, f, shape.Angle, shape.InnerRange, color, duration);

                // 両端の線(内側の半径から、外側の半径まで)
                var left = Quaternion.AngleAxis(-shape.Angle * 0.5f, Vector3.up) * f;
                var rightDir = Quaternion.AngleAxis(shape.Angle * 0.5f, Vector3.up) * f;
                Debug.DrawLine(origin + left * shape.InnerRange, origin + left * shape.Radius, color, duration);
                Debug.DrawLine(origin + rightDir * shape.InnerRange, origin + rightDir * shape.Radius, color, duration);
                break;
        }
    }

    // 役割の目印。形が同じでも、役割がすぐに分かるようにする
    private static void DrawRoleMarker(ColliderRole role, Vector3 origin, Vector3 forward, Color color, float duration)
    {
        var f = new Vector3(forward.x, 0f, forward.z);
        f = f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;
        var side = Vector3.Cross(Vector3.up, f);

        switch (role)
        {
            case ColliderRole.Hit:
                // 体: 中心に、十字
                Debug.DrawLine(origin - Vector3.right * 0.15f, origin + Vector3.right * 0.15f, color, duration);
                Debug.DrawLine(origin - Vector3.forward * 0.15f, origin + Vector3.forward * 0.15f, color, duration);
                break;

            case ColliderRole.Attack:
                // 攻撃: 向きを示す矢印
                var tip = origin + f * 0.6f;
                Debug.DrawLine(origin, tip, color, duration);
                Debug.DrawLine(tip, tip - f * 0.2f + side * 0.12f, color, duration);
                Debug.DrawLine(tip, tip - f * 0.2f - side * 0.12f, color, duration);
                break;

            case ColliderRole.Search:
                // 索敵: 中心に、小さな菱形
                var d = 0.15f;
                Debug.DrawLine(origin + f * d, origin + side * d, color, duration);
                Debug.DrawLine(origin + side * d, origin - f * d, color, duration);
                Debug.DrawLine(origin - f * d, origin - side * d, color, duration);
                Debug.DrawLine(origin - side * d, origin + f * d, color, duration);
                break;

                // 新しい役割を足したときは、ここに目印を足す(足さなくても、目印なしで描かれる)
        }
    }

    public static void DrawCircle(Vector3 center, float radius, Color color, float duration = 0f)
    {
        DrawArc(center, Vector3.forward, 360f, radius, color, duration);
    }

    // forward を中心に、totalAngle(度)の円弧を描く
    private static void DrawArc(Vector3 origin, Vector3 forward, float totalAngle, float radius, Color color, float duration)
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
