using System;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

/// <summary>
/// 火のライト設定（Inspectorで調整する値）。LightManagerが持ち、全ての火で共通に使う
/// </summary>
[Serializable]
public class FireLightConfig
{
    [Tooltip("ゲーム内の1ブロックの大きさ（Unityの距離）。判定上の光の届く距離 = 光量レベル × この値")]
    [Range(0.1f, 5f)] public float BlockSize = 1f;

    [Tooltip("光量レベルが最大（15）のときのLightの強さ")]
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

    [Header("見た目と判定を合わせる補正")]
    [Tooltip("Lightのrangeを、判定上の届く距離の何倍にするか。Point Lightは端に向かってなだらかに暗くなるため、見た目で明るく見える範囲はrangeより小さい。見た目と判定が合うように、Sceneビューの黄色い円（判定上の届く距離）を見ながら調整する")]
    [Range(1f, 3f)] public float VisualRangeScale = 1.6f;

    [Tooltip("光量レベルが低いときのLightの強さの下限（MaxIntensityに対する割合）。強さを光量レベルにそのまま比例させると、低いレベルが暗すぎて見えないため、最低でもこの割合は確保する")]
    [Range(0f, 1f)] public float MinIntensityRate = 0.4f;

    [Tooltip("Sceneビューに、判定上の届く距離（黄）と、暗いと判定される境界（赤）の円を表示する")]
    public bool ShowGizmo = true;

    /// <summary>明るさレベルに対応する光量レベルを返す</summary>
    public int GetLightLevel(FireBrightness brightness)
    {
        switch (brightness)
        {
            case FireBrightness.Level1: return Level1;
            case FireBrightness.Level3: return Level3;
            default: return Level2;
        }
    }
}

/// <summary>
/// 火の光を担当する純粋なC#クラス。Lightの生成・明るさの反映・「ある地点の光量」の計算をまとめる
/// FireControllerは触らない。LightManagerが生成して、SetBrightnessを呼ぶ
/// 光量の考え方はマイクラと同じ（光量レベル=届くブロック数。1ブロック離れるごとに光量が1下がる）
/// 判定（GetLightLevelAt）は、Lightのrangeではなく、この「届くブロック数」だけで決まる
/// 見た目のLightは、VisualRangeScaleで広げて、判定の範囲と同じくらいに明るく見えるようにしている
/// </summary>
public class FireLight
{
    /// <summary>光量レベルの最大値（マイクラと同じ15）</summary>
    public const int MaxLightLevel = 15;

    private readonly Transform _owner;
    private readonly FireLightConfig _config;
    private readonly Light _light;
    private FireBrightness _brightness = FireBrightness.Level2;

    /// <summary>今の光量レベル（1~15）</summary>
    public int LightLevel => _config.GetLightLevel(_brightness);

    /// <summary>判定上の光の届く距離（Unityの距離）。この距離で光量が0になる</summary>
    public float Range => LightLevel * _config.BlockSize;

    /// <summary>見た目のLightのrange。判定上の届く距離より広くして、見た目の明るい範囲を合わせる</summary>
    public float VisualRange => Range * _config.VisualRangeScale;

    /// <summary>
    /// ownerの子として、火の色のPoint Lightを作る。existingLightを渡すとそれを使う（nullなら自動で作る）
    /// </summary>
    public FireLight(Transform owner, FireColor color, FireLightConfig config, Light existingLight = null)
    {
        _owner = owner;
        _config = config;
        _light = existingLight != null ? existingLight : CreateLight(owner, color, config);
        Refresh();
    }

    #region Public
    /// <summary>明るさレベルを設定して、Lightに反映する</summary>
    public void SetBrightness(FireBrightness brightness)
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

        // 既存のLightを渡された場合でも、Spotにそろえる
        _light.type = LightType.Spot;

        float rate = Mathf.Lerp(_config.MinIntensityRate, 1f, (float)LightLevel / MaxLightLevel);
        _light.intensity = _config.MaxIntensity * rate;

        // 地面に出る円の半径 = VisualRange になるように、角度と届く距離を決める
        float height = Mathf.Max(0.1f, _config.Height);
        float groundRadius = VisualRange;
        float spotAngle = Mathf.Clamp(2f * Mathf.Atan(groundRadius / height) * Mathf.Rad2Deg, 1f, 170f);

        _light.spotAngle = spotAngle;
        _light.innerSpotAngle = spotAngle * 0.5f;   // 縁をなだらかにする
        _light.range = Mathf.Sqrt(height * height + groundRadius * groundRadius) * 1.05f;

        UpdatePose();
    }

    /// <summary>Lightを、火の真上に置いて真下へ向ける（火の回転がカメラに追従するため、毎フレーム呼ぶ）</summary>
    public void UpdatePose()
    {
        if (_light == null || _owner == null)
        {
            return;
        }

        Transform t = _light.transform;
        t.position = _owner.position + Vector3.up * _config.Height;
        t.rotation = Quaternion.Euler(90f, 0f, 0f);
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

#if UNITY_EDITOR
    /// <summary>
    /// Sceneビューに、判定の範囲を円で描く（黄＝光量が0になる距離 / 赤＝darkThreshold以下になる＝暗いと判定される境界）
    /// 見た目の明るさと、この円が合っているかを見て、VisualRangeScaleを調整する
    /// </summary>
    public void DrawGizmos(float darkThreshold)
    {
        if (_owner == null)
        {
            return;
        }

        Vector3 center = _owner.position;

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, Range);

        float darkRange = Mathf.Max(0f, LightLevel - darkThreshold) * _config.BlockSize;
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, darkRange);
    }
#endif
    #endregion

    #region Private
    //火の色のPoint Lightを子として作る
    private static Light CreateLight(Transform owner, FireColor color, FireLightConfig config)
    {
        GameObject obj = new GameObject("Object_FireLight");
        obj.transform.SetParent(owner, false);

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = TypeManager.ToUnityColor(color);
        light.intensity = 250f;
        light.shadows = LightShadows.None;

        return light;
    }
    #endregion
}
