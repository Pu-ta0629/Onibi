using UnityEngine;

/// <summary>
/// ステージの生成をまとめるクラス。FieldSpawner（地形）→ EntitySpawner（敵など）の順にInitializeを呼ぶ
/// 各Spawnerが共通で必要な参照（HitWorld・迷子・カメラ）は、ここでStageContextにまとめて渡す
/// 生成するものの種類を増やすときは、IStageSpawnerを実装したクラスを足して、ここで呼ぶ
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("Stage")]
    [Tooltip("生成するステージ情報格納変数　各SpawnerでStageContext経由で渡す")]
    [SerializeField] private StageData _stage;

    [Header("SpawnerManager")]
    [Tooltip("地形（フィールド）の生成")]
    [SerializeField] private FieldSpawnManager _fieldSpawnManager;
    [Tooltip("敵などの生成")]
    [SerializeField] private EntitySpawnManager _entitySpawnManager;

    private StageContext _context;

    public StageContext context => _context;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。共通の参照をまとめて、各Spawnerを初期化する
    /// </summary>
    public void Initialize(HitWorld world, LostChildController lostChild, Camera mainCamera)
    {
        if (world == null || lostChild == null || mainCamera == null || _fieldSpawnManager == null || _entitySpawnManager == null)
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
        _fieldSpawnManager.Initialize(_context);
        SpawnLostChild();
        _entitySpawnManager.Initialize(_context);
    }
    #endregion

    #region private
    //迷子をStageDataのStartの位置へ置く（Stage未設定のときは、Scene上の位置のまま）
    private void SpawnLostChild()
    {
        if (_stage == null)
        {
            return;
        }

        _context.LostChildController.SpawnAt(_stage.Start.Position);
    }
    #endregion
}