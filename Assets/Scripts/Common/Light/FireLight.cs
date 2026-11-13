using System;
using UnityEngine;

/// <summary>
/// 明るさレベル1つ分の設定。角度は Light の Spot Angle(全体の角度)と同じ意味
/// SpotLightはInner/Outer角だけがレベルで変わる（Intensity・Rangeは全レベル共通。色の輝度で倍率がかかる）
/// 判定(GetLightLevelAt)と見た目(SpotLight)は、ここの同じ角度を使うので一致する
/// </summary>
[Serializable]
public class FireLightLevel
{
    [Tooltip("Inner角(度)。この内側は光量が最大(15)")]
    [Range(0f, 179f)] public float InnerAngle;

    [Tooltip("Outer角(度)。この外側は光量が0")]
    [Range(1f, 179f)] public float OuterAngle = 15f;

    public FireLightLevel() { }

    public FireLightLevel(float inner, float outer)
    {
        InnerAngle = inner;
        OuterAngle = outer;
    }
}

/// <summary>
/// 火のライト設定(Inspectorで調整する値)。LightManagerが持ち、全ての火で共通に使う
/// Lightそのものは、各火のFireControllerが持つ(SpotLightとPointLightの2つ)
/// </summary>
[Serializable]
public class FireLightConfig
{
    [Header("SpotLight(レベルで変わるのはInner/Outer角だけ)")]
    [Tooltip("SpotLightを置く高さ(火の位置からの上方向)")]
    public float YOffset = 10f;

    [Tooltip("SpotLightのIntensityの基準値(白の火のとき。色の輝度で倍率がかかる)")]
    public float Intensity = 250f;

    [Tooltip("SpotLightのRangeの基準値(白の火のとき。色の輝度で倍率がかかる)")]
    public float Range = 20f;

    [Header("輝度合わせ(SpotのIntensity・Range)")]
    [Tooltip("色ごとの見かけの明るさをそろえる(白を基準に、暗く見える色ほどIntensityとRangeを上げる)")]
    public bool MatchLuminance = true;

    [Tooltip("輝度合わせで、Intensityを何倍まで上げるか(青が約8.8倍)。Rangeはその平方根倍(距離の2乗で光が弱まるため)")]
    [Range(1f, 20f)] public float MaxLuminanceScale = 10f;

    [Header("PointLight(迷子用。レベルごとにIntensity・Rangeが変わる)")]
    [Tooltip("PointLightを置く高さ(火の位置からの上方向)")]
    public float PointYOffset = 2f;

    [Tooltip("PointLightのIntensity(Element 0 = Level1, 1 = Level2, 2 = Level3)")]
    public float[] PointIntensities = { 3f, 5f, 10f };

    [Tooltip("PointLightのRange(Element 0 = Level1, 1 = Level2, 2 = Level3)")]
    public float[] PointRanges = { 3f, 5f, 10f };

    [Header("Culling Mask")]
    [Tooltip("SpotLightが照らすLayer(見た目用)。0(Nothing)のときは、Groundレイヤーのみ")]
    public LayerMask SpotCullingMask;

    [Tooltip("迷子用PointLightが照らすLayer(迷子のLayer)。0(Nothing)のときは、Ground以外すべて")]
    public LayerMask PointCullingMask;

    [Header("明るさレベル(Element 0 = Level1, 1 = Level2, 2 = Level3)")]
    public FireLightLevel[] Levels =
    {
        new FireLightLevel(0f, 15f),
        new FireLightLevel(25f, 40f),
        new FireLightLevel(60f, 75f),
    };

    [Header("Debug")]
    [Tooltip("Sceneビューに、照らされる範囲(黄=外側 / 緑=内側 / 赤=暗いと判定される境界)を表示する")]
    public bool ShowGizmo = true;

    private static readonly FireLightLevel Fallback = new FireLightLevel();

    /// <summary>レベル(0 = Level1)の設定。範囲外は、端のレベルで代用する</summary>
    public FireLightLevel GetLevel(int index)
    {
        if (Levels == null || Levels.Length == 0)
        {
            return Fallback;
        }

        return Levels[Mathf.Clamp(index, 0, Levels.Length - 1)] ?? Fallback;
    }

    /// <summary>レベルごとのPointLightのIntensity。範囲外は端の値で代用する</summary>
    public float GetPointIntensity(int index) => GetFromArray(PointIntensities, index, 3f);

    /// <summary>レベルごとのPointLightのRange。範囲外は端の値で代用する</summary>
    public float GetPointRange(int index) => GetFromArray(PointRanges, index, 3f);

