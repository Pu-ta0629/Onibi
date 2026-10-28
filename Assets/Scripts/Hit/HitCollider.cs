using System.Collections.Generic;
using Unity.Android.Gradle;
using UnityEngine;

[System.Serializable]
public struct HitCircle
{
    [Tooltip("体の中心からの位置のずれ(x = 右,y = 前)")]
    [SerializeField] private Vector2 _offset;

    [Tooltip("半径")]
    [SerializeField] private float _radius;

    public HitCircle(Vector2 offset,float radius)
    {
        _offset = offset;
        _radius = radius;
    }

    public Vector2 Offset => _offset;
    public float Radius => _radius;
}

[System.Serializable]
public class HitCollider
{
    [SerializeField] List<HitCircle> _circles = new() { new HitCircle(Vector2.zero, 0.5f) };

    public IReadOnlyList<HitCircle> Circles => _circles;

    public const string CirclesField = nameof(_circles);
}
