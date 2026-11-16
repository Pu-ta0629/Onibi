using UnityEngine;

public class BurnConfig
{
    ///<summary>継続ダメージの間隔</summary>
    public const float DefaultCooldown = 1f;
    /// <summary>回数・威力の上限の初期値 </summary>
    public const int DefaultMax = 99;
    private const float MinCooldown = 0.01f;

    /// <summary>初期値の設定(読み取り専用なので、使いまわす)</summary>
    public static readonly BurnConfig Default = new BurnConfig();

    /// <summary>定期ダメージの間隔(秒)。全色で共通の固定値</summary>
    public float Cooldown { get; }
    /// <summary>火傷回数の上限(色ごと)</summary>
    public int MaxCount { get; }
    /// <summary>火傷威力の上限(色ごと)</summary>
    public int MaxPotency { get; }

    public BurnConfig(float cooldown = DefaultCooldown,int maxCount = DefaultMax,int maxPotency = DefaultMax)
    {
        Cooldown = Mathf.Max(MinCooldown, cooldown);
        MaxCount = Mathf.Max(1, maxCount);
        maxCount = Mathf.Max(1, maxCount);
    }

}
