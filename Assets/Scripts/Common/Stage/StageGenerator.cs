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
        if(hitWorld == null ||mainCamera == null || context == null || fieldSpawnManager == null || entitySpawnManager == null)
        {
            Debug.LogError("[StageGenerator] Initialize : Reference is Null!!!!");
            return;
        }

        if(mainCamera == null)
        {
            Debug.LogWarning("[StageGenerator] Initialize : Reference Error mainCamera is Null!!!");
            _mainCamera = Camera.main;
        }

        _hitWorld = hitWorld;
        _mainCamera = mainCamera;
        _context = context;
        _fieldSpawnManager = fieldSpawnManager;
        _entitySpawnManager = entitySpawnManager;
    }

    public void Generate()
    {
        _fieldSpawnManager.Initialize(_context);
        SpawnLostChild();
        _entitySpawnManager.Build(_context.Stage);
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

   /* [Header("Stage")]
    [Tooltip("生成するステージ情報格納変数　各SpawnerでStageContext経由で渡す")]
    [SerializeField] private StageData _stage;

    [Header("Spawner")]
    [Tooltip("地形（フィールド）の生成")]
    [SerializeField] private FieldSpawner _fieldSpawner;
    [Tooltip("敵などの生成")]
    //[SerializeField] private EntitySpawner _entitySpawner;

    private StageContext _context;

    public StageContext context => _context;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。共通の参照をまとめて、各Spawnerを初期化する
    /// </summary>
    public void Initialize(HitWorld world, LostChildController lostChild, Camera mainCamera)
    {
        if (world == null || lostChild == null || mainCamera == null || _fieldSpawner == null*//* || _entitySpawner == null*//*)
        {
            Debug.LogError("[StageGenerator] Initialize : Reference is Null!!! ");
            return;
        }

        //Stage未設定でも、各Spawnerの初期化は続ける（Scene上の地形と、デバッグ用の湧きポイントを使う）
        if (_stage == null)
        {
            Debug.LogWarning("[StageGenerator] Initialize : Stage is not set. Scene field is used");
        }

        _context = new StageContext(_stage, world, lostChild, mainCamera, transform);

        //地形が先、迷子はNavMeshができてから置く、その後に敵などを出す
        _fieldSpawner.Initialize(_context);
        SpawnLostChild();
       // _entitySpawner.Initialize(_context);
    }
    */
    //迷子をStageDataのStartの位置へ置く（Stage未設定のときは、Scene上の位置のまま）
}