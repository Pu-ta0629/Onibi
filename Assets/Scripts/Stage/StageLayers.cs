using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;


public static class StageLayers
{
    public const string Prefix = "Terrain_";

    private static readonly HashSet<TerrainType> Warned = new HashSet<TerrainType>();

    // 例: Terrain_Ground
    public static string GetLayerName(TerrainType terrain)
    {
        return Prefix + terrain;
    }

    // 地形のレイヤーの番号。作られていない時は、警告を一回だけ出して、Defaultレイヤー(0)にする
    public static int GetLayer(TerrainType terrain)
    {
        int layer = LayerMask.NameToLayer(GetLayerName(terrain));
        if (layer >= 0) return layer;

        if(Warned.Add(terrain))
        {
            Debug.LogWarning($"[StageLayers] レイヤー{GetLayerName(terrain)}がありません。" +
                             "Tools > Stage Tools > Ensure Layers で作れます(Defaultレイヤーで出します)");
        }
        return 0;
    }

    // 複数地形を合わせた、レイヤーマスク
    public static int GetMask(params TerrainType[] terrains)
    {
        int mask = 0;
        foreach(TerrainType terrain in terrains)
        {
            int layer = LayerMask.NameToLayer(GetLayerName(terrain));
            if (layer >= 0) mask |= 1 << layer;
        }
        return mask;
    }

    // 再生を始めるときに、前の状態を消す
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Warned.Clear();
    }

}
