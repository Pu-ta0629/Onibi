using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵を湧かせるクラス。EnemyPoolを作り、指定した湧きポイントから、一定間隔で1体ずつ敵を出す
/// 湧きポイントは順番に使う。最大数はPoolCount（全員が出ている間は、湧かない）
/// 敵のクラス（Enemy_???）は、EnemyDataの種族（EnemyTypeData.enemyType）から選ぶ
/// 迷子の座標は、ここで毎フレーム1回だけ読んで、全ての敵へ渡す
/// </summary>
public class EnemyGenerator : MonoBehaviour
{
    [Header("Enemy")]
    [Tooltip("敵のPrefab。NavMeshAgentを付けておく（Agentのチェックは外しておくと、生成時の警告が出ない）")]
    [SerializeField] private NavMeshAgent _enemyPrefab;

    [Tooltip("この敵のマスターデータ。種族（Type）を設定しておくと、種族に合ったEnemy_???が使われる")]
    [SerializeField] private EnemyData _enemyData;

    [SerializeField] private EnemyMoveConfig _moveConfig = new EnemyMoveConfig();

    [Header("Spawn")]
    [Tooltip("敵が湧く位置。順番に使う")]
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("湧く間隔（秒）")]
    [SerializeField, Range(0.1f, 60f)] private float _spawnInterval = 5f;

    [Tooltip("敵の最大数（プールの数）。これ以上は、同時に湧かない")]
    [SerializeField, Range(1, 200)] private int _poolCount = 10;

    private EnemyPool _pool;
    private LostChildController _lostChild;
    private float _timer;
    private int _nextPointIndex;
    private bool _isInitialized;

    public EnemyPool pool => _pool;

    #region Initialize
    /// <summary>
    /// InitializeManagerから呼ぶ。プールを作る
    /// </summary>
    public void Initialize(HitWorld world, LostChildController lostChild)
    {
        if (world == null || lostChild == null || _enemyPrefab == null || _enemyData == null)
        {
            Debug.LogError("[EnemyGenerator] Initialize : Reference is Null!!! ");
            return;
        }

        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("[EnemyGenerator] Initialize : SpawnPoints is Empty!!! ");
            return;
        }

        //種族が未設定なら、Kappaとして扱う
        EnemyType type = EnemyType.Kappa;
        if (_enemyData.Type != null)
        {
            type = _enemyData.Type.enemyType;
        }
        else
        {
            Debug.LogWarning($"[EnemyGenerator] Initialize : EnemyData.Type is Null!! Kappa is used. data = {_enemyData.name}");
        }

        _lostChild = lostChild;
        _pool = new EnemyPool(_enemyPrefab, transform, _enemyData, _moveConfig, world, _poolCount, () => EnemyFactory.Create(type));
        _isInitialized = true;
    }
    #endregion

    #region UnityLifeCycle
    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            SpawnOne();
        }

        _pool.Tick(_lostChild.transform.position);
    }

    private void OnDestroy()
    {
        _pool?.Dispose();
    }
    #endregion

    #region Private
    //次の湧きポイントから、敵を1体出す。プールが空なら出さない
    private void SpawnOne()
    {
        Transform point = _spawnPoints[_nextPointIndex];
        _nextPointIndex = (_nextPointIndex + 1) % _spawnPoints.Length;

        if (point == null)
        {
            Debug.LogWarning("[EnemyGenerator] SpawnOne : SpawnPoint is Null!!");
            return;
        }

        _pool.TrySpawn(point.position, out EnemyController _);
    }
    #endregion
}