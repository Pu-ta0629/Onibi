
/// <summary>
/// HPを減らすための、小さな入口。ダメージが最終的に通る道は、ここ1本だけ
/// 直接ダメージも、火傷の定期ダメージも、起爆ダメージも、必ずここを通る
/// </summary>
public interface IDamageSink
{
    void TakeDamage(int damage);
}
/// <summary>
/// 命中効果(IHitEffect)が使う、受け手の窓口。効果は、受けてそのものではなく、この窓口だけを見る
/// 受け手は、自分が持っている窓口だけを公開する
/// </summary>
public interface IHitTarget : IDamageSink
{
    /// <summary>火傷の状態。火傷が聞かない相手はNull(付与も起爆も、何も起きない)</summary>
    BurnState Burn { get; }
}
