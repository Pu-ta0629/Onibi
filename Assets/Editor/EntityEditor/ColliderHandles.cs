using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum HandleKind
{
    Move,       // 位置
    Yaw,        // 向き
    Radius,     // 半径
    Length,     // 長さ
    Width,      // 幅
    Angle,      // 角度
    Inner,      // 手前の当たらない範囲
}

public readonly struct HandleInfo
{
    public readonly HandleKind Kind;
    public readonly Vector2 ScreenPosition;

    public HandleInfo(HandleKind kind, Vector2 screenPosition)
    {
        Kind = kind;
        ScreenPosition = screenPosition;
    }
}

public static class ColliderHandles
{
    private const float MinInnerOffset = 18f;   // 手前の当たらない範囲のハンドルを中心から離しておく最小距離
    private const float YawOffsetPixels = 28f;  // 向きのハンドルの、中心からの距離
    private const float MinSize = 0.05f;        // 大きさの最小値

    // 選んでいるコライダーの、ハンドルの一覧を作る
    // toScreen: 世界の座標(x は右、y は前。m)を、画面の位置に直す関数。scale: 1m あたりの画素数
    public static void Collect(ColliderDef def,Func<Vector2,Vector2> toScreen,float scale,List<HandleInfo> results)
    {
        results.Clear();

        GetFrame(def, out var origin, out var forward, out var right);
        var shape = def.Shape;
        var originScreen = toScreen(origin);
        var forwardScreen = ScreenDirection(forward);
        var rightScreen = ScreenDirection(right);

        results.Add(new HandleInfo(HandleKind.Move, originScreen));

        switch(shape.Kind)
        {
            case ColliderShapeKind.Circle:
                results.Add(new HandleInfo(HandleKind.Radius, toScreen(origin + right * shape.Radius)));
                results.Add(new HandleInfo(HandleKind.Inner, originScreen - rightScreen * Mathf.Max(shape.InnerRange * scale, MinInnerOffset)));
                break;
            case ColliderShapeKind.Capsule:
                results.Add(new HandleInfo(HandleKind.Radius, toScreen(origin + right * shape.Radius)));
                results.Add(new HandleInfo(HandleKind.Length, toScreen(origin + forward * (shape.Length * 0.5f + shape.Radius))));
                break;

            case ColliderShapeKind.Box:
                results.Add(new HandleInfo(HandleKind.Length, toScreen(origin + forward * (shape.Length * 0.5f))));
                results.Add(new HandleInfo(HandleKind.Width, toScreen(origin + right * (shape.Width * 0.5f))));
                break;

            case ColliderShapeKind.Sector:
                {
                    var half = shape.Angle * 0.5f * Mathf.Deg2Rad;
                    results.Add(new HandleInfo(HandleKind.Radius, toScreen(origin + forward * shape.Radius)));
                    results.Add(new HandleInfo(HandleKind.Angle, toScreen(origin + Toward(forward, right, half) * shape.Radius)));
                    results.Add(new HandleInfo(HandleKind.Inner, originScreen + forwardScreen * Mathf.Max(shape.InnerRange * scale, MinInnerOffset)));
                    break;
                }
        }

        //向きのハンドル
        if (shape.Kind != ColliderShapeKind.Circle)
            results.Add(new HandleInfo(HandleKind.Yaw, originScreen - forwardScreen * YawOffsetPixels));
    }

    //向きをドラッグしたときの、新しい値を書きこ

