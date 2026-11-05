using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LightTransport;
using static UnityEngine.EventSystems.EventTrigger;

[System.Serializable]
public class ColliderDef
{
    [Tooltip("名前(Key)。")]
    [SerializeField] private string _key = "collider";

    [Tooltip("役割")]
    [SerializeField] private ColliderRole _role;

    [SerializeField] private ColliderShape _shape = ColliderShape.Circle(0.5f);

    [Tooltip("起点の位置ずれ")]
    [SerializeField] private Vector2 _offset;

    [Tooltip("向きの角度のずれ")]
    [SerializeField] private float _yawOffset;

    [Tooltip("対象の陣営")]
    [SerializeField] private Faction _targets = Faction.None;

    public string Key => _key;
    public ColliderRole Role => _role;
    public ColliderShape Shape => _shape;
    public Vector2 Offset => _offset;
    public float YawOffset => _yawOffset;
    public Faction Targets => _targets;

    public const string KeyField = nameof(_key);
    public const string RoleField = nameof(_role);
    public const string ShapeField = nameof(_shape);
    public const string OffsetField = nameof(_offset);
    public const string YawOffsetField = nameof(_yawOffset);
    public const string TargetsField = nameof(_targets);

    public static ColliderDef Create(string key,ColliderRole role,ColliderShape shape,Vector2 offset,float yawOffset,Faction targets)
    {
        return new ColliderDef
        {
            _key = key,
            _role = role,
            _shape = shape,
            _offset = offset,
            _yawOffset = yawOffset,
            _targets = targets
        };
    }

    public void Resolve(Vector3 position, float yaw, out Vector3 origin, out Vector3 forward)
    {
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        origin = position + rotation * new Vector3(_offset.x, 0f, _offset.y);
        forward = Quaternion.Euler(0f, yaw + _yawOffset, 0f) * Vector3.forward;
    }

    public void Query(HitWorld world, Vector3 position, float yaw, List<HitBody> results)
    {
        Query(world, position, yaw, _targets, results);
    }

    public void Query(HitWorld world, Vector3 position, float yaw, Faction mask, List<HitBody> results)
    {
        Resolve(position, yaw, out var origin, out var forward);
        world.Query(_shape, origin, forward, mask, results);
    }
}

// Entity が持つ、コライダーの一覧。体も、攻撃も、索敵も、ここに並べる
[Serializable]
public class ColliderSet
{
    [SerializeField]
    private List<ColliderDef> _colliders = new()
    {
        ColliderDef.Create("hit", ColliderRole.Hit, ColliderShape.Circle(0.5f), Vector2.zero, 0f, Faction.None),
    };

    public IReadOnlyList<ColliderDef> All => _colliders;

    public const string CollidersField = nameof(_colliders);

    // 名前(Key)で探す
    public bool TryFind(string key, out ColliderDef def)
    {
        foreach (var collider in _colliders)
        {
            if (collider != null && collider.Key == key)
            {
                def = collider;
                return true;
            }
        }

        def = null;
        return false;
    }

    // ある役割のコライダーを、すべて集める
    public void Collect(ColliderRole role, List<ColliderDef> results)
    {
        results.Clear();
        foreach (var collider in _colliders)
        {
            if (collider != null && collider.Role == role) results.Add(collider);
        }
    }

    // キャラの中心から、体(Hit など)の端までの、最大距離
    public float BodyBoundingRadius
    {
        get
        {
            var max = 0f;
            foreach (var collider in _colliders)
            {
                if (collider == null || !ColliderRules.IsBody(collider.Role)) continue;
                max = Mathf.Max(max, collider.Offset.magnitude + collider.Shape.BoundingRadius);
            }
            return max;
        }
    }
}

