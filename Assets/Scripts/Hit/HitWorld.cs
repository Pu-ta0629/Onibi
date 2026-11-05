using System.Collections.Generic;
using UnityEngine;

// 体を登録しておいて、形(円、カプセル、四角、扇形、動く円)に当たる体を調べる。
// コライダーも物理演算も使わず、計算だけで判定する。判定は、XZ 平面だけで行う(高さは見ない)。
// 結果を入れるリストは、呼び出す側が持っておいて、使い回す(毎回 new しない)。
public sealed class HitWorld
{
    private readonly List<HitBody> _bodies = new();

    public void Add(HitBody body)
    {
        if (!_bodies.Contains(body)) _bodies.Add(body);
    }

    public void Remove(HitBody body) => _bodies.Remove(body);

    // ---- 形で調べる(コライダーの ColliderShape を、そのまま渡せる) ----

    public void Query(ColliderShape shape, Vector3 origin, Vector3 forward, Faction mask, List<HitBody> results)
    {
        switch (shape.Kind)
        {
            case ColliderShapeKind.Circle:
                QueryCircle(origin, shape.Radius, mask, results, shape.InnerRange);
                break;
            case ColliderShapeKind.Capsule:
                QueryCapsule(origin, forward, shape.Radius, shape.Length, mask, results);
                break;
            case ColliderShapeKind.Box:
                QueryBoxCentered(origin, forward, shape.Length, shape.Width, mask, results);
                break;
            case ColliderShapeKind.Sector:
                QuerySector(origin, forward, shape.Radius, shape.Angle, mask, results, shape.InnerRange);
                break;
            default:
                results.Clear();
                break;
        }
    }

