using System;
using System.Collections.Generic;
using UnityEngine;

#region Enum
/// <summary>火の色（ラジアルに登録できる最大７色）</summary>
public enum FireColor
{
    None = 0,
    Red,
    Green,
    Blue,
    Cyan,
    Yellow,
    Magenta,
    White
}

public enum FireBrightness
{
    Level1 = 0,
    Level2,
    Level3
}

/// <summary>迷子の追従先更新モード</summary>
public enum LostChildFollowMode
{
    None,       //対象無し
    KeepTarget, //対象固定
    UpdateTarget//随時更新
}
public enum EnemyType
{
    Kappa,
    Oni,
    Dog,
    Cat
}
#endregion
#region Struct
/// <summary> 火ごとの操作状態</summary>
public struct FireState
{
    public bool IsSelected;
    public bool IsFixed;
    public FireBrightness Brightness;
}

/// <summary>火のAIが使える行動の許可（false=重み０として扱う）</summary>
public struct FireAIPermission
{
    public bool CanDash;        //第１スキル（近距離攻撃）突進
    public bool CanMove;        //移動
    public bool CanSearch;      //索敵
    public bool CanLongAttack;  //第２スキル：遠距離攻撃
    public bool CanGuard;       //第２スキル：守護
    public bool CanCounterGuard;//第２スキル：反撃守護
    public bool CanExplosion;   //第２スキル：自爆
}
#endregion

/// <summary>
/// Enum・Structの管理と、（Enum|Struct）の取得・ChangeStateを一括で行う純粋なC#Class
/// 【持つもの】Enum・Structの定義、火ごとのState、選択中・最後に選んだ火、迷子の追従先と追従モード、ラジアルの開閉
/// 【やること】Stateの取得・変更（変わったときだけイベント通知）、Enum・Structの変換（AIPermission・Color・int→Enum）
/// 【やらないこと】Transform・Light・NavMeshなどUnityのオブジェクトを触る、入力を読む、移動・索敵・攻撃をする
///   （Stateを見て何をするかは、各スクリプトが通知を受けて行う）
/// 各スクリプトはStateに関する処理を必ず経由して行う。Stateを別の場所にコピーして持ち続けない
/// </summary>
public class TypeManager
{
    public event Action<FireColor, FireState> OnFireStateChanged;
    public event Action<FireColor> OnFollowTargetChanged;
    public event Action<LostChildFollowMode> OnFollowModeChanged;

    private readonly Dictionary<FireColor, FireState> _fireStates = new Dictionary<FireColor, FireState>();
    private FireColor _selectedColor;  //ラジアルで選択中の火
    private FireColor _lastSelectedColor;//最後に選択された火
    private FireColor _followTarget; //迷子の追従先
    private LostChildFollowMode _followMode;
    private bool _isRadialOpen;

    public FireColor SelectedColor => _selectedColor;
    public FireColor LastSelectedColor => _lastSelectedColor;
    public FireColor FollowTarget => _followTarget;
    public bool HasSelectedFire => _selectedColor != FireColor.None;
    public bool HasFollowTarget => _followTarget != FireColor.None;
    public LostChildFollowMode FollowMode => _followMode;
    public bool IsRadialOpen => _isRadialOpen;

    #region Initialize
    public void Initialize()
    {
        _fireStates.Clear();
        foreach (FireColor color in Enum.GetValues(typeof(FireColor)))
        {
            //Noneは「火を選択していない」状態なので、Stateを持たない
            if (color == FireColor.None) continue;
            _fireStates[color] = new FireState { Brightness = FireBrightness.Level2 };
        }

        //最初は何も選択していない（最初の火はFireManagerが SelectInitialFire で選択する）
        _selectedColor = FireColor.None;
        _lastSelectedColor = FireColor.None;
        _followTarget = FireColor.None;
        _followMode = LostChildFollowMode.UpdateTarget;
        _isRadialOpen = false;
    }
    #endregion

    #region FireState
    /// <summary>火のStateを取得する。Noneの場合は初期値を返す</summary>
    public FireState GetFireState(FireColor color)
    {
        if (!_fireStates.TryGetValue(color, out FireState state))
            Debug.LogWarning($"[TypeManager] GetFireState : {color} has no state!!");
        return state;
    }

    /// <summary>火のStateを変更する。値が変わった時だけOnFireStateChangedを通知する</summary>
    public void ChangeFireState(FireColor color, FireState newState)
    {
        //current = 現在のState
        //newState = 変更先State

        if (!_fireStates.TryGetValue(color, out FireState current))
        {
            Debug.LogWarning($"[TypeManager] ChangeFireState : {color} has no state!!");
            return;
        }

        //値が同じときは何も通知しない
        if (current.IsSelected == newState.IsSelected
            && current.IsFixed == newState.IsFixed
            && current.Brightness == newState.Brightness)
        {
            return;
        }


        _fireStates[color] = newState;
        Debug.Log($"[TypeManager] ChangeFireState : {color} \n " +
                  $"IsSelected = {newState.IsSelected}, \n" +
                  $"IsFixed = {newState.IsFixed}, \n" +
                  $"Brightness = {newState.Brightness}");
        OnFireStateChanged?.Invoke(color, newState);
    }
    /// <summary>火の明るさを指定した段階にする</summary>
    public void ChangeBrightness(FireColor color, FireBrightness brightness)
    {
        if (!_fireStates.TryGetValue(color, out FireState state)) return;
        state.Brightness = brightness;
        ChangeFireState(color, state);
    }

