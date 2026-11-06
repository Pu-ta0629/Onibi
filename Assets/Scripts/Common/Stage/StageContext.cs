using UnityEngine;

/// <summary>
/// ステージの生成に共通して必要な参照のまとめ（StageGeneratorが作り、各Spawnerへ渡す）
/// 各Spawnerが必要な参照を個別に探さなくてよいように、ここへ足していく
/// </summary>
public sealed class StageContext
{
    public HitWorld world { get; }
    public LostChildController lostChild { get; }
    public Camera mainCamera { get; }

    public StageContext(HitWorld world, LostChildController lostChild, Camera mainCamera)
    {
        this.world = world;
        this.lostChild = lostChild;
        this.mainCamera = mainCamera;
    }
}

/// <summary>
/// ステージの生成を担当するクラス（FieldSpawner・EntitySpawnerなど）が実装する
/// StageGeneratorが、StageContextを渡して順番にInitializeを呼ぶ
/// </summary>
public interface IStageSpawner
{
    void Initialize(StageContext context);
}
