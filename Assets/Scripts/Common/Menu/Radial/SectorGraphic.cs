using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 項目の形描画
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SectorGraphic : MaskableGraphic
{
    private const float _arcStepAngle = 3f;   // 円弧の滑らかさ(度)。小さいほど滑らか

    private float _innerRadius;
    private float _outerRadius;
    private float _startAngle;
    private float _endAngle;

    /// <summary>扇形の形を設定する。RadialMenuの生成時に呼ぶ</summary>
    public void Setup(float innerRadius, float outerRadius, float startAngle, float endAngle)
    {
        _innerRadius = innerRadius;
        _outerRadius = outerRadius;
        _startAngle = startAngle;
        _endAngle = endAngle;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float sweep = _endAngle - _startAngle;
        if (sweep <= 0f || _outerRadius <= _innerRadius) return;

        int steps = Mathf.Max(1, Mathf.CeilToInt(sweep / _arcStepAngle));

        for (int i = 0; i <= steps; i++)
        {
            float radian = (_startAngle + sweep * i / steps) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(radian), Mathf.Cos(radian));
            vh.AddVert(direction * _innerRadius, color, Vector2.zero);
            vh.AddVert(direction * _outerRadius, color, Vector2.zero);
        }

        for (int i = 0; i < steps; i++)
        {
            int baseIndex = i * 2;
            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 3);
            vh.AddTriangle(baseIndex, baseIndex + 3, baseIndex + 2);
        }
    }
}