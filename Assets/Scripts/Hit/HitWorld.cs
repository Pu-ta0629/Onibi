using System.Collections.Generic;
using System.Data;
using Unity.VisualScripting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 体を登録しておき、形に当たる体を調べる
/// 結果リストは呼び出す側が持ち、使いまわす
/// </summary>
public class HitWorld
{
    private readonly List<HitBody> _bodies = new();

    public void Add(HitBody body)
    {
        if(!_bodies.Contains(body)) _bodies.Add(body);
    }

    public void Remove(HitBody body) => _bodies.Remove(body);

    //----形で調べる----
    #region QueryShape      

    public void Query(HitShape shape,Vector3 origin,Vector3 forward,Faction mask,List<HitBody> results)
    {
        switch(shape.Kind)
        {
            case HitShapeKind.Circle:
                QueryCircle(origin,shape.Range,mask,results);
                break;
            case HitShapeKind.Sector:
                QuerySector(origin,forward,shape.Range,shape.Angle,mask,results);
                break;
            case HitShapeKind.Box:
                QueryBox(origin,forward,shape.Range,shape.Width,mask,results);
                break;
        }
    }

    public void QueryCircle(Vector3 center, float radius, Faction mask, List<HitBody> results)
    {
        results.Clear();

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;

            var dx = body.Position.x - center.x;
            var dz = body.Position.z - center.z;
            var sum = radius + body.Radius;

            if(dx * dx + dz * dz < sum * sum) 
                results.Add(body);
        }
    }

    public void QuerySector(Vector3 origin,Vector3 forward,float range,float angle,Faction mask,List<HitBody> results)
    {
        results.Clear();
        if (!TryGetDirection(forward, out var direction)) return;

        var helfAngle = angle * 0.5f * Mathf.Deg2Rad;

        foreach(var body in _bodies)
        {
            if(!Match(body, mask)) continue;

            var to = new Vector2(body.Position.x - origin.x,body.Position.z - origin.z);
            var distance = to.magnitude;

            if (distance > range + body.Radius) continue;

            if(distance <= body.Radius)
            {
                results.Add(body);
                continue;
            }

            var between = Mathf.Acos(Mathf.Clamp(Vector2.Dot(direction, to / distance), -1f, 1f));
            var margin = Mathf.Asin(Mathf.Clamp01(body.Radius / distance));

            if (between <= helfAngle + margin) results.Add(body);
        }
    }

    public void QueryBox(Vector3 origin,Vector3 forward,float length,float width,Faction mask,List<HitBody> results)
    {
        results.Clear();
        if(!TryGetDirection(forward,out var direction)) return;

        var right = new Vector2(direction.y,-direction.x);
        var half = width * 0.5f;

        foreach (var body in _bodies)
        {
            if (!Match(body, mask)) continue;

            var to = new Vector2(body.Position.x - origin.x,body.Position.z - origin.z);
            var localZ = Vector2.Dot(to, direction);
            var localX = Vector2.Dot(to, right);

            var dx = localX - Mathf.Clamp(localX,-half,half);
            var dz = localZ - Mathf.Clamp(localZ,0f,length);

            if (dx * dx + dz * dz <= body.Radius * body.Radius) results.Add(body);
        }
    }

    public void QuerySweep(Vector3 from, Vector3 to, float radius, Faction mask, List<HitBody> results)
    {
        results.Clear();

        foreach(var body in _bodies)
        {
            if (!Match(body, mask)) continue;
            if(TrySweepHit(from,to,radius,body,out _)) results.Add(body);
        }
    }

    public bool SweepFirst(Vector3 from, Vector3 to, float radius, Faction mask, out HitBody hit,out float t)
    {
        hit = null;
        t = 0f;

        foreach(var body in _bodies)
        {
            if (Match(body, mask)) continue;
            if (!TrySweepHit(from, to, radius, body, out var bodyT)) continue;

            if(hit == null || bodyT <t)
            {
                hit =body;
                t = bodyT;
            }
        }
        return hit != null;
    }

    #endregion
    //---- 内部計算 ----
    #region logic
    private static bool Match(HitBody body, Faction mask)
    {
        return body.Active && (body.Faction & mask) != Faction.None;
    }

    private static bool TryGetDirection(Vector3 forward,out Vector2 direction)
    {
        direction = new Vector2(forward.x,forward.z);
        if(direction.sqrMagnitude < 1e-6f) return false;

        direction.Normalize();
        return true;
    }

    private static bool TrySweepHit(Vector3 from,Vector3 to,float radius,HitBody body,out float t)
    {
        t = 0f;

        var sx = to.x - from.x;
        var sz = to.z - from.z;
        var fx = from.x - body.Position.x;
        var fz = from.z - body.Position.z;
        var sum = radius + body.Radius;

        var c = fx * fx + fz * fz - sum * sum;
        if(c < 0f ) return true;

        var a = sx * sx + sz * sz;
        if(a < 0f ) return false;

        var b = 2f * (fx * sx + fz * sz);
        var discriminemt = b * b - 4f * a * c;
        if(discriminemt < 0f ) return false;

        t = (-b - Mathf.Sqrt(discriminemt)) / (2f * a);
        return t >= 0f && t <= 1f;
    }

    #endregion
}
