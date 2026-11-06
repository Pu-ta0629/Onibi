using System;
using System.Collections.Generic;

/// <summary>
/// 所持している火の色を管理する純粋なC#クラス（最大7色）
/// ラジアルメニューはここの中身を見て項目を作る
/// </summary>
public class FireInventory
{
    public event Action<FireColor> OnFireAdded;

    private readonly List<FireColor> _ownedColors = new List<FireColor>();

    public IReadOnlyList<FireColor> ownedColors => _ownedColors;

    #region Initialize
    /// <summary>
    /// 初期装備（赤・緑・青）を設定する。InitializeManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize()
    {
        _ownedColors.Clear();
        _ownedColors.Add(FireColor.Red);
        _ownedColors.Add(FireColor.Green);
        _ownedColors.Add(FireColor.Blue);
    }
    #endregion

    #region Public
    /// <summary>
    /// 火を所持しているか
    /// </summary>
    public bool Has(FireColor color)
    {
        return _ownedColors.Contains(color);
    }

    /// <summary>
    /// 火を獲得する。すでに所持している場合はfalseを返す
    /// </summary>
    public bool TryAdd(FireColor color)
    {
        if (color == FireColor.None || _ownedColors.Contains(color))
        {
            return false;
        }

        _ownedColors.Add(color);
        OnFireAdded?.Invoke(color);
        return true;
    }
    #endregion
}