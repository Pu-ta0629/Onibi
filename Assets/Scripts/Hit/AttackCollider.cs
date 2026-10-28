using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AttackCollider
{
    [Tooltip("形(円、扇型、四角)と、範囲、手前の当たらない範囲")]
    [SerializeField] HitShape _shape = HitShape.Sector(2f, 90f);

    [Tooltip("攻撃の起点の位置のずれ(X = 右,Y = 前。キャラの向きを基準に)")]
    [SerializeField] Vector2 _offset;

    [Tooltip("攻撃の向きの角度のずれ")]
    [SerializeField] float _yawOffset;

    public HitShape Shape => _shape;
    public Vector2 Offset => _offset;
    public float YawOffset => _yawOffset;

    public const string ShapeField = nameof(_shape);
    public const string OffsetField = nameof(_offset);
    public const string YawOffsetField = nameof(_yawOffset);

    public void Resolve(Vector3 position,float yaw,out Vector3 origin,out Vector3 forward)
    {
        var rotation = Quaternion.Euler(0f,yaw,0f);
        origin = position + rotation * new Vector3(_offset.x, 0f, _offset.y);
        forward = Quaternion.Euler(0f,yaw + _yawOffset,0f) * Vector3.forward;
    }

    public void Query(HitWorld world,Vector3 position,float yaw,Faction mask,List<HitBody> results)
    {
        Resolve(position, yaw, out var origin, out var forward);
        world.Query(_shape, origin, forward, mask, results);
    }
}