    /// <summary>火の明るさを1段階ずつ上げ下げする（step=+1で明るく、-1で暗く。両端で止まる）</summary>
    public void ShiftBrightness(FireColor color, int step)
    {
        if (!_fireStates.TryGetValue(color, out FireState state)) return;
        int next = Mathf.Clamp((int)state.Brightness + step, (int)FireBrightness.Level1, (int)FireBrightness.Level3);
        ChangeBrightness(color, (FireBrightness)next);
    }

    /// <summary>
    /// ラジアルで火を選ぶ。選択中の火をもう一度選ぶと選択解除（None）になる
    /// </summary>
    public void ToggleSelectFire(FireColor color)
    {
        if (color == FireColor.None)
        {
            Debug.LogWarning("[TypeManager] ToggleSelectFire : FireColor.None can not be selected!!");
            return;
        }

        if (_selectedColor == color) //現在選んでいると同じ項目だったらその色を解除
        {
            SetSelected(color, false);
            _selectedColor = FireColor.None;
            return;
        }

        if (_selectedColor != FireColor.None)
        {
            SetSelected(_selectedColor, false);
        }

        _selectedColor = color;
        _lastSelectedColor = color;
        SetSelected(color, true);
        RefreshFollowTarget();
    }

    /// <summary>選択中の火の固定／固定解除を切り替える。選択中の火がなければ何もせずfalseを返す</summary>
    public bool ToggleFixedSelectedFire()
    {
        if (_selectedColor == FireColor.None) return false;

        FireState state = _fireStates[_selectedColor];
        state.IsFixed = !state.IsFixed; //　固定/固定解除
        ChangeFireState(_selectedColor, state);
        return true;
    }

    /// <summary>ラジアルメニューの開閉状態を設定する</summary>
    public void SetRadialOpen(bool isOpen)
    {
        _isRadialOpen = isOpen;
    }
    private void SetSelected(FireColor color, bool isSelected)
    {
        FireState state = _fireStates[color];
        state.IsSelected = isSelected;
        ChangeFireState(color, state);
    }
    #endregion

    #region Follow
    /// <summary>追従先の更新モードを切り替える(ミドルクリック)</summary>
    public void ToggleFollowMode()
    {
        //現在のFollowModeを格納
        _followMode = _followMode == LostChildFollowMode.KeepTarget
            ? LostChildFollowMode.UpdateTarget
            : LostChildFollowMode.KeepTarget;

        Debug.Log($"[TypeManager] ToggleFollowMode : followMode = {_followMode}");
        OnFollowModeChanged?.Invoke(_followMode);

        if (_followMode == LostChildFollowMode.UpdateTarget)
        {
            ChangeFollowTarget(_lastSelectedColor);
        }
    }
    private void RefreshFollowTarget()
    {
        //追従先が未設定（None）なら最初に選んだ火が追従先になる。更新モードなら常に最後に選んだ火
        if (_followTarget == FireColor.None || _followMode == LostChildFollowMode.UpdateTarget)
        {
            ChangeFollowTarget(_lastSelectedColor);
        }
    }
    private void ChangeFollowTarget(FireColor next)
    {
        if (next == FireColor.None || _followTarget == next)
        {
            return;
        }
        _followTarget = next;
        Debug.Log($"[TypeManager] ChangeFollowTarget : followTarget = {next}");
        OnFollowTargetChanged?.Invoke(next);
    }
    #endregion

    #region Convert
    public static FireAIPermission ToAIPermission(FireState state)
    {
        if (state.IsSelected)
        {
            return default;
        }

        bool isFree = !state.IsFixed;
        return new FireAIPermission
        {
            CanDash = isFree,
            CanMove = isFree,
            CanSearch = true,
            CanLongAttack = true,
            CanGuard = true,
            CanCounterGuard = true,
            CanExplosion = true,
        };
    }

    /// <summary>火の色をUnityのColorに変換する。Noneは透明</summary>
    public static Color ToUnityColor(FireColor color)
    {
        switch (color)
        {
            case FireColor.None: return Color.clear;
            case FireColor.Red: return Color.red;
            case FireColor.Green: return Color.green;
            case FireColor.Blue: return Color.blue;
            case FireColor.Cyan: return Color.cyan;
            case FireColor.Yellow: return Color.yellow;
            case FireColor.Magenta: return Color.magenta;
            case FireColor.White: return Color.white;
            default: Debug.LogWarning($"[TypeManager] ToUnityColor : Unknown FireColor = {color}"); return Color.white;
        }
    }

    /// <summary>intを安全にEnumへ変換する。定義にない値はfallbackを返す</summary>
    public static T ToEnum<T>(int value, T fallback) where T : struct, Enum
    {
        return Enum.IsDefined(typeof(T), value) ? (T)Enum.ToObject(typeof(T), value) : fallback;
    }

    public Dictionary<FireColor, FireState> CopyFireStates()
    {
        return new Dictionary<FireColor, FireState>(_fireStates);
    }
    #endregion
}
