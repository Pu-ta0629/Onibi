using UnityEngine;

public static class StageNavMeshProvider
{
    public static StageNavMeshSet Current { get; private set;  }

    // ステージの読み込み時に1回呼ぶ。必要なNavMeshを、まとめて先に作る
    public static void Load(StageData stage,params MovementProfile[]extraProfiles)
    {
        Unload();
        Current = StageNavMeshSet.CreateFor(stage, extraProfiles);
    }

    // ステージの終了時に1回呼ぶ
    public static void Unload()
    {
        Current?.Dispose();
        Current = null;
    }

    // 再生を始める前に、前の状態を消す(Domain Reloadを切っている場合の対策)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Current = null;
    }
}
