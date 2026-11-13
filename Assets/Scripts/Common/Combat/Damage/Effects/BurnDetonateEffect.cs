using UnityEngine;

/// <summary>
/// 火傷起爆。火傷の回数を消費して、威力*消費量のダメージを、直接ダメージとして与える
/// 色は、特定の色か、全色。上限回数は、ありか、あり
/// 全色の場合は、色ごとに上限を適用し、色ごとの威力*消費回数の合計を、1回のダメージにする
/// 火傷を持たない相手には、ダメージ0で不発
/// </summary>
public sealed class BurnDetonateEffect : IHitEffect
{
    ///<summary>上限回数なし(持っている回数をすべて使う)</summary>
    public const int Unlimited = 1000;

    private readonly FireColor _color;
    private readonly int _maxCount;

    /// <param name="color">起爆する色。FireColor.Noneは全色</param>
    /// <param name="maxCount">消費する回数の上限(色ごと)。Unlimitedで上限無し</param>
    public BurnDetonateEffect(FireColor color,int maxCount)
    {
        _color = color;
        _maxCount = maxCount;
    }

    /// <summary>全色を対象にした起爆</summary>
    public static BurnDetonateEffect AllColors(int maxCount = Unlimited)
    {
        return new BurnDetonateEffect(FireColor.None, maxCount);
    }

    public void Apply(IHitTarget target)
    {
        BurnState burn = target.Burn;
        if(burn == null)
        {
            return;
        }

        int damage = burn.Detonate(_color, _maxCount);
        if(damage > 0)
        {
            target.TakeDamage(damage);
        }
    }
}
