using UnityEngine;
using UnityEngine.AI;

/// <summary>敵を出すときの種族の選び方</summary>
public enum SpawnMode
{
    Random,     //出せる種族からランダムに選ぶ
    Specified   //指定した種族だけを出す
}

/// <summary>
/// 敵などのEntityを湧かせるクラス（旧EnemyGeneratorを改変）
/// プールの管理はEnemyPoolManagerに任せ、ここは「いつ・どこに・どの種族を」出すかだけを決める
/// StageDataはStageGeneratorからStageContext経由で受け取り、Enemiesの配置どおりに最初に出す
/// 自動湧き（デバッグ用）は、Transformの湧きポイントを順番に使う
/// 迷子の座標は、ここで毎フレーム1回だけ読んで、全ての敵へ渡す
/// </summary>
public class EntitySpawner : MonoBehaviour, IStageSpawner
{
    [Header("Pool")]
    [Tooltip("種族ごとの敵のプール。Initialize時に、ここの初期化もまとめて行う")]
    [SerializeField] private EnemyPoolManager _enemyPoolManager;

    [Header("Spawn")]
    [Tooltip("Random=出せる種族からランダム / Specified=下のSpecified Typeだけ")]
    [SerializeField] private SpawnMode _spawnMode = SpawnMode.Random;

    [Tooltip("Spawn ModeがSpecifiedのときに出す種族")]
    [SerializeField] private EnemyType _specifiedType = EnemyType.Kappa;

    [Tooltip("自動で湧かせる間隔（秒）")]
    [SerializeField, Range(0.1f, 60f)] private float _spawnInterval = 5f;

    [Header("Debug")]
    [Tooltip("trueの間、一定間隔で自動で湧かせる（falseなら、StageDataの配置と、Spawn・SpawnRandomを呼んだときだけ）")]
    [SerializeField] private bool _autoSpawn = true;

    [Tooltip("自動湧き用の湧きポイント。順番に使う（Auto Spawnがtrueのときだけ必要）")]
    [SerializeField] private Transform[] _debugSpawnPoints;

    private LostChildController _lostChild;
    private float _timer;
    private int _nextPointIndex;
    private bool _isInitialized;

    public EnemyPoolManager enemyPoolManager => _enemyPoolManager;

    #region Initialize
    /// <summary>
    /// StageGeneratorから一度だけ呼ぶ。敵のプールを作り、StageDataの配置どおりに敵を出す
    /// </summary>
    public void Initialize(StageContext context)
    {
        if (context == null || context.World == null || context.LostChildController == null || _enemyPoolManager == null)
        {
            Debug.LogError("[EntitySpawner] Initialize : Reference is Null!!! ");
            return;
        }

        //自動湧きを使うときだけ、湧きポイントが必要
        if (_autoSpawn && (_debugSpawnPoints == null || _debugSpawnPoints.Length == 0))
        {
            Debug.LogError("[EntitySpawner] Initialize : DebugSpawnPoints is Empty!!! ");
            return;
        }

        _enemyPoolManager.Initialize(context.World);
        if (!_enemyPoolManager.isInitialized)
        {
            Debug.LogError("[EntitySpawner] Initialize : EnemyPoolManager is not initialized!!! ");
            return;
        }

        _lostChild = context.LostChildController;
        _isInitialized = true;

        SpawnPlacements(context.Stage);
    }
    #endregion

    #region UnityLifeCycle
    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (_autoSpawn)
        {
            _timer += Time.deltaTime;
            if (_timer >= _spawnInterval)
            {
                _timer = 0f;
                SpawnByMode();
            }
        }

        _enemyPoolManager.Tick(_lostChild.transform.position);
    }
    #endregion

    #region Public
    /// <summary>
    /// 指定した種族の敵を、次の湧きポイントへ1体出す。出せなければfalse
    /// </summary>
    public bool Spawn(EnemyType type)
    {
        if (!_isInitialized || !TryGetSpawnPosition(out Vector3 position))
        {
            return false;
        }

        return _enemyPoolManager.TrySpawn(type, position, out EnemyController _);
    }

    /// <summary>
    /// 出せる種族からランダムに選んで、次の湧きポイントへ1体出す。出せなければfalse
    /// </summary>
    public bool SpawnRandom()
    {
        if (!_isInitialized || !TryGetSpawnPosition(out Vector3 position))
        {
            return false;
        }

        return _enemyPoolManager.TrySpawnRandom(position, out EnemyController _);
    }
    #endregion

    #region Private
    //StageDataのEnemiesの配置どおりに、敵を出す
    //プールは種族ごとのEnemyPoolEntryのDataで作られるので、ここでは種族（EnemyType）だけを使う
    //TODO 向き（RotationY）と大きさ（Size）の反映は、EnemyController.Spawnの対応後
    private void SpawnPlacements(StageData stage)
    {
        if (stage == null)
        {
            return;
        }

        for (int i = 0; i < stage.Enemies.Count; i++)
        {
            EnemyPlacement placement = stage.Enemies[i];
            if (placement.Enemy == null || placement.Enemy.Type == null)
            {
                Debug.LogWarning($"[EntitySpawner] SpawnPlacements : Enemy{i + 1} is not set!!");
                continue;
            }

            if (!_enemyPoolManager.TrySpawn(placement.Enemy.Type.enemyType, placement.Position, out EnemyController _))
            {
                Debug.LogWarning($"[EntitySpawner] SpawnPlacements : Failed to spawn Enemy{i + 1}!!");
            }
        }
    }

    //Inspectorで選んだSpawnModeに従って、1体出す
    private void SpawnByMode()
    {
        if (_spawnMode == SpawnMode.Specified)
        {
            Spawn(_specifiedType);
            return;
        }

        SpawnRandom();
    }

    //次の湧きポイントの座標を返し、順番を進める
    private bool TryGetSpawnPosition(out Vector3 position)
    {
        position = default;

        if (_debugSpawnPoints == null || _debugSpawnPoints.Length == 0)
        {
            return false;
        }

        Transform point = _debugSpawnPoints[_nextPointIndex];
        _nextPointIndex = (_nextPointIndex + 1) % _debugSpawnPoints.Length;

        if (point == null)
        {
            Debug.LogWarning("[EntitySpawner] TryGetSpawnPosition : SpawnPoint is Null!!");
            return false;
        }

        position = point.position;
        return true;
    }
    #endregion
}