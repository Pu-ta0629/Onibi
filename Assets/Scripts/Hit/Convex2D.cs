using System;
using UnityEngine;

// 2D(XZ 平面)の、凸な図形。「ある向きに、最も遠い点」を答えられれば、重なりの判定ができる
public interface IConvex2D
{
   public Vector2 Support(Vector2 direction);
}

// 体の図形。重なりの判定のほかに、大まかな範囲と、ある点からの最大距離も答える
public interface IBodyShape : IConvex2D
{
   public Vector2 Center { get; }
   public float BoundingRadius { get; }              // 中心から、図形の端までの最大距離
   public float MaxDistanceFrom(Vector2 point);      // ある点から、図形の最も遠い点までの距離
}

public static class Geo2D
{
    public static Vector2 Normalize(Vector2 v)
    {
        var magnitude = v.magnitude;
        return magnitude < 1e-6f ? Vector2.zero : v / magnitude;
    }
}

// ---- 体の図形(位置が変わるので、同じ入れ物を使い回す) ----

public sealed class CircleBody : IBodyShape
{
    public Vector2 Position;
    public float Radius;

    public Vector2 Center => Position;
    public float BoundingRadius => Radius;

    public Vector2 Support(Vector2 d) => Position + Geo2D.Normalize(d) * Radius;
    public float MaxDistanceFrom(Vector2 p) => (Position - p).magnitude + Radius;
}

public sealed class CapsuleBody : IBodyShape
{
    public Vector2 A;       // 中心線の一方の端
    public Vector2 B;       // 中心線のもう一方の端
    public float Radius;

    public Vector2 Center => (A + B) * 0.5f;
    public float BoundingRadius => (B - A).magnitude * 0.5f + Radius;

    public Vector2 Support(Vector2 d)
    {
        var end = Vector2.Dot(d, A) >= Vector2.Dot(d, B) ? A : B;
        return end + Geo2D.Normalize(d) * Radius;
    }

    public float MaxDistanceFrom(Vector2 p) => Mathf.Max((A - p).magnitude, (B - p).magnitude) + Radius;
}

// 向きのある四角(AxisX は右、AxisY は前。どちらも単位ベクトル)
public sealed class BoxBody : IBodyShape
{
    public Vector2 Position;
    public Vector2 AxisX;
    public Vector2 AxisY;
    public float HalfX;     // 幅の半分
    public float HalfY;     // 長さの半分

    public Vector2 Center => Position;
    public float BoundingRadius => Mathf.Sqrt(HalfX * HalfX + HalfY * HalfY);

    public Vector2 Support(Vector2 d)
    {
        var x = Vector2.Dot(d, AxisX) >= 0f ? HalfX : -HalfX;
        var y = Vector2.Dot(d, AxisY) >= 0f ? HalfY : -HalfY;
        return Position + AxisX * x + AxisY * y;
    }

    public float MaxDistanceFrom(Vector2 p)
    {
        var max = 0f;
        for (var i = 0; i < 4; i++)
        {
            var corner = Position
                + AxisX * ((i & 1) == 0 ? HalfX : -HalfX)
                + AxisY * ((i & 2) == 0 ? HalfY : -HalfY);
            max = Mathf.Max(max, (corner - p).magnitude);
        }
        return max;
    }
}

// ---- 判定する側の図形(判定のたびに作るので、new しても負担のない構造体) ----

public readonly struct CircleQuery : IConvex2D
{
    private readonly Vector2 _center;
    private readonly float _radius;

    public CircleQuery(Vector2 center, float radius)
    {
        _center = center;
        _radius = radius;
    }

    public Vector2 Support(Vector2 d) => _center + Geo2D.Normalize(d) * _radius;
}

// 動く円の軌跡(弾)や、カプセル。2点を結ぶ線分に、半径を足した形
public readonly struct CapsuleQuery : IConvex2D
{
    private readonly Vector2 _a;
    private readonly Vector2 _b;
    private readonly float _radius;

    public CapsuleQuery(Vector2 a, Vector2 b, float radius)
    {
        _a = a;
        _b = b;
        _radius = radius;
    }

    public Vector2 Support(Vector2 d)
    {
        var end = Vector2.Dot(d, _a) >= Vector2.Dot(d, _b) ? _a : _b;
        return end + Geo2D.Normalize(d) * _radius;
    }
}

public readonly struct BoxQuery : IConvex2D
{
    private readonly Vector2 _center;
    private readonly Vector2 _axisX;
    private readonly Vector2 _axisY;
    private readonly float _halfX;
    private readonly float _halfY;

    public BoxQuery(Vector2 center, Vector2 axisX, Vector2 axisY, float halfX, float halfY)
    {
        _center = center;
        _axisX = axisX;
        _axisY = axisY;
        _halfX = halfX;
        _halfY = halfY;
    }

    public Vector2 Support(Vector2 d)
    {
        var x = Vector2.Dot(d, _axisX) >= 0f ? _halfX : -_halfX;
        var y = Vector2.Dot(d, _axisY) >= 0f ? _halfY : -_halfY;
        return _center + _axisX * x + _axisY * y;
    }
}

// 扇形(角度が 180 度以下の、凸なものだけ。広いものは、HitWorld が2つに分けて渡す)
public readonly struct SectorQuery : IConvex2D
{
    private readonly Vector2 _apex;
    private readonly Vector2 _forward;
    private readonly float _range;
    private readonly float _cosHalf;
    private readonly Vector2 _left;    // 弧の一方の端
    private readonly Vector2 _right;   // 弧のもう一方の端

    public SectorQuery(Vector2 apex, Vector2 forward, float range, float halfAngleRadians)
    {
        _apex = apex;
        _forward = forward;
        _range = range;

        var cos = Mathf.Cos(halfAngleRadians);
        var sin = Mathf.Sin(halfAngleRadians);
        _cosHalf = cos;
        _left = apex + new Vector2(forward.x * cos - forward.y * sin, forward.x * sin + forward.y * cos) * range;
        _right = apex + new Vector2(forward.x * cos + forward.y * sin, -forward.x * sin + forward.y * cos) * range;
    }

    public Vector2 Support(Vector2 d)
    {
        var best = _apex;
        var bestDot = Vector2.Dot(d, _apex);

        var left = Vector2.Dot(d, _left);
        if (left > bestDot)
        {
            best = _left;
            bestDot = left;
        }

        var right = Vector2.Dot(d, _right);
        if (right > bestDot)
        {
            best = _right;
            bestDot = right;
        }

        // 向きが、扇形の角度の中にあるときは、弧の上にも、最も遠い点がある
        var length = d.magnitude;
        if (length > 1e-6f && Vector2.Dot(d / length, _forward) >= _cosHalf)
        {
            var arc = _apex + d / length * _range;
            if (Vector2.Dot(d, arc) > bestDot) best = arc;
        }

        return best;
    }
}

// ---- 重なりの判定(GJK 法。接しているだけでも、重なっているとみなす) ----

public static class Gjk
{
    private const int MaxIterations = 32;

    public static bool Overlaps<TA>(in TA a, IConvex2D b) where TA : struct, IConvex2D
    {
        Span<Vector2> simplex = stackalloc Vector2[3];
        var count = 0;

        var point = Support(a, b, Vector2.right);
        simplex[count++] = point;
        var direction = -point;

        for (var i = 0; i < MaxIterations; i++)
        {
            if (direction.sqrMagnitude < 1e-12f) return true;   // 原点が、点の上にある

            point = Support(a, b, direction);
            if (Vector2.Dot(point, direction) < 0f) return false;   // これ以上、原点に近づけない

            simplex[count++] = point;
            if (Update(simplex, ref count, ref direction)) return true;
        }

        return true;
    }

    private static Vector2 Support<TA>(in TA a, IConvex2D b, Vector2 d) where TA : struct, IConvex2D
    {
        return a.Support(d) - b.Support(-d);
    }

    // 点の集まり(単体)を更新して、原点を含んだら true を返す。含まないときは、次に調べる向きを決める
    private static bool Update(Span<Vector2> s, ref int count, ref Vector2 direction)
    {
        if (count == 2)
        {
            var a = s[1];
            var b = s[0];
            var ab = b - a;
            var ao = -a;

            if (Vector2.Dot(ab, ao) > 0f)
            {
                var perpendicular = new Vector2(-ab.y, ab.x);
                direction = Vector2.Dot(perpendicular, ao) >= 0f ? perpendicular : -perpendicular;
            }
            else
            {
                s[0] = a;
                count = 1;
                direction = ao;
            }
            return false;
        }

        // 三角形
        {
            var a = s[2];
            var b = s[1];
            var c = s[0];
            var ab = b - a;
            var ac = c - a;
            var ao = -a;

            var abPerp = new Vector2(-ab.y, ab.x);
            if (Vector2.Dot(abPerp, ac) > 0f) abPerp = -abPerp;   // c から離れる向き
            if (Vector2.Dot(abPerp, ao) > 0f)
            {
                s[0] = b;
                s[1] = a;
                count = 2;
                direction = abPerp;
                return false;
            }

            var acPerp = new Vector2(-ac.y, ac.x);
            if (Vector2.Dot(acPerp, ab) > 0f) acPerp = -acPerp;   // b から離れる向き
            if (Vector2.Dot(acPerp, ao) > 0f)
            {
                s[0] = c;
                s[1] = a;
                count = 2;
                direction = acPerp;
                return false;
            }

            return true;   // 原点が、三角形の中にある
        }
    }
}
