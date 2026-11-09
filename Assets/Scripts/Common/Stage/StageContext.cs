using UnityEngine;

/// <summary>
/// ステージの生成に共通して必要な参照のまとめ（StageGeneratorが作り、各Spawnerへ渡す）
/// 各Spawnerが必要な参照を個別に探さなくてよいように、ここへ足していく
/// </summary>
public sealed class StageContext
{
    [Tooltip("未設定ならnull(Scene上の地形をそのまま使う)")]
    public StageData Stage { get; }
    public HitWorld World { get; }
    public LostChildController LostChildController { get; }
    [Tooltip("MainCamera：ゲーム通して一つしかないカメラ")]
    public Camera MainCamera { get; }
    [Tooltip("生成物の親Obj")]
    public Transform Root { get; }

    public StageContext(StageData stage, HitWorld world, LostChildController lostChild, Camera mainCamera, Transform root)
    {
        this.Stage = stage;
        this.World = world;
        this.LostChildController = lostChild;
        this.MainCamera = mainCamera;
        this.Root = root;
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