    // 円: 索敵範囲、明るさ、ゴール到達、範囲攻撃などに使う
    // innerRadius: 手前の当たらない範囲(0 なら、中心から当たる。ドーナツ状にしたいときに指定する)
    public void QueryCircle(Vector3 center, float radius, Faction mask, List<HitBody> results, float innerRadius = 0f)
    {
        results.Clear();

        var c = new Vector2(center.x, center.z);
        var query = new CircleQuery(c, radius);

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if (!NearBy(body, c, radius)) continue;
            if (!Gjk.Overlaps(query, body.Shape)) continue;

            // 内側の円(当たらない範囲)の中に、体が、すっぽり入っているときは、当たらない
            if (innerRadius > 0f && body.Shape.MaxDistanceFrom(c) < innerRadius) continue;

            results.Add(body);
        }
    }

    // 扇形: 近接攻撃などに使う。angle は全体の角度(度)で、forward を中心に左右へ広がる
    // innerRange: 手前の当たらない範囲(遠くには当たるが、近くには当たらない形にできる)
    // (扇形の端と、手前の当たらない範囲が重なる付近は、少しだけ広めに当たる)
    public void QuerySector(Vector3 origin, Vector3 forward, float range, float angle, Faction mask, List<HitBody> results, float innerRange = 0f)
    {
        results.Clear();
        if (angle <= 0f || range <= 0f) return;

        // 360 度なら、円と同じ
        if (angle >= 360f)
        {
            QueryCircle(origin, range, mask, results, innerRange);
            return;
        }

        if (!TryGetDirection(forward, out var direction)) return;

        var apex = new Vector2(origin.x, origin.z);
        var half = angle * 0.5f * Mathf.Deg2Rad;

        // 扇形は、角度が 180 度以下のときだけ、凸な形になる。それより広いときは、2つの扇形に分けて判定する
        var split = half > Mathf.PI * 0.5f;
        SectorQuery first;
        SectorQuery second = default;

        if (split)
        {
            var quarter = half * 0.5f;
            first = new SectorQuery(apex, Rotate(direction, quarter), range, quarter);
            second = new SectorQuery(apex, Rotate(direction, -quarter), range, quarter);
        }
        else
        {
            first = new SectorQuery(apex, direction, range, half);
        }

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if (!NearBy(body, apex, range)) continue;

            var hit = Gjk.Overlaps(first, body.Shape) || (split && Gjk.Overlaps(second, body.Shape));
            if (!hit) continue;

            if (innerRange > 0f && body.Shape.MaxDistanceFrom(apex) < innerRange) continue;

            results.Add(body);
        }
    }

    // カプセル: 中心を center、向きを forward にした、細長い形(length は、両端の円の中心の間)
    public void QueryCapsule(Vector3 center, Vector3 forward, float radius, float length, Faction mask, List<HitBody> results)
    {
        results.Clear();
        if (radius <= 0f) return;
        if (!TryGetDirection(forward, out var direction)) return;

        var c = new Vector2(center.x, center.z);
        var half = direction * (Mathf.Max(0f, length) * 0.5f);
        var query = new CapsuleQuery(c - half, c + half, radius);
        var bounding = Mathf.Max(0f, length) * 0.5f + radius;

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if (!NearBy(body, c, bounding)) continue;
            if (!Gjk.Overlaps(query, body.Shape)) continue;

            results.Add(body);
        }
    }

    // 四角(中心基準): 中心を center、向きを forward にした、前後の長さ length、左右の幅 width の四角
    public void QueryBoxCentered(Vector3 center, Vector3 forward, float length, float width, Faction mask, List<HitBody> results)
    {
        results.Clear();
        if (width <= 0f || length <= 0f) return;
        if (!TryGetDirection(forward, out var direction)) return;

        QueryBoxCore(new Vector2(center.x, center.z), direction, length * 0.5f, width * 0.5f, mask, results);
    }

    // 四角(起点基準): 起点から前方へ、innerRange から length までの、幅 width の帯。ビームなどに使う
    public void QueryBox(Vector3 origin, Vector3 forward, float length, float width, Faction mask, List<HitBody> results, float innerRange = 0f)
    {
        results.Clear();
        if (width <= 0f || length <= innerRange) return;
        if (!TryGetDirection(forward, out var direction)) return;

        var start = new Vector2(origin.x, origin.z);
        var halfLength = (length - innerRange) * 0.5f;
        var center = start + direction * (innerRange + halfLength);

        QueryBoxCore(center, direction, halfLength, width * 0.5f, mask, results);
    }

    private void QueryBoxCore(Vector2 center, Vector2 direction, float halfLength, float halfWidth, Faction mask, List<HitBody> results)
    {
        var right = new Vector2(direction.y, -direction.x);
        var query = new BoxQuery(center, right, direction, halfWidth, halfLength);
        var bounding = Mathf.Sqrt(halfWidth * halfWidth + halfLength * halfLength);

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if (!NearBy(body, center, bounding)) continue;
            if (!Gjk.Overlaps(query, body.Shape)) continue;

            results.Add(body);
        }
    }

    // ---- 動く円(弾) ----

    // 弾が from から to へ動く間に、当たる体を、すべて集める(速い弾が、すり抜けない)
    public void QuerySweep(Vector3 from, Vector3 to, float radius, Faction mask, List<HitBody> results)
    {
        results.Clear();

        var a = new Vector2(from.x, from.z);
        var b = new Vector2(to.x, to.z);
        var query = new CapsuleQuery(a, b, radius);
        var center = (a + b) * 0.5f;
        var bounding = (b - a).magnitude * 0.5f + radius;

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if (!NearBy(body, center, bounding)) continue;
            if (!Gjk.Overlaps(query, body.Shape)) continue;

            results.Add(body);
        }
    }

    // 弾が最初に当たる体と、そのときの位置の割合 t(0 は from、1 は to)
    public bool SweepFirst(Vector3 from, Vector3 to, float radius, Faction mask, out HitBody hit, out float t)
    {
        hit = null;
        t = 0f;

        var a = new Vector2(from.x, from.z);
        var b = new Vector2(to.x, to.z);
        var query = new CapsuleQuery(a, b, radius);
        var center = (a + b) * 0.5f;
        var bounding = (b - a).magnitude * 0.5f + radius;

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if (!NearBy(body, center, bounding)) continue;
            if (!Gjk.Overlaps(query, body.Shape)) continue;

            var bodyT = FindContactTime(a, b, radius, body.Shape);
            if (hit == null || bodyT < t)
            {
                hit = body;
                t = bodyT;
            }
        }

        return hit != null;
    }

    // ---- 内部の計算 ----

    private static bool Match(HitBody body, Faction mask)
    {
        return body.Active && (body.Faction & mask) != Faction.None;
    }

    // 大まかな範囲で、遠いものを先に除く(中心と半径だけの、軽い判定)
    private static bool NearBy(HitBody body, Vector2 center, float radius)
    {
        var sum = radius + body.Shape.BoundingRadius;
        return (body.Shape.Center - center).sqrMagnitude <= sum * sum;
    }

    private static bool TryGetDirection(Vector3 forward, out Vector2 direction)
    {
        direction = new Vector2(forward.x, forward.z);
        if (direction.sqrMagnitude < 1e-6f) return false;

        direction.Normalize();
        return true;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // 弾の位置を、a から b へ少しずつ進めて、最初に体に触れる位置の割合を求める
    private static float FindContactTime(Vector2 a, Vector2 b, float radius, IBodyShape body)
    {
        var length = (b - a).magnitude;
        var steps = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(radius, 0.05f)), 1, 64);
        var previous = 0f;

        for (var i = 0; i <= steps; i++)
        {
            var t = (float)i / steps;
            if (!Gjk.Overlaps(new CircleQuery(Vector2.Lerp(a, b, t), radius), body))
            {
                previous = t;
                continue;
            }

            // 最初から重なっているとき
            if (i == 0) return 0f;

            // 前の位置と、この位置の間を、二分探索して、触れ始める位置を絞り込む
            var low = previous;
            var high = t;
            for (var k = 0; k < 8; k++)
            {
                var mid = (low + high) * 0.5f;
                if (Gjk.Overlaps(new CircleQuery(Vector2.Lerp(a, b, mid), radius), body)) high = mid;
                else low = mid;
            }
            return high;
        }

        return 1f;
    }
}
