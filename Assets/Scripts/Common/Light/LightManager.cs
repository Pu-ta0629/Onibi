using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火に付属するLightをまとめて管理するクラス
/// 明るさレベルごとの設定(角度・Range・Intensity)は、全ての火で共通のものをここで持つ
/// Light本体(SpotLightとPointLight)は各火のFireControllerが持ち、ここへ渡される
/// 火が持つのは明るさのレベルだけで、レベルを受け取ってLightの値を変えるのはここが担当する
/// 光量の計算（ある地点の明るさ）もここで行う
/// </summary>
public class LightManager : MonoBehaviour
{
    [Header("共通の光の設定（全ての火で共通）")]
    [SerializeField] private FireLightConfig _config = new FireLightConfig();

    private readonly Dictionary<FireColor, FireLight> _lights = new Dictionary<FireColor, FireLight>();
    private float _darkThreshold;   //Gizmoで「暗いと判定される境界」を描くのに使う
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
    /// 火のLightを登録する。以降はSetBrightnessで値を変える
    /// </summary>
    /// <param name="spotLight">明るさ判定＋見た目用（必須）</param>
    /// <param name="pointLight">迷子用。nullなら自動で作る</param>
    public bool Register(FireColor color, Transform owner, Light spotLight, Light pointLight, bool isLit)
    {
        if (!_isInitialized || owner == null || color == FireColor.None)
        {
            Debug.LogError("[LightManager] Register : Not initialized or reference is Null!!! ");
            return false;
        }

        if (_lights.ContainsKey(color))
        {
            Debug.LogWarning($"[LightManager] Register : {color} is already registered!!");
            return false;
        }

        FireLight fireLight = new FireLight(owner, color, _config, spotLight, pointLight);
        fireLight.SetLit(isLit);
        _lights.Add(color , fireLight);
        return true;
    }

    /// <summary>
    /// 火の明るさレベルを反映する。そのレベルに対応する共通の値でLightが変わる
    /// </summary>
    public void SetBrightness(FireColor color, FireBrightness brightness)
    {
        if (_lights.TryGetValue(color, out FireLight light))
        {
            light.SetBrightness(brightness);
        }
    }
    public void SetLit(FireColor color, bool isLit)
    {
        if(_lights.TryGetValue(color, out FireLight light))
        {
            light.SetLit(isLit);
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

    /// <summary>
    /// 「暗い」と判定されるしきい値を受け取る（Sceneビューの確認用の円を描くためだけに使う。判定自体はDarknessSensorが行う）
    /// </summary>
    public void SetDarkThreshold(float darkThreshold)
    {
        _darkThreshold = darkThreshold;
    }
    #endregion

    #region UnityLifeCycle
    //火はカメラと同じ向きに回るので、Lightを毎フレーム真下に向け直す
    private void LateUpdate()
    {
        foreach (FireLight light in _lights.Values)
        {
            light.UpdatePose();
#if UNITY_EDITOR
            light.Refresh();   //再生中にInspectorの共通設定を触っても、すぐ反映する
#endif
        }
    }
    #endregion

#if UNITY_EDITOR
    #region EditorOnly
    private void OnDrawGizmos()
    {
        if (!_config.ShowGizmo)
        {
            return;
        }

        foreach (FireLight light in _lights.Values)
        {
            light.DrawGizmos(_darkThreshold);
        }
    }
    #endregion
#endif
}