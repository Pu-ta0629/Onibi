using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ラジアルメニューに表示する火の見た目（Inspectorで設定する）。設定がない火は、色の名前の文字だけで表示する
/// </summary>
[Serializable]
public class FireRadialEntry
{
    public FireColor Color;
    [Tooltip("TextMeshProモードで表示する文字。空なら色の名前")] public string Label;
    [Tooltip("Imageモードで表示するアイコン")] public Sprite Icon;
}

/// <summary>
/// RadialMenu（見た目と入力）と、所持している火・火の選択をつなぐ純粋なC#クラス
/// 所持している火をRadialMenuの項目として登録し、火を獲得したら項目を追加する
/// 項目が選ばれたら火の選択を切り替え、開閉の状態をFireManagerへ伝える
/// </summary>
public class FireRadialMenuBinder : IDisposable
{
    private readonly List<FireColor> _colors = new List<FireColor>();  //RadialMenuの項目の並び（番号が一致する）

    private RadialMenu _radialMenu;
    private TypeManager _typeManager;
    private FireInventory _fireInventory;
    private FireManager _fireManager;
    private FireRadialEntry[] _entries;
    private bool _isInitialized;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。RadialMenuのStartより前に呼んでもよい
    /// </summary>
    public void Initialize(
        RadialMenu radialMenu,
        TypeManager typeManager,
        FireInventory fireInventory,
        FireManager fireManager,
        FireRadialEntry[] entries)
    {
        if (radialMenu == null || typeManager == null || fireInventory == null || fireManager == null)
        {
            Debug.LogError("[FireRadialMenuBinder] Initialize : Reference is Null!!! ");
            return;
        }

        _radialMenu = radialMenu;
        _typeManager = typeManager;
        _fireInventory = fireInventory;
        _fireManager = fireManager;
        _entries = entries;

        //Menu項目は常に先頭（真上）に置く。火の項目はその後ろに並ぶ
        AddRadialItem(FireColor.None);

        foreach (FireColor color in _fireInventory.ownedColors)
        {
            AddRadialItem(color);
        }

        _fireInventory.OnFireAdded += HandleFireAdded;
        _radialMenu.OnOpenChanged += HandleOpenChanged;
        _isInitialized = true;

        //ステージ開始時に選択済みの火を、RadialMenuの中心表示にも反映する
        SyncActiveItem();
    }

    /// <summary>
    /// イベントの購読を解除する。InitializeManagerのOnDestroyから呼ぶ
    /// </summary>
    public void Dispose()
    {
        if (!_isInitialized)
        {
            return;
        }

        _fireInventory.OnFireAdded -= HandleFireAdded;
        if (_radialMenu != null)
        {
            _radialMenu.OnOpenChanged -= HandleOpenChanged;
        }

        _isInitialized = false;
    }
    #endregion

    #region Private
    //RadialMenuに項目を1つ追加する。選ばれたときの処理もここで渡す。FireColor.Noneは火ではなくMenu項目
    private void AddRadialItem(FireColor color)
    {
        bool isMenu = color == FireColor.None;
        if (!isMenu)
        {
            _colors.Add(color);
        }

        string label = isMenu ? "Menu" : color.ToString();
        Sprite icon = null;
        FireRadialEntry entry = FindEntry(color);
        if (entry != null)
        {
            icon = entry.Icon;
            if (!string.IsNullOrEmpty(entry.Label))
            {
                label = entry.Label;
            }
        }

        if (isMenu)
        {
            _radialMenu.AddItem(label, icon, HandleMenuSelected);
        }
        else
        {
            _radialMenu.AddItem(label, icon, () => HandleSelected(color));
        }
    }

    private FireRadialEntry FindEntry(FireColor color)
    {
        if (_entries == null)
        {
            return null;
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] != null && _entries[i].Color == color)
            {
                return _entries[i];
            }
        }

        return null;
    }

    //RadialMenuの中心に表示する項目を、TypeManagerの選択中の火に合わせる（選択なしなら-1）
    //先頭はMenu項目なので、火の番号に1足す
    private void SyncActiveItem()
    {
        int index = _colors.IndexOf(_typeManager.SelectedColor);
        _radialMenu.SetActiveItem(index < 0 ? -1 : index + 1);
    }
    #endregion

    #region Handler
    private void HandleSelected(FireColor color)
    {
        //選択の切り替え（同じ火をもう一度選ぶと解除）はTypeManagerの結果が正。見た目はその結果に合わせる
        _fireManager.ToggleSelectFire(color);
        SyncActiveItem();
    }

    private void HandleMenuSelected()
    {
        //Menu画面を開く依頼を出す。中心の表示は、選択中の火のままにする
        _fireManager.RequestMenu();
        SyncActiveItem();
    }

    private void HandleFireAdded(FireColor color)
    {
        AddRadialItem(color);
    }

    private void HandleOpenChanged(bool isOpen)
    {
        _fireManager.SetRadialOpen(isOpen);
    }
    #endregion
}