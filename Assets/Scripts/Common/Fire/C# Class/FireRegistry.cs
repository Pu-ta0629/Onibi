using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーン上の火を登録・管理する純粋なC#クラス（「どの火が存在するか」だけを担当する）
/// 登録するときに、火の初期化・戦闘の準備・Lightの取り付け・最初の明るさの反映をまとめて行う
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
    private readonly KodamaStock _kodamaStock;

    /// <summary>登録済みの火（登録順）。毎フレーム回すのに使う</summary>
    public IReadOnlyList<FireController> all => _list;

    public FireRegistry(TypeManager typeManager, LightManager lightManager, Camera mainCamera, HitWorld hitWorld, LostChildController lostChild, KodamaStock kodamaStock)
    {
        _typeManager = typeManager;
        _lightManager = lightManager;
        _mainCamera = mainCamera;
        _hitWorld = hitWorld;
        _lostChild = lostChild;
        _kodamaStock = kodamaStock;
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
    /// 火を登録する。初期化・戦闘の準備・Lightの取り付け・最初の明るさの反映までを行う
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
        fire.InitializeCombat(_hitWorld, _lostChild, _kodamaStock);   //当たり判定・索敵・突進の準備
        _fires.Add(fire.Color, fire);
        _list.Add(fire);
        fire.OnBrightnessEdited += HandleBrightnessEdited;

        //Inspectorの明るさレベルを最初のStateにする。値が変わらなくても反映されるよう、Stateを読んで直接渡す
        _typeManager.ChangeBrightness(fire.Color, fire.initialBrightness);
        FireState state = _typeManager.GetFireState(fire.Color);
        fire.ApplyState(state);
        _lightManager.Register(fire.Color, fire.transform, fire.SpotLight, fire.PointLight);
        _lightManager.SetBrightness(fire.Color, state.Brightness);
        return true;
    }

    /// <summary>
    /// イベントの購読を解除する
    /// </summary>
    public void Dispose()
    {
        for (int i = 0; i < _list.Count; i++)
        {
            if (_list[i] != null)
            {
                _list[i].OnBrightnessEdited -= HandleBrightnessEdited;
            }
        }
    }
    #endregion

    #region Handler
    //再生中にInspectorで明るさレベルが変えられたら、TypeManagerのStateへ反映する（反映後はStateBinderが火とLightへ配る）
    private void HandleBrightnessEdited(FireColor color, FireBrightness level)
    {
        _typeManager.ChangeBrightness(color, level);
    }
    #endregion
}
