using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class HitColliderInstance : IDisposable
{
   private readonly HitWorld _world;
   private readonly HitCollider _collider;
   private readonly List<HitBody> _bodies = new();

    public HitColliderInstance(HitWorld world, object owner, Faction faction, HitCollider collider)
    {
        _world = world;
        _collider = collider;

        foreach (var circle in collider.Circles)
        {
            var body = new HitBody(owner, faction, circle.Radius);
            _bodies.Add(body);
            world.Add(body);
        }
    }

    public void SetTransform(Vector3 position,float yaw)
    {
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        for (var i = 0; i < _bodies.Count;i++)
        {
            var offset = _collider.Circles[i].Offset;
            _bodies[i].Position = position + rotation * new Vector3(offset.x,0f,offset.y);
        }
    }

    public bool Active
    {
        set
        {
            foreach (var body in _bodies) body.Active = value;
        }
    }

    public void Dispose()
    {
        foreach (var body in _bodies) _world.Remove(body);
        _bodies.Clear();
    }
}

public static class HitResults
{
    public static void DistinctOwners(List<HitBody> hits, List<object> oweners)
    {
        oweners.Clear();
        foreach (var hit in hits)
        {
            if (!oweners.Contains(hit.Owner)) oweners.Add(hit.Owner);
        }
    }
}
