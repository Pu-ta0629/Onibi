using UnityEngine;

public enum HitShapeKind: byte
{
    Circle = 0,     //円
    Sector = 1,     //扇
    Box = 2,        //四角
}

[System.Serializable]
public class HitShape
{
    [Tooltip("形(Circle = 円 / Sector = 扇 / Box = 四角)")]
    [SerializeField] private HitShapeKind _kind;

    [Tooltip("円と扇は半径、四角は前方への長さ")]
    [SerializeField] private float _range;

    [Tooltip("扇型の全体角。扇の時のみ")]
    [SerializeField] private float _angle;

    [Tooltip("四角の幅。四角の時だけ使う")]
    [SerializeField] private float _width;

    public HitShape(HitShapeKind kind, float range, float angle=0f, float width=0f)
    {
        _kind = kind;
        _range = range;
        _angle = angle;
        _width = width;
    }

    public HitShapeKind Kind => _kind;
    public float Range => _range;
    public float Angle => _angle;
    public float Width => _width;

    public static HitShape Circle(float radius) => new(HitShapeKind.Circle, radius);
    public static HitShape Sector(float range,float angle) => new(HitShapeKind.Sector, range,angle);
    public static HitShape Box(float length,float width) => new(HitShapeKind.Box, length,0f,width);

}
