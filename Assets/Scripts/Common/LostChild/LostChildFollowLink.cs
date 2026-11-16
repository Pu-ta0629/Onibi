using System;

/// <summary>
/// 迷子へ「どの火を追うか」を渡す純粋なC#クラス
/// 追従先（TypeManagerのFollowTarget）の火の座標を、毎フレーム迷子のTickへ渡す
/// 明るい場所にいる間は追従先を渡さない（迷子は動かない）。追従先が切り替わったら、迷子の経路を捨てる
/// </summary>
public class LostChildFollowLink : IDisposable
{
    private readonly TypeManager _typeManager;
    private readonly FireRegistry _registry;
    private readonly LostChildController _lostChild;
    private readonly DarknessSensor _darkness;

    public LostChildFollowLink(TypeManager typeManager, FireRegistry registry, LostChildController lostChild, DarknessSensor darkness)
    {
        _typeManager = typeManager;
        _registry = registry;
        _lostChild = lostChild;
        _darkness = darkness;

        _typeManager.OnFollowTargetChanged += HandleFollowTargetChanged;
    }

    #region Public
    /// <summary>
    /// 毎フレーム呼ぶ。迷子へ追従先の座標を渡す
    /// </summary>
    public void Tick()
    {
        if (_darkness.IsBright)
        {
            return;
        }

        FireColor color = _typeManager.FollowTarget;
        if (color == FireColor.None)
        {
            return;
        }

        if (_registry.TryGet(color, out FireController fire))
        {
            _lostChild.Tick(fire.Position);
        }
    }

    /// <summary>
    /// イベントの購読を解除する
    /// </summary>
    public void Dispose()
    {
        _typeManager.OnFollowTargetChanged -= HandleFollowTargetChanged;
    }
    #endregion

    #region Handler
    private void HandleFollowTargetChanged(FireColor color)
    {
        _lostChild.ResetDestination();
    }
    #endregion
}
