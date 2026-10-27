using System;
using System.Collections.Generic;

/// <summary>
/// 所持している火の色を管理する純粋なC#クラス（最大7色）
/// ラジアルメニューはここの中身を見て項目を作る
/// </summary>
public class FireInventory
{
    public event Action<TypeManager.FireColor> OnFireAdded;

    private readonly List<TypeManager.FireColor> _ownedColors = new List<TypeManager.FireColor>();

    public IReadOnlyList<TypeManager.FireColor> ownedColors => _ownedColors;

    #region Initialize
    /// <summary>
    /// 初期装備（赤・緑・青）を設定する。InitializeManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize()
    {
        _ownedColors.Clear();
        _ownedColors.Add(TypeManager.FireColor.Red);
        _ownedColors.Add(TypeManager.FireColor.Green);
        _ownedColors.Add(TypeManager.FireColor.Blue);
    }
    #endregion

    #region Public
    /// <summary>
    /// 火を所持しているか
    /// </summary>
    public bool Has(TypeManager.FireColor color)
    {
        return _ownedColors.Contains(color);
    }

    /// <summary>
    /// 火を獲得する。すでに所持している場合はfalseを返す
    /// </summary>
    public bool TryAdd(TypeManager.FireColor color)
    {
        if (color == TypeManager.FireColor.None || _ownedColors.Contains(color))
        {
            return false;
        }

        _ownedColors.Add(color);
        OnFireAdded?.Invoke(color);
        return true;
    }
    #endregion
}