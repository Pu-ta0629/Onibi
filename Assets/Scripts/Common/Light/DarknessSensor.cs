using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DarknessConfig
{
    [Tooltip("周りの光量がこの値以下なら「暗い」と判定する")]
    [Range(0, 15)] public float DarkThreshold = 2f;

    [Tooltip("暗い状態から戻るには、しきい値よりこの値だけ明るくなる必要がある")]
    [Range(0, 15)] public float ExitMargin = 0.5f;
}

/// <summary>
/// 暗い場所に入った・出た時の処理。やりたいことが増えたら、これこれを実装したクラスを足してAddReactionで登録する
/// </summary>
public interface IDarknessReaction
{
    /// <summary>暗い場所に入った時</summary>
    void OnEnterDark();
    /// <summary>暗い場所から出たとき</summary>
    void OnExitDark();
}

/// <summary>
/// 対象（迷子）の周りの暗さを検知する純粋C#クラス
/// 光量を取得方法は呼ぶ側げ関数で渡す（光源がどう実装されていても使える）
/// 暗さが一定以下になった時の処理は IDarknessReaction として登録する（拡張用）
/// </summary>
public class DarknessSensor
{
    private readonly Transform _target;
    private readonly Func<Vector3, float> _getLightLevel;
    private readonly DarknessConfig _config;
    private readonly List<IDarknessReaction> _reactions = new List<IDarknessReaction>();

    /// <summary>対象の周りにある光量</summary>
    public float LightLevel { get; private set; }
    /// <summary>暗い場所にいるか</summary>
    public bool IsDark { get; private set; }
    /// <summary>切り替わった時に通知をする。暗い = true</summary>
    public event Action<bool> OnDarkChange;

    /// <param name="target">暗さを調べる対象</param>
    /// <param name="getLightLevel">指定した座標の光量を返す関数</param>
    /// <param name="config">しきい値などの設定</param>
    public DarknessSensor(Transform target, Func<Vector3, float> getLightLevel, DarknessConfig config)
    {
        _target = target;
        _getLightLevel = getLightLevel;
        _config = config;
    }


    #region PUBLIC
    /// <summary>暗い場所に入った/出た時の処理を登録する</summary>
    public void AddReaction(IDarknessReaction reaction)
    {
        if(reaction != null && !_reactions.Contains(reaction))
        {
            _reactions.Add(reaction);
        }
    }
    /// <summary>登録した処理を外す</summary>
    public void RemoveReaction(IDarknessReaction reaction)
    {
        _reactions.Remove(reaction);
    }

    /// <summary>毎フレーム呼ぶ。周りの光量を調べて、暗い/明るいが切り替わったら通知する</summary>
    public void Tick()
    {
        LightLevel = _getLightLevel(_target.position);

        if(!IsDark && LightLevel <= _config.DarkThreshold)
        {
            SetDark(true);
        }
        if(IsDark && LightLevel > _config.DarkThreshold + _config.ExitMargin)
        {
            SetDark(false);
        }
    }
    #endregion

    #region Private
    private void SetDark(bool isDark)
    {
        IsDark = isDark;
        Debug.Log($"[DarknessSensor] SetDark : isDark = {isDark}, LightLevel = {LightLevel:F1}");
        for(int i = 0; i < _reactions.Count;i++)
        {
            if(IsDark)
            {
                _reactions[i].OnEnterDark();
            }
            else
            {
                _reactions[i].OnExitDark();
            }
        }
        OnDarkChange?.Invoke(isDark);
    }
    #endregion 
}

public class StopFollowReaction : IDarknessReaction
{
    private readonly LostChildController _child;
    public StopFollowReaction(LostChildController child)
    {
        _child = child;
    }

    public void OnEnterDark()
    {
        _child.SetCanMove(false);
    }

    public void OnExitDark()
    {
        _child.SetCanMove(true);
    }
}
