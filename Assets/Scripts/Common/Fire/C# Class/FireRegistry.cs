using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーン上の火を登録・管理する純粋なC#クラス（「どの火が存在するか」だけを担当する）
/// 登録するときに、火の初期化・戦闘の準備・Effectの生成・Lightの取り付け・最初のState（進化段階・明るさ）の反映をまとめて行う
/// Stateの保持と変更はTypeManagerが行う。ここはStateを持たない
/// </summary>
public class FireRegistry : IDisposable
{
    private readonly Dictionary<FireColor, FireController> _fires = new Dictionary<FireColor, FireController>();
    private readonly List<FireController> _list = new List<FireController>();
    private readonly TypeManager _typeManager;
    private readonly LightManager _lightManager;
    private readonly Camera _mainCamera;
    private readonly HitWorld _hitWorld;
    private readonly LostChildController _lostChild;
    private readonly int _initialKodama;   //火が最初に持つ木霊の量（木霊は火ごとに持つ）
    private readonly FireEffectCatalog _effectCatalog;

    /// <summary>登録済みの火（登録順）。毎フレーム回すのに使う</summary>
    public IReadOnlyList<FireController> all => _list;

    /// <summary>登録済みのどれかの火の、特殊木霊が変わったときに通知する（MorphManagerが再判定に使う）</summary>
    public event Action OnSpecialKodamaChanged;

    /// <summary>登録済みのどれかの火の、木霊量が変わったときに通知する（MorphManagerが再判定に使う）</summary>
    public event Action OnKodamaChanged;

    public FireRegistry(TypeManager typeManager, LightManager lightManager, Camera mainCamera, HitWorld hitWorld, LostChildController lostChild, int initialKodama, FireEffectCatalog effectCatalog)
    {
        _typeManager = typeManager;
        _lightManager = lightManager;
        _mainCamera = mainCamera;
        _hitWorld = hitWorld;
        _lostChild = lostChild;
        _initialKodama = initialKodama;
        _effectCatalog = effectCatalog;
    }

    #region Public
    /// <summary>
    /// 火が登録されているか
    /// </summary>
    public bool Has(FireColor color)
    {
        return _fires.ContainsKey(color);
    }

    /// <summary>
    /// 色から火を取得する。未登録ならfalse
    /// </summary>
    public bool TryGet(FireColor color, out FireController fire)
    {
        return _fires.TryGetValue(color, out fire);
    }

    /// <summary>
    /// 火を登録する。初期化・戦闘の準備・Effectの生成・Lightの取り付け・最初のStateの反映までを行う
    /// </summary>
    public bool Register(FireController fire)
    {
        if (fire == null)
        {
            Debug.LogError("[FireRegistry] Register : Fire is Null!!! ");
            return false;
        }

        if (fire.Color == FireColor.None)
        {
            Debug.LogError($"[FireRegistry] Register : FireColor is None!!! name = {fire.name}");
            return false;
        }

        if (_fires.ContainsKey(fire.Color))
        {
            Debug.LogWarning($"[FireRegistry] Register : {fire.Color} is already registered!!");
            return false;
        }

        fire.Initialize(_mainCamera);
        fire.InitializeCombat(_hitWorld, _lostChild, _initialKodama); //当たり判定・索敵・突進の準備（木霊はこの火の分を作る）
        fire.InitializeEffect(_effectCatalog);                         //今のデータのEffectを、火の色で出す
        _fires.Add(fire.Color, fire);
        _list.Add(fire);
        fire.OnSpecialKodamaChanged += HandleSpecialKodamaChanged;
        if (fire.Kodama != null)
        {
            fire.Kodama.OnChanged += HandleKodamaChanged;
        }

        //FireStateDataの進化段階と明るさレベルを最初のStateにする。値が変わらなくても反映されるよう、Stateを読んで直接渡す
        _typeManager.SetInitialKind(fire.Color, fire.Data.FireKind);
        _typeManager.ChangeBrightness(fire.Color, fire.initialBrightness);
        FireState state = _typeManager.GetFireState(fire.Color);
        fire.ApplyState(state);
        _lightManager.Register(fire.Color, fire.transform, fire.SpotLight, fire.PointLight, false);
        _lightManager.SetBrightness(fire.Color, state.Brightness);
        return true;
    }

    /// <summary>
    /// 後始末（火の特殊木霊の変更通知の購読を外す）
    /// </summary>
    public void Dispose()
    {
        for (int i = 0; i < _list.Count; i++)
        {
            if (_list[i] != null)
            {
                _list[i].OnSpecialKodamaChanged -= HandleSpecialKodamaChanged;
                if (_list[i].Kodama != null)
                {
                    _list[i].Kodama.OnChanged -= HandleKodamaChanged;
                }
            }
        }
    }

    private void HandleKodamaChanged(int count)
    {
        OnKodamaChanged?.Invoke();
    }

    private void HandleSpecialKodamaChanged()
    {
        OnSpecialKodamaChanged?.Invoke();
    }
    #endregion
}