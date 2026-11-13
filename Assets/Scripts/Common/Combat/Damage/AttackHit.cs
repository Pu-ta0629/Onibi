using System;

/// <summary>
/// 命中効果。１つの効果が、受けて窓口(IHitTarget)を使って、自分で処理する
/// 同じ命中情報を複数の相手へ使いまわすので、公かは状態を持たない
/// 効果の結果は、効果に書き込まず、受け手の中だけで扱う
/// </summary>
public interface IHitEffect
{
    void Apply(IHitTarget target);
}

/// <summary>
/// 攻撃が当たったときに、相手へ渡す命中情報。効果を、並べた順に１つずつ適用する
/// 例: "火傷を付けてから起爆する" のように、順番が変わる攻撃は、並べ方で表現
/// 読み取り専用のため、範囲攻撃で全員に同じものを渡してもよい
/// </summary>
public sealed class AttackHit
{
    private readonly IHitEffect[] _effects;

    ///<summary>誰が当てたか(倒した経緯や演出に使う。不要ならnull)</summary>
    public object Source { get; }

    public AttackHit(object source,params IHitEffect[] effects)
    {
        Source = source;
        _effects = effects != null ? (IHitEffect[])effects.Clone() : Array.Empty<IHitEffect>();
    }

    /// <summary>
    /// 直接ダメージだけの命中情報
    /// </summary>
    public static AttackHit Direct(int damage,object source = null)
    {
        return new AttackHit(source, new DirectDamageEffect(damage));
    }

    ///<summary>
    /// 効果を、並べた順に、相手へ適用する
    ///</summary>
    public void ApplyTo(IHitTarget target)
    {
        if(target == null)
        {
            return;
        }

        for(int i = 0; i < _effects.Length; i++)
        {
            _effects[i]?.Apply(target);
        }
    }
}
