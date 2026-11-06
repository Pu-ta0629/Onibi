using UnityEngine;

public class StageGroundProvider
{
    private static StageGroundBuilder _builder;
    
    // ステージの読み込み時に1回呼ぶ
    public static void Load(StageData stage)
    {
        Unload();
        if (stage == null) return;

        _builder = new StageGroundBuilder();
        _builder.Build(stage.WalkableGrid, new StageTerrainColors(), HideFlags.None);
    }

    // ステージの終了時に1回呼ぶ
    public static void Unload()
    {
        _builder?.Clear();
        _builder = null;
    }

    // 再生を始めるときに、前の状態を消す(Domain Reloadを切っている場合の対策)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _builder = null;
    }
}
