using System;
using UnityEngine;

/// <summary>
/// マウス操作を、火のStateの変更と、選択中の火の移動に変える純粋なC#クラス
///   ・左クリック ＝ 選択中の火の固定／固定解除
///   ・ミドルクリック ＝ 迷子の追従モードの切り替え
///   ・毎フレーム ＝ 選択中かつ固定されていない火を、マウス位置の地面へ向かわせる
/// Stateを変えるときは必ずTypeManager経由で行う（入力はInputManagerのイベントで受け取る）
/// </summary>
public class FireInputHandler : IDisposable
{
    private readonly TypeManager _typeManager;
    private readonly InputManager _inputManager;
    private readonly FireRegistry _registry;

    public FireInputHandler(TypeManager typeManager, InputManager inputManager, FireRegistry registry)
    {
        _typeManager = typeManager;
        _inputManager = inputManager;
        _registry = registry;

        _inputManager.OnLeftClick += HandleLeftClick;
        _inputManager.OnMiddleClick += HandleMiddleClick;
    }

    #region Public
    /// <summary>
    /// 毎フレーム呼ぶ。選択中かつ固定されていない火に、マウス位置の地面を目的地として渡す（火はNavMeshAgentでそこへ向かう）
    /// </summary>
    public void Tick()
    {
        FireColor color = _typeManager.SelectedColor;
        if (color == FireColor.None || !_registry.TryGet(color, out FireController fire))
        {
            return;
        }

        //ラジアルを開いている間は、その場で止める
        if (_typeManager.IsRadialOpen)
        {
            fire.StopMoving();
            return;
        }

        //固定中は、Stateが変わった時点で止まっているので何もしない
        if (_typeManager.GetFireState(color).IsFixed)
        {
            return;
        }

        if (_inputManager.TryGetGroundPoint(out Vector3 point))
        {
            fire.MoveTo(point);
        }
    }

    /// <summary>
    /// イベントの購読を解除する
    /// </summary>
    public void Dispose()
    {
        _inputManager.OnLeftClick -= HandleLeftClick;
        _inputManager.OnMiddleClick -= HandleMiddleClick;
    }
    #endregion

    #region Handler
    private void HandleLeftClick()
    {
        //ラジアルを開いている間のクリックは固定の切り替えに使わない
        if (_typeManager.IsRadialOpen)
        {
            return;
        }

        _typeManager.ToggleFixedSelectedFire();
    }

    private void HandleMiddleClick()
    {
        _typeManager.ToggleFollowMode();
    }
    #endregion
}
