using UnityEngine;

public enum ColliderShapeKind : byte
{
    Circle = 0,
    Capsule = 1,
    Box = 2,
    Sector = 3,
}

[System.Serializable]
public struct ColliderShape
{
    [Tooltip("形(Circle = 円/Capsule = カプセル/Box = 四角/Sector = 扇)")]
    [SerializeField] private ColliderShapeKind _kind;


    [Tooltip("半径")]
    [SerializeField] private float _radius;

    [Tooltip("長さ")]
    [SerializeField] private float _length;

    [Tooltip("幅")]
    [SerializeField] private float _width;

    [Tooltip("扇形の全体の角度(度)")]
    [SerializeField] private float _angle;

    [Tooltip("手前の当たらない範囲(円と扇形。起点からの距離)。0 なら、起点から当たる。遠くには当たるが、近くには当たらない形にできる")]
    [SerializeField] private float _innerRange;

    private ColliderShape(ColliderShapeKind kind, float radius, float length, float width, float angle, float innerRange)
    {
        _kind = kind;
        _radius = radius;
        _length = length;
        _width = width;
        _angle = angle;
        _innerRange = innerRange;
    }

    public static ColliderShape Circle(float radius, float innerRange = 0f)
      => new(ColliderShapeKind.Circle, radius, 0f, 0f, 0f, innerRange);

    public static ColliderShape Capsule(float radius, float length)
        => new(ColliderShapeKind.Capsule, radius, length, 0f, 0f, 0f);

    public static ColliderShape Box(float length, float width)
        => new(ColliderShapeKind.Box, 0f, length, width, 0f, 0f);

    public static ColliderShape Sector(float range, float angle, float innerRange = 0f)
        => new(ColliderShapeKind.Sector, range, 0f, 0f, angle, innerRange);

    public ColliderShapeKind Kind => _kind;
    public float Radius => _radius;
    public float Length => _length;
    public float Width => _width;
    public float Angle => _angle;
    public float InnerRange => _innerRange;

    // エディタから、SerializedObject で書き込むときのフィールド名
    public const string KindField = nameof(_kind);
    public const string RadiusField = nameof(_radius);
    public const string LengthField = nameof(_length);
    public const string WidthField = nameof(_width);
    public const string AngleField = nameof(_angle);
    public const string InnerRangeField = nameof(_innerRange);

    // 起点(位置のずれを足した中心)から、形の端までの、最大距離
    public float BoundingRadius
    {
        get
        {
            switch (_kind)
            {
                case ColliderShapeKind.Capsule: return _length * 0.5f + _radius;
                case ColliderShapeKind.Box: return Mathf.Sqrt(_length * _length + _width * _width) * 0.5f;
                default: return _radius;   // 円と扇形
            }
        }
    }
}