    public static void Drag(HandleKind kind,ColliderDef def,SerializedProperty colliderProperty,Vector2 pointerWorld,bool snap)
    {
        GetFrame(def, out var origin, out var forward, out var right);
        var shape = def.Shape;
        var relative = pointerWorld - origin;
        var shapeProperty = colliderProperty.FindPropertyRelative(ColliderDef.ShapeField);

        switch (kind)
        {
            case HandleKind.Move:
                {
                    // Entity は、原点で、前を向いているので、世界の座標が、そのまま、起点のずれになる
                    var offset = new Vector2(Round(Snap(pointerWorld.x, snap, 0.1f)), Round(Snap(pointerWorld.y, snap, 0.1f)));
                    colliderProperty.FindPropertyRelative(ColliderDef.OffsetField).vector2Value = offset;
                    break;
                }

            case HandleKind.Yaw:
                {
                    // ハンドルは、中心の後ろにあるので、ポインターの向きの反対が、前になる
                    // 前(上)を 0 として、右回りを正にした、度
                    var yaw = Mathf.Atan2(-relative.x, -relative.y) * Mathf.Rad2Deg;
                    colliderProperty.FindPropertyRelative(ColliderDef.YawOffsetField).floatValue = Round(Snap(yaw, snap, 15f));
                    break;
                }

            case HandleKind.Radius:
                {
                    // カプセルは、横方向の距離。円と扇形は、中心からの距離
                    var radius = shape.Kind == ColliderShapeKind.Capsule
                        ? Mathf.Abs(Vector2.Dot(relative, right))
                        : relative.magnitude;

                    radius = Mathf.Max(Snap(radius, snap, 0.1f), MinSize);
                    if (shape.InnerRange > 0f) radius = Mathf.Max(radius, shape.InnerRange + MinSize);   // 手前の当たらない範囲より、小さくしない
                    SetFloat(shapeProperty, ColliderShape.RadiusField, radius);
                    break;
                }

            case HandleKind.Length:
                {
                    var projection = Mathf.Abs(Vector2.Dot(relative, forward));
                    var length = shape.Kind == ColliderShapeKind.Capsule
                        ? Mathf.Max(0f, 2f * (projection - shape.Radius))   // カプセルは、両端の円の中心の間
                        : Mathf.Max(MinSize, 2f * projection);              // 四角は、全体の長さ
                    SetFloat(shapeProperty, ColliderShape.LengthField, Snap(length, snap, 0.1f));
                    break;
                }

            case HandleKind.Width:
                {
                    var width = Mathf.Max(MinSize, 2f * Mathf.Abs(Vector2.Dot(relative, right)));
                    SetFloat(shapeProperty, ColliderShape.WidthField, Snap(width, snap, 0.1f));
                    break;
                }

            case HandleKind.Angle:
                {
                    // 前と、ポインターの向きの間の角度の2倍が、全体の角度
                    var total = relative.sqrMagnitude < 1e-6f ? shape.Angle : Vector2.Angle(forward, relative) * 2f;
                    SetFloat(shapeProperty, ColliderShape.AngleField, Mathf.Clamp(Snap(total, snap, 5f), 1f, 360f));
                    break;
                }

            case HandleKind.Inner:
                {
                    // 扇形は、前方向の距離。円は、中心からの距離
                    var inner = shape.Kind == ColliderShapeKind.Sector
                        ? Mathf.Max(0f, Vector2.Dot(relative, forward))
                        : relative.magnitude;

                    inner = Snap(inner, snap, 0.1f);
                    if (inner < MinSize) inner = 0f;                        // 中心の近くまで戻すと、0 になる
                    inner = Mathf.Min(inner, shape.Radius - MinSize);       // 範囲より、小さくする
                    SetFloat(shapeProperty, ColliderShape.InnerRangeField, Mathf.Max(0f, inner));
                    break;
                }
        }

    }

    public static string Describe(HandleKind kind,ColliderDef def)
    {
        var shape = def.Shape;
        switch (kind)
        {
            case HandleKind.Move: return $"位置のずれ  右 {def.Offset.x:0.##} m、前 {def.Offset.y:0.##} m";
            case HandleKind.Yaw: return $"向きの角度のずれ  {def.YawOffset:0.#} 度";
            case HandleKind.Radius: return shape.Kind == ColliderShapeKind.Sector ? $"範囲  {shape.Radius:0.##} m" : $"半径  {shape.Radius:0.##} m";
            case HandleKind.Length: return $"長さ  {shape.Length:0.##} m";
            case HandleKind.Width: return $"幅  {shape.Width:0.##} m";
            case HandleKind.Angle: return $"角度  {shape.Angle:0.#} 度";
            case HandleKind.Inner: return $"手前の当たらない範囲  {shape.InnerRange:0.##} m";
            default: return string.Empty;
        }

    }

    // ---- 内部の計算 ----

    private static void GetFrame(ColliderDef def,out Vector2 origin,out Vector2 forward,out Vector2 right)
    {
        def.Resolve(Vector3.zero,0f, out var origin3, out var forward3);
        origin = new Vector2(origin3.x, origin3.z);
        forward = new Vector2(forward3.x, forward3.z);
        right = new Vector2(forward.y, -forward.x);
    }

    private static Vector2 Toward(Vector2 forward,Vector2 right,float radians)
    {
        return forward * Mathf.Cos(radians) + right * Mathf.Sin(radians);
    }

    private static Vector2 ScreenDirection(Vector2 world)
    {
        var direction = new Vector2(world.x, -world.y);
        return direction.sqrMagnitude < 1e-6f ? new Vector2(0f,-1f) : direction.normalized;
    }

    private static float Snap(float value,bool snap,float step)
    {
        return snap ? Mathf.Round(value / step) * step : value;
    }

    private static float Round(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }

    private static void SetFloat(SerializedProperty parent,string field,float value)
    {
        parent.FindPropertyRelative(field).floatValue = Round(value);
    }
}
