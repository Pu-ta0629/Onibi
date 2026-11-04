using System;
using UnityEngine;

/// <summary>
/// 火のライト設定（Inspectorで調整する値）。FireControllerのフィールドとして持たせる
/// </summary>
[Serializable]
public class FireLightConfig
{
    [Tooltip("ゲーム内の1ブロックの大きさ（Unityの距離）。光の届く距離 = 光量レベル × この値")]
    [Range(0.1f, 5f)] public float BlockSize = 1f;

    [Tooltip("光量レベルが最大（15）のときのLightの強さ。実際の強さは 光量レベル / 15 を掛けた値になる")]
    [Range(0.1f, 10f)] public float MaxIntensity = 3f;

    [Tooltip("自動で作るLightの高さ（火の位置からの上方向のずらし量。地面を照らすため少し上に置く）")]
    [Range(0f, 5f)] public float Height = 1f;

    [Header("明るさレベルごとの光量（マイクラと同じ。値=届くブロック数）")]
    [Tooltip("レベル1：非常に小さい範囲だけ照らせる")]
    [Range(1, FireLight.MaxLightLevel)] public int Level1 = 3;
    [Tooltip("レベル2：少しばかり照らせる")]
    [Range(1, FireLight.MaxLightLevel)] public int Level2 = 7;
    [Tooltip("レベル3：最大出力。広範囲を照らせる")]
    [Range(1, FireLight.MaxLightLevel)] public int Level3 = 11;

    /// <summary>明るさレベルに対応する光量レベルを返す</summary>
    public int GetLightLevel(TypeManager.FireBrightness brightness)
    {
        switch (brightness)
        {
            case TypeManager.FireBrightness.Level1: return Level1;
            case TypeManager.FireBrightness.Level3: return Level3;
            default: return Level2;
        }
    }
}

/// <summary>
/// 火の光を担当する純粋なC#クラス。Lightの生成・明るさの反映・「ある地点の光量」の計算をまとめる
/// FireControllerは、生成して SetBrightness を呼ぶだけでよい
/// 光量の考え方はマイクラと同じ（光量レベル=届くブロック数。1ブロック離れるごとに光量が1下がる）
/// </summary>
public class FireLight
{
    /// <summary>光量レベルの最大値（マイクラと同じ15）</summary>
    public const int MaxLightLevel = 15;

    private readonly Transform _owner;
    private readonly FireLightConfig _config;
    private readonly Light _light;
    private TypeManager.FireBrightness _brightness = TypeManager.FireBrightness.Level2;

    /// <summary>今の光量レベル（1~15）</summary>
    public int LightLevel => _config.GetLightLevel(_brightness);

    /// <summary>光の届く距離（Unityの距離）</summary>
    public float Range => LightLevel * _config.BlockSize;

    /// <summary>
    /// ownerの子として、火の色のPoint Lightを作る。existingLightを渡すとそれを使う（nullなら自動で作る）
    /// </summary>
    public FireLight(Transform owner, TypeManager.FireColor color, FireLightConfig config, Light existingLight = null)
    {
        _owner = owner;
        _config = config;
        _light = existingLight != null ? existingLight : CreateLight(owner, color, config);
        Refresh();
    }

    #region Public
    /// <summary>明るさレベルを設定して、Lightに反映する</summary>
    public void SetBrightness(TypeManager.FireBrightness brightness)
    {
        _brightness = brightness;
        Refresh();
    }

    /// <summary>今の設定でLightを更新する（Inspectorで設定値を触ったときにも使える）</summary>
    public void Refresh()
    {
        if (_light == null)
        {
            return;
        }

        //光量レベル=届くブロック数。光量が高いほど遠くまで、かつ明るくなる
        _light.range = Range;
        _light.intensity = _config.MaxIntensity * LightLevel / MaxLightLevel;
    }

    /// <summary>
    /// 指定した地点の、この火による光量（0~15）を返す
    /// マイクラと同じく、光源から1ブロック離れるごとに1下がり、0未満にはならない
    /// 高さは無視して水平の距離で計算する（迷子が地面にいる前提）
    /// </summary>
    public float GetLightLevelAt(Vector3 point)
    {
        Vector3 offset = point - _owner.position;
        offset.y = 0f;
        float blocks = offset.magnitude / _config.BlockSize;
        return Mathf.Max(0f, LightLevel - blocks);
    }
    #endregion

    #region Private
    //火の色のPoint Lightを子として作る
    private static Light CreateLight(Transform owner, TypeManager.FireColor color, FireLightConfig config)
    {
        GameObject obj = new GameObject("FireLight");
        obj.transform.SetParent(owner, false);
        obj.transform.localPosition = Vector3.up * config.Height;

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = TypeManager.ToUnityColor(color);
        light.shadows = LightShadows.None;
        return light;
    }
    #endregion
}