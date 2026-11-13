using System;

/// <summary>
/// TypeManagerの火のState変更の通知を受けて、火とLightへ配る純粋なC#クラス
/// 「Stateが変わったら、見た目・挙動・光を合わせる」ことだけを担当する
/// </summary>
public class FireStateBinder : IDisposable
{
    private readonly TypeManager _typeManager;
    private readonly FireRegistry _registry;
    private readonly LightManager _lightManager;

    public FireStateBinder(TypeManager typeManager, FireRegistry registry, LightManager lightManager)
    {
        _typeManager = typeManager;
        _registry = registry;
        _lightManager = lightManager;

        _typeManager.OnFireStateChanged += HandleFireStateChanged;
        _typeManager.OnFollowTargetChanged += HandleFollowTargetChanged;
    }

    /// <summary>
    /// イベントの購読を解除する
    /// </summary>
    public void Dispose()
    {
        _typeManager.OnFireStateChanged -= HandleFireStateChanged;
        _typeManager.OnFollowTargetChanged -= HandleFollowTargetChanged;
    }

    private void HandleFireStateChanged(FireColor color, FireState state)
    {
        if (_registry.TryGet(color, out FireController fire))
        {
            fire.ApplyState(state);
            _lightManager.SetBrightness(color, state.Brightness);
        }
        RefreshLit();
    }

    private void HandleFollowTargetChanged(FireColor color) => RefreshLit();

    // 選択中 かつ 追従先 の火だけ点灯する
    private void RefreshLit()
    {
        FireColor selected = _typeManager.SelectedColor;
        FireColor follow = _typeManager.FollowTarget;

        for (int i = 0; i < _registry.all.Count; i++)
        {
            FireColor color = _registry.all[i].Color;
            bool isLit = color == selected || color == follow;
            _lightManager.SetLit(color,isLit);
        }
    }
}
