using UnityEngine;

// 当たりの色を決める場所。ここだけを直せば、エディタとゲーム中の表示が、まとめて変わる
public static class ColliderColors
{
    // 当てられる側の体(Hit): 緑
    public static readonly Color Hit = new(0.2f, 0.9f, 0.4f, 1f);

    // 当てる側の攻撃(Attack): 赤みのあるオレンジ
    public static readonly Color Attack = new(1f, 0.35f, 0.15f, 1f);

    // 索敵(Search): 青
    public static readonly Color Search = new(0.3f, 0.6f, 1f, 1f);

    // 攻撃が、実際に当たっている体: 黄
    public static readonly Color Contact = new(1f, 0.9f, 0.1f, 1f);

    // 塗りつぶしの色の、薄さ
    public const float FillAlpha = 0.25f;

    public static Color Fill(Color color) => new(color.r, color.g, color.b, FillAlpha);

    // 役割から、色を引く。新しい役割を足したときは、ここに色を足す(足さなければ、灰色)
    public static Color Of(ColliderRole role)
    {
        switch (role)
        {
            case ColliderRole.Hit: return Hit;
            case ColliderRole.Attack: return Attack;
            case ColliderRole.Search: return Search;
            default: return new Color(0.7f, 0.7f, 0.7f, 1f);
        }
    }
}