    private static float GetFromArray(float[] array, int index, float fallback)
    {
        if (array == null || array.Length == 0)
        {
            return fallback;
        }

        return array[Mathf.Clamp(index, 0, array.Length - 1)];
    }

    /// <summary>SpotLightのCulling Mask。未設定ならGroundのみ</summary>
    public int ResolveSpotMask()
    {
        return SpotCullingMask.value != 0 ? SpotCullingMask.value : LayerMask.GetMask("Ground");
    }

    /// <summary>PointLightのCulling Mask。未設定ならGround以外すべて</summary>
    public int ResolvePointMask()
    {
        return PointCullingMask.value != 0 ? PointCullingMask.value : ~LayerMask.GetMask("Ground");
    }
}

/// <summary>
/// 火の光を担当する純粋なC#クラス。FireControllerが持つSpotLightとPointLightを、明るさレベルに合わせて調整し、
/// 「ある地点の光量」を計算する
///   SpotLight  ... 明るさ判定と見た目用。Culling MaskはGroundのみ。位置y=YOffset、Inner/Outerがレベルで変わる
///                  Intensity・Rangeは基準値に、色の輝度に応じた倍率を掛けた値
///   PointLight ... 迷子用。迷子のLayerだけを照らす。位置y=PointYOffset、Intensity・Rangeはレベルごと
/// 判定は、Lightを真下に向けたときに地面に出る円と同じ形になる
///   Inner角の内側 = 最大(15)、Outer角の外側 = 0、その間は直線で下がる
/// </summary>
public class FireLight
{
    /// <summary>光量の最大値</summary>
    public const int MaxLightLevel = 15;

    private readonly Transform _owner;
    private readonly FireLightConfig _config;
    private readonly Light _spot;
    private readonly Light _point;
    private readonly Color _color;
    private FireBrightness _brightness = FireBrightness.Level1;
    private bool _isLit;   // 初期は消灯
    private FireLightLevel Level => _config.GetLevel((int)_brightness);

    /// <summary>今のレベルのOuter角(度)。この外側は光量0</summary>
    public float OuterAngle => Level.OuterAngle;

    /// <summary>今のレベルのInner角(度)。この内側は光量が最大</summary>
    public float InnerAngle => Mathf.Min(Level.InnerAngle, Level.OuterAngle);

    /// <summary>SpotLightのIntensity(基準値 × 輝度の倍率)</summary>
    public float SpotIntensity => _config.Intensity * GetLuminanceScale();

    /// <summary>SpotLightのRange(基準値 × 輝度の倍率の平方根)。判定に使う距離の上限にもなる</summary>
    public float SpotRange => _config.Range * Mathf.Sqrt(GetLuminanceScale());

    /// <param name="spotLight">FireControllerが持つSpotLight(必須)</param>
    /// <param name="pointLight">FireControllerが持つ迷子用PointLight。nullなら火の子として自動で作る</param>
    public FireLight(Transform owner, FireColor color, FireLightConfig config, Light spotLight, Light pointLight)
    {
        _owner = owner;
        _config = config;
        _color = TypeManager.ToUnityColor(color);

        if (spotLight == null)
        {
            Debug.LogWarning($"[FireLight] SpotLight is not set. It is created automatically. color = {color}");
            GameObject createdSpot = new GameObject("Light_Spot");
            createdSpot.transform.SetParent(owner, false);
            spotLight = createdSpot.AddComponent<Light>();
        }
        _spot = spotLight;

        if (pointLight == null)
        {
            Debug.LogWarning($"[FireLight] PointLight is not set. It is created automatically. color = {color}");
            GameObject created = new GameObject("Light_Point");
            created.transform.SetParent(owner, false);
            pointLight = created.AddComponent<Light>();
        }
        _point = pointLight;

        if (LayerMask.GetMask("Ground") == 0 && config.SpotCullingMask.value == 0)
        {
            Debug.LogWarning("[FireLight] Layer 'Ground' is not found. Set SpotCullingMask in LightManager!!");
        }

        Refresh();
    }

    #region Public
    /// <summary>明るさレベルを設定して、Lightの値を切り替える</summary>
    public void SetBrightness(FireBrightness brightness)
    {
        _brightness = brightness;
        Refresh();
    }

