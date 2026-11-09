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
    }

    /// <summary>
    /// イベントの購読を解除する
    /// </summary>
    public void Dispose()
    {
        _typeManager.OnFireStateChanged -= HandleFireStateChanged;
    }

    private void HandleFireStateChanged(FireColor color, FireState state)
    {
        if (_registry.TryGet(color, out FireController fire))
        {
            fire.ApplyState(state);
            _lightManager.SetBrightness(color, state.Brightness);
        }
    }
}
