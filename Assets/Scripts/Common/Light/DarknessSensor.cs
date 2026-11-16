using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DarknessConfig
{
    [Header("暗さ(ダメージ)")]
    [Tooltip("周りの光量がこの値以下なら「暗い」と判定する。暗い間、迷子はダメージを受ける")]
    [Range(0, 15)] public float DarkThreshold = 2f;

    [Tooltip("暗い状態から戻るには、しきい値よりこの値だけ明るくなる必要がある")]
    [Range(0, 15)] public float DarkExitMargin = 0.5f;

    [Tooltip("暗い間、何秒ごとにダメージを受けるか(最初のダメージは、暗くなってからこの秒数後)")]
    [Range(0.1f, 10f)] public float DarkDamageInterval = 1f;

    [Header("明るさ(停止)")]
    [Tooltip("周りの光量がこの値以上なら「明るい」と判定する。明るい間、迷子は動かない。15が最大(火のInner角の内側)")]
    [Range(0, 15)] public float BrightThreshold = 15f;

    [Tooltip("明るい状態から戻るには、しきい値よりこの値だけ暗くなる必要がある")]
    [Range(0, 15)] public float BrightExitMargin = 0.5f;
}

/// <summary>
/// 暗い場所に入った・出た時の処理。やりたいことが増えたら、これを実装したクラスを足してAddReactionで登録する
/// </summary>
public interface IDarknessReaction
{
    /// <summary>暗い場所に入った時</summary>
    void OnEnterDark();
    /// <summary>暗い場所から出た時</summary>
    void OnExitDark();
}

/// <summary>
/// 明るい場所に入った・出た時の処理。実装したクラスをAddBrightReactionで登録する
/// </summary>
public interface IBrightReaction
{
    /// <summary>明るい場所に入った時</summary>
    void OnEnterBright();
    /// <summary>明るい場所から出た時</summary>
    void OnExitBright();
}

/// <summary>
/// 対象(迷子)の周りの明るさを検知する純粋C#クラス
/// 光量の取得方法は、呼ぶ側が関数で渡す(光源がどう実装されていても使える)
/// 暗い(しきい値以下)と、明るい(しきい値以上)の2つの状態を持ち、切り替わったときにReactionへ通知する
/// </summary>
public class DarknessSensor
{
    private readonly Transform _target;
    private readonly Func<Vector3, float> _getLightLevel;
    private readonly DarknessConfig _config;
    private readonly List<IDarknessReaction> _darkReactions = new List<IDarknessReaction>();
    private readonly List<IBrightReaction> _brightReactions = new List<IBrightReaction>();

    /// <summary>対象の周りにある光量</summary>
    public float LightLevel { get; private set; }
    /// <summary>暗い場所にいるか</summary>
    public bool IsDark { get; private set; }
    /// <summary>明るい場所にいるか</summary>
    public bool IsBright { get; private set; }
    /// <summary>暗さが切り替わった時に通知する。暗い = true</summary>
    public event Action<bool> OnDarkChange;
    /// <summary>明るさが切り替わった時に通知する。明るい = true</summary>
    public event Action<bool> OnBrightChange;

    /// <param name="target">明るさを調べる対象</param>
    /// <param name="getLightLevel">指定した座標の光量を返す関数</param>
    /// <param name="config">しきい値などの設定</param>
    public DarknessSensor(Transform target, Func<Vector3, float> getLightLevel, DarknessConfig config)
    {
        _target = target;
        _getLightLevel = getLightLevel;
        _config = config;
    }

    #region Public
    /// <summary>暗い場所に入った/出た時の処理を登録する</summary>
    public void AddReaction(IDarknessReaction reaction)
    {
        if (reaction != null && !_darkReactions.Contains(reaction))
        {
            _darkReactions.Add(reaction);
        }
    }

    /// <summary>登録した処理を外す</summary>
    public void RemoveReaction(IDarknessReaction reaction)
    {
        _darkReactions.Remove(reaction);
    }

    /// <summary>明るい場所に入った/出た時の処理を登録する</summary>
    public void AddBrightReaction(IBrightReaction reaction)
    {
        if (reaction != null && !_brightReactions.Contains(reaction))
        {
            _brightReactions.Add(reaction);
        }
    }

    /// <summary>登録した処理を外す</summary>
    public void RemoveBrightReaction(IBrightReaction reaction)
    {
        _brightReactions.Remove(reaction);
    }

    /// <summary>毎フレーム呼ぶ。周りの光量を調べて、暗い/明るいが切り替わったら通知する</summary>
    public void Tick()
    {
        LightLevel = _getLightLevel(_target.position);

        if (!IsDark && LightLevel <= _config.DarkThreshold)
        {
            SetDark(true);
        }
        else if (IsDark && LightLevel > _config.DarkThreshold + _config.DarkExitMargin)
        {
            SetDark(false);
        }

        if (!IsBright && LightLevel >= _config.BrightThreshold)
        {
            SetBright(true);
        }
        else if (IsBright && LightLevel < _config.BrightThreshold - _config.BrightExitMargin)
        {
            SetBright(false);
        }
    }
    #endregion

    #region Private
    private void SetDark(bool isDark)
    {
        IsDark = isDark;
        Debug.Log($"[DarknessSensor] SetDark : isDark = {isDark}, LightLevel = {LightLevel:F1}");

        for (int i = 0; i < _darkReactions.Count; i++)
        {
            if (isDark)
            {
                _darkReactions[i].OnEnterDark();
            }
            else
            {
                _darkReactions[i].OnExitDark();
            }
        }

        OnDarkChange?.Invoke(isDark);
    }

    private void SetBright(bool isBright)
    {
        IsBright = isBright;
        Debug.Log($"[DarknessSensor] SetBright : isBright = {isBright}, LightLevel = {LightLevel:F1}");

        for (int i = 0; i < _brightReactions.Count; i++)
        {
            if (isBright)
            {
                _brightReactions[i].OnEnterBright();
            }
            else
            {
                _brightReactions[i].OnExitBright();
            }
        }

        OnBrightChange?.Invoke(isBright);
    }
    #endregion
}

/// <summary>明るい間、迷子を動かなくする</summary>
public class StopFollowReaction : IBrightReaction
{
    private readonly LostChildController _child;

    public StopFollowReaction(LostChildController child)
    {
        _child = child;
    }

    public void OnEnterBright()
    {
        _child.SetCanMove(false);
    }

    public void OnExitBright()
    {
        _child.SetCanMove(true);
    }
}

/// <summary>暗い間、一定間隔で迷子にダメージを与える(Tickを毎フレーム呼ぶ)</summary>
public class DarkDamageReaction : IDarknessReaction
{
    private readonly LostChildController _child;
    private readonly DarknessConfig _config;
    private float _timer;
    private bool _isDark;

    public DarkDamageReaction(LostChildController child, DarknessConfig config)
    {
        _child = child;
        _config = config;
    }

    public void OnEnterDark()
    {
        _isDark = true;
        _timer = 0f;
    }

    public void OnExitDark()
    {
        _isDark = false;
        _timer = 0f;
    }

    /// <summary>毎フレーム呼ぶ。暗い間だけ時間を進めて、間隔ごとにダメージを与える</summary>
    public void Tick(float deltaTime)
    {
        if (!_isDark)
        {
            return;
        }

        _timer += deltaTime;
        if (_timer < _config.DarkDamageInterval)
        {
            return;
        }

        _timer = 0f;
        _child.TakeDamage(1);   //迷子はライフ型なので、値に関わらず1つ減る
    }
}