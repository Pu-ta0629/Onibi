
/// <summary>
/// 直接ダメージ
/// </summary>
public sealed class DirectDamageEffect : IHitEffect
{
    private readonly int _damage;
    
    public DirectDamageEffect(int damage)
    {
        _damage = damage;
    }

    public void Apply(IHitTarget target)
    {
        if(_damage > 0)
        {
            target.TakeDamage(_damage);
        }
    }
}
