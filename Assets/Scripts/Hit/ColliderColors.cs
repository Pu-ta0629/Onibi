using UnityEngine;

public static class ColliderColors
{
    public static readonly Color Hit = new(0.2f, 0.9f, 0.4f, 1f);

    public static readonly Color Attack = new(1f, 0.35f, 0.15f, 1f);

    public static readonly Color Contact = new(1f, 0.9f, 0.1f, 1f);

    public const float FillAlpha = 0.25f;

    public static Color Fill(Color color) => new(color.r, color.g, color.b, FillAlpha);
}
