using UnityEngine;

/// <summary>
/// ステージの生成をまとめるクラス。FieldSpawner（地形）→ EntitySpawner（敵など）の順にInitializeを呼ぶ
/// 各Spawnerが共通で必要な参照（HitWorld・迷子・カメラ）は、ここでStageContextにまとめて渡す
/// 生成するものの種類を増やすときは、IStageSpawnerを実装したクラスを足して、ここで呼ぶ
/// </summary>
public class StageGenerator : MonoBehaviour
{
    [Header("Spawner")]
    [Tooltip("地形（フィールド）の生成")]
    [SerializeField] private FieldSpawner _fieldSpawner;

    [Tooltip("敵などの生成")]
    [SerializeField] private EntitySpawner _entitySpawner;

    private StageContext _context;

    public StageContext context => _context;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。共通の参照をまとめて、各Spawnerを初期化する
    /// </summary>
    public void Initialize(HitWorld world, LostChildController lostChild, Camera mainCamera)
    {
        if (world == null || lostChild == null || mainCamera == null || _fieldSpawner == null || _entitySpawner == null)
        {
            Debug.LogError("[StageGenerator] Initialize : Reference is Null!!! ");
            return;
        }

        _context = new StageContext(world, lostChild, mainCamera);

        //地形が先、その上に出す敵などが後
        _fieldSpawner.Initialize(_context);
        _entitySpawner.Initialize(_context);
    }
    #endregion
}
