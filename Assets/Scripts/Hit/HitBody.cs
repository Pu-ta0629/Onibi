using UnityEngine;

// 判定される側の体(コライダー1つ分)。体の役割のコライダーごとに、HitBody を1つ作る
public sealed class HitBody
{
    private readonly ColliderDef _def;
    private readonly CircleBody _circle;
    private readonly CapsuleBody _capsule;
    private readonly BoxBody _box;

    private Vector3 _position;
    private float _yaw;

    // 円1つの体(簡易用。位置のずれなし)
    public HitBody(object owner, Faction faction, float radius)
        : this(owner, faction, ColliderDef.Create("hit", ColliderRole.Hit, ColliderShape.Circle(radius), Vector2.zero, 0f, Faction.None)) { }

    public HitBody(object owner, Faction faction, ColliderDef def)
    {
        Owner = owner;
        Faction = faction;
        _def = def;

        switch (def.Shape.Kind)
        {
            case ColliderShapeKind.Capsule:
                _capsule = new CapsuleBody();
                Shape = _capsule;
                break;

            case ColliderShapeKind.Box:
                _box = new BoxBody();
                Shape = _box;
                break;

            default:
                if (def.Shape.Kind == ColliderShapeKind.Sector)
                    Debug.LogError($"[Hit] 扇形は、体の形に使えません。円として扱います: {def.Key}");

                _circle = new CircleBody();
                Shape = _circle;
                break;
        }

        SetTransform(Vector3.zero, 0f);
    }

    public object Owner { get; }               // 持ち主(どのキャラに当たったか調べるために使う)
    public Faction Faction { get; }            // 陣営(1つだけ指定する)
    public bool Active { get; set; } = true;   // false の間は、判定の対象にならない
    public IBodyShape Shape { get; }           // 世界での図形(SetTransform のたびに更新される)

    public float Radius => Shape.BoundingRadius;   // 大まかな半径(簡易用)

    // 位置だけを変える(向きは、そのまま)
    public Vector3 Position
    {
        get => _position;
        set => SetTransform(value, _yaw);
    }

    // キャラの位置と向き(yaw: 度)に合わせて、世界での図形を更新する。毎フレーム、呼ぶ
    public void SetTransform(Vector3 position, float yaw)
    {
        _position = position;
        _yaw = yaw;

        _def.Resolve(position, yaw, out var origin, out var direction);
        var center = new Vector2(origin.x, origin.z);
        var forward = new Vector2(direction.x, direction.z);
        var right = new Vector2(forward.y, -forward.x);
        var shape = _def.Shape;

        switch (shape.Kind)
        {
            case ColliderShapeKind.Capsule:
                var half = forward * (shape.Length * 0.5f);
                _capsule.A = center - half;
                _capsule.B = center + half;
                _capsule.Radius = shape.Radius;
                break;

            case ColliderShapeKind.Box:
                _box.Position = center;
                _box.AxisX = right;
                _box.AxisY = forward;
                _box.HalfX = shape.Width * 0.5f;
                _box.HalfY = shape.Length * 0.5f;
                break;

            default:
                _circle.Position = center;
                _circle.Radius = shape.Radius;
                break;
        }
    }
}