    /// <summary>共通設定と今のレベルの設定を、SpotLightとPointLightへ反映する</summary>
    public void Refresh()
    {
        FireLightLevel level = Level;
        int index = (int)_brightness;

        //SpotLight: Type=Spot、Inner/Outerはレベルごと、Intensity・Rangeは基準値に輝度の倍率を掛けた値。Groundだけを照らす
        if (_spot != null)
        {
            _spot.type = LightType.Spot;
            _spot.color = _color;
            _spot.spotAngle = level.OuterAngle;                              //Outerを先に設定する(Innerは、Outer以下に制限されるため)
            _spot.innerSpotAngle = Mathf.Min(level.InnerAngle, level.OuterAngle);
            _spot.range = SpotRange;
            _spot.intensity = SpotIntensity;
            _spot.cullingMask = _config.ResolveSpotMask();
            _spot.shadows = LightShadows.None;
            _spot.enabled = _isLit;
        }

        //PointLight: 迷子用。Type=Point、Intensity・Rangeはレベルごと
        if (_point != null)
        {
            _point.type = LightType.Point;
            _point.color = _color;
            _point.intensity = _config.GetPointIntensity(index);
            _point.range = _config.GetPointRange(index);
            _point.cullingMask = _config.ResolvePointMask();
            _point.shadows = LightShadows.None;
            _point.enabled = _isLit;
        }

        UpdatePose();
    }

    /// <summary>
    /// SpotLightを火の真上(YOffset)に置いて真下へ向け、PointLightを火の上(PointYOffset)に置く
    /// 火はカメラと同じ向きに回る(ビルボード)ので、子のままだとLightも傾く。毎フレーム呼ぶ
    /// </summary>
    public void UpdatePose()
    {
        if (_owner == null)
        {
            return;
        }

        if (_spot != null)
        {
            Vector3 position = _owner.position + Vector3.up * _config.YOffset;
            _spot.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        }

        if (_point != null)
        {
            _point.transform.position = _owner.position + Vector3.up * _config.PointYOffset;
        }
    }

    /// <summary>
    /// 指定した地点の、この火による光量(0 to 15)を返す
    /// Lightから見て、真下との角度で決める。Inner角の内側=15、Outer角の外側=0、その間は直線で下がる
    /// 地点の高さも使うので、地面が丘状でも見た目と合う
    /// </summary>
    public float GetLightLevelAt(Vector3 point)
    {
        if (!_isLit) return 0f;
        Vector3 lightPosition = _owner.position + Vector3.up * _config.YOffset;
        float down = lightPosition.y - point.y;   //Lightから地点までの、真下方向の距離
        if (down <= 0f)
        {
            return 0f;
        }

        float horizontal = new Vector2(point.x - lightPosition.x, point.z - lightPosition.z).magnitude;
        if (Mathf.Sqrt(horizontal * horizontal + down * down) > SpotRange)
        {
            return 0f;
        }

        float angle = Mathf.Atan2(horizontal, down) * Mathf.Rad2Deg;   //真下からの角度
        float inner = InnerAngle * 0.5f;
        float outer = OuterAngle * 0.5f;

        float t = outer <= inner ? (angle <= outer ? 0f : 1f) : Mathf.InverseLerp(inner, outer, angle);
        return MaxLightLevel * (1f - t);
    }

    /// <summary>明るさを、輝度で測る(Stack Overflowの定番の式。0 to 1)</summary>
    public static float Luminance(Color color)
    {
        return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Sceneビューに、照らされる範囲を円で描く
    /// 黄=Outer角(光量0になる境界) / 緑=Inner角(最大の範囲) / 赤=darkThreshold以下になる=暗いと判定される境界
    /// 円は、火の高さの水平面に描く
    /// </summary>
    public void DrawGizmos(float darkThreshold)
    {
        if (_owner == null)
        {
            return;
        }

        float inner = InnerAngle * 0.5f;
        float outer = OuterAngle * 0.5f;
        float darkT = 1f - Mathf.Clamp01(darkThreshold / MaxLightLevel);
        float darkAngle = Mathf.Lerp(inner, outer, darkT);

        Vector3 center = _owner.position;

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, RadiusAt(outer));

        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, RadiusAt(inner));

        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, RadiusAt(darkAngle));
    }
#endif
    #endregion

    #region Private
    //片側の角度(度)から、火の高さの水平面に出る円の半径を求める
    private float RadiusAt(float halfAngle)
    {
        return _config.YOffset * Mathf.Tan(Mathf.Clamp(halfAngle, 0f, 89f) * Mathf.Deg2Rad);
    }

    //白(輝度1)を基準に、暗く見える色ほど大きくなる倍率(Intensityに掛ける。Rangeはこの平方根)
    private float GetLuminanceScale()
    {
        if (!_config.MatchLuminance)
        {
            return 1f;
        }

        float luminance = Luminance(_color);
        if (luminance < 1e-4f)
        {
            return 1f;
        }

        return Mathf.Clamp(1f / luminance, 1f, _config.MaxLuminanceScale);
    }

    public void SetLit(bool isLit)
    {
        if (_isLit == isLit) return;
        _isLit = isLit;
        Refresh();
    }
    #endregion
}