
/// <summary>
/// 火傷付与。指定した色の火傷に、回数と威力を加える
/// 火傷が効かない空いて(Burnがnull)には、何も起きない
/// </summary>
public sealed class BurnApplyEffect : IHitEffect
{
    private readonly FireColor _color;
    private readonly int _count;
    private readonly int _potency;

    public BurnApplyEffect(FireColor color,int count,int potency)
    {
        _color = color;
        _count = count;
        _potency = potency;
    }

    public void Apply(IHitTarget target)
    {
        target.Burn?.Add(_color,_count, _potency);
    }
}
