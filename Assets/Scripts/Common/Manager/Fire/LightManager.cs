using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火に付属するLightをまとめて管理するクラス
/// 明るさレベルごとの光量などの値は、全ての火で共通のものをここで持つ
/// 火が持つのは明るさのレベルだけで、レベルを受け取ってLightの値を変えるのはここが担当する
/// 光量の計算（ある地点の明るさ）もここで行う
/// </summary>
public class LightManager : MonoBehaviour
{
    [Header("共通の光の設定（全ての火で共通）")]
    [SerializeField] private FireLightConfig _config = new FireLightConfig();

    private readonly Dictionary<TypeManager.FireColor, FireLight> _lights = new Dictionary<TypeManager.FireColor, FireLight>();
    private bool _isInitialized;

    #region Initialize
    /// <summary>
    /// FireManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize()
    {
        _lights.Clear();
        _isInitialized = true;
    }
    #endregion

    #region Public
    /// <summary>
    /// 火にLightを付ける。火の子としてLightを作り、以降はSetBrightnessで値を変える
    /// </summary>
    public bool Register(TypeManager.FireColor color, Transform owner)
    {
        if (!_isInitialized || owner == null || color == TypeManager.FireColor.None)
        {
            Debug.LogError("[LightManager] Register : Not initialized or reference is Null!!! ");
            return false;
        }

        if (_lights.ContainsKey(color))
        {
            Debug.LogWarning($"[LightManager] Register : {color} is already registered!!");
            return false;
        }

        _lights.Add(color, new FireLight(owner, color, _config));
        return true;
    }

    /// <summary>
    /// 火の明るさレベルを反映する。そのレベルに対応する共通の値でLightが変わる
    /// </summary>
    public void SetBrightness(TypeManager.FireColor color, TypeManager.FireBrightness brightness)
    {
        if (_lights.TryGetValue(color, out FireLight light))
        {
            light.SetBrightness(brightness);
        }
    }

    /// <summary>
    /// 指定した地点の光量。登録されている全ての火のうち、最も明るい値を返す
    /// </summary>
    public float GetLightLevelAt(Vector3 point)
    {
        float max = 0f;
        foreach (FireLight light in _lights.Values)
        {
            max = Mathf.Max(max, light.GetLightLevelAt(point));
        }

        return max;
    }
    #endregion

#if UNITY_EDITOR
    #region UnityLifeCycle
    //再生中にInspectorで共通の設定値を触ったとき、全ての火にすぐ反映する
    private void LateUpdate()
    {
        foreach (FireLight light in _lights.Values)
        {
            light.Refresh();
        }
    }
    #endregion
#endif
}