using UnityEngine;

/// <summary>
/// ステージの生成をまとめるクラス。FieldSpawner（地形）→ EntitySpawner（敵など）の順にInitializeを呼ぶ
/// 各Spawnerが共通で必要な参照（HitWorld・迷子・カメラ）は、ここでStageContextにまとめて渡す
/// 生成するものの種類を増やすときは、IStageSpawnerを実装したクラスを足して、ここで呼ぶ
/// </summary>
public class StageGenerator
{
    private HitWorld _hitWorld;
    private Camera _mainCamera;
    private StageContext _context;
    private FieldSpawnManager _fieldSpawnManager;
    private EntitySpawnManager _entitySpawnManager;


    public StageGenerator(
        HitWorld hitWorld,
        Camera mainCamera,
        StageContext context,
        FieldSpawnManager fieldSpawnManager,
        EntitySpawnManager entitySpawnManager)
    {
        if (hitWorld == null || mainCamera == null || context == null || fieldSpawnManager == null || entitySpawnManager == null)
        {
            Debug.LogError("[StageGenerator] Initialize : Reference is Null!!!!");
            return;
        }

        _hitWorld = hitWorld;
        _mainCamera = mainCamera;
        _context = context;
        _fieldSpawnManager = fieldSpawnManager;
        _entitySpawnManager = entitySpawnManager;
    }

    public void Generate()
    {
        if (_context == null)
        {
            return;
        }

        _fieldSpawnManager.Initialize(_context);
        SpawnLostChild();

        // 地形（NavMesh）ができた後に呼ぶ。中でPoolManager.Buildを呼んで、敵を生成する
        _entitySpawnManager.Build(_context);
    }

    private void SpawnLostChild()
    {
        if (_context.Stage.Start == null)
        {
            Debug.LogError("StageDataにStart情報がありません");
            return;
        }

        _context.LostChildController.SpawnAt(_context.Stage.Start.Position);
    }
}
