using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵などのEntityの生成を担当するクラス（例：EntitySpawner + 敵SpawnManager）
/// StageDataをStageManagerからStageContext経由で受け取る
/// 敵の在庫（PoolManager）を管理し、各スポットごとに一定間隔で敵を出現させる
/// 在庫切れでfalseを返したらスポットは停止。PoolManagerの再入荷通知で再び出現させる
/// 迷子の座標を取得して敵全員に渡す（敵はNavMeshAgentで迷子へ移動する）
/// FieldSpawnManagerより後でInitializeすること（NavMeshができていないと、スポーン位置を決められない）
/// </summary>
public class EntitySpawnManager : MonoBehaviour, IStageSpawner
{
    private class SpawnEntry
    {
        public int PoolId;
        public Vector3 Center;
        public float Radius;
        public float Interval;
        public float Timer;
        public bool Stopped;
    }

    private const float MinInterval = 0.01f;

    [Header("Pool")]
    [Tooltip("敵の在庫を管理するPoolManager（同じObjectに付けてよい）")]
    [SerializeField] private PoolManager _poolManager;

    [Header("Spawn")]
    [Tooltip("スポーン位置を決めるとき、スポットの円の中で通れる場所を探す回数")]
    [SerializeField, Min(1)] private int _pickTryCount = 5;

    [Tooltip("ステージ開始時に、StageDataに位置が指定して配置された敵を出現させる")]
    [SerializeField] private bool _spawnPlacedOnStart = true;

    private readonly List<SpawnEntry> _entries = new();

    private Transform _poolParent;
    private Transform _target;
    private bool _isInitialized;

    #region Initialize
    /// <summary>
    /// StageManagerから一度だけ呼ぶ。StageDataから敵の在庫と敵スポットを構築する
    /// 追従対象（Target）は、迷子で固定
    /// </summary>
    public void Initialize(StageContext context)
    {
        if (context == null || context.World == null || context.LostChildController == null || _poolManager == null)
        {
            Debug.LogError($"[EntitySpawnManager] Initialize : Reference is Null!!! poolManager = {_poolManager == null} (Inspectorで割り当てる)");
            return;
        }

        //再Initializeに備えて、前の状態を片づける
        Shutdown();

        StageData stage = context.Stage;
        if (stage == null)
        {
            Debug.Log("[EntitySpawnManager] Initialize : Stage is not set. Entity is not spawned");
            return;
        }

        if (StageNavMeshProvider.Current == null)
        {
            Debug.LogWarning("[EntitySpawnManager] Initialize : NavMesh is not ready. FieldSpawnManager must be initialized first!!");
        }

        _target = context.LostChildController.transform;

        //敵の親Objを、StageManagerの下に作成
        _poolParent = new GameObject("Entity_Enemies").transform;
        _poolParent.SetParent(context.Root);

        _poolManager.Build(stage, context.World, _poolParent);

        BuildEntries(stage);

        if (_spawnPlacedOnStart)
        {
            _poolManager.SpawnPlacedEnemies();
        }

        _isInitialized = true;

#if UNITY_EDITOR
        //動かないときの原因調べ用。敵が作れているか、スポットがあるかを1回だけ出す
        Debug.Log($"[EntitySpawnManager] Initialize : spawnSpots = {_entries.Count}, navMesh = {(StageNavMeshProvider.Current != null ? "ready" : "NOT READY")}\n{_poolManager.DescribeStocks()}");
#endif
    }
    #endregion

    #region UnityLifeCycle
    private void OnEnable()
    {
        if (_poolManager != null)
        {
            _poolManager.Restocked += OnRestocked;
        }
    }

    private void OnDisable()
    {
        if (_poolManager != null)
        {
            _poolManager.Restocked -= OnRestocked;
        }
    }

    private void Update()
    {
        if (!_isInitialized || _target == null)
        {
            return;
        }

        UpdateSpawnTimers();
        _poolManager.Tick(_target.position);
    }

    private void OnDestroy()
    {
        Shutdown();
    }
    #endregion

    #region Public
    /// <summary>出現中の敵を全員プールへ戻す（全滅処理）</summary>
    public void ReturnAll()
    {
        if (_poolManager != null)
        {
            _poolManager.ReturnAll();
        }
    }

    /// <summary>位置を指定した敵（StageDataのEnemies）を、全員出現させる</summary>
    public void SpawnPlacedEnemies()
    {
        if (_poolManager != null)
        {
            _poolManager.SpawnPlacedEnemies();
        }
    }

    /// <summary>敵の生成を停止して、敵のGameObjectを全て破棄する（ステージを閉じるとき）</summary>
    public void Shutdown()
    {
        _isInitialized = false;

        //PoolManagerはInspectorで割り当てた参照なので、nullにはしない（再Initializeで使う）
        if (_poolManager != null)
        {
            _poolManager.Clear();
        }

        if (_poolParent != null)
        {
            Destroy(_poolParent.gameObject);
            _poolParent = null;
        }

        _entries.Clear();
        _target = null;
    }
    #endregion

#if UNITY_EDITOR
    #region Debug
    /// <summary>Editor確認用。Poolの在庫の状態を文字列にする</summary>
    public string DescribeStocks()
    {
        return _poolManager != null ? _poolManager.DescribeStocks() : "not initialized";
    }

    /// <summary>Editor確認用。出現中の敵が、StageDataから作ったNavMeshの上で、NavMeshAgentにより動いているかを数える</summary>
    public string DescribeNavStates()
    {
        EnemyController[] enemies = _poolParent != null ? _poolParent.GetComponentsInChildren<EnemyController>() : new EnemyController[0];

        int active = 0;
        int onMesh = 0;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < enemies.Length; i++)
        {
            if (!enemies[i].isActive)
            {
                continue;
            }

            active++;
            if (enemies[i].isOnExpectedNavMesh)
            {
                onMesh++;
            }
            else
            {
                builder.AppendLine($"  NG {enemies[i].name}: {enemies[i].DescribeNavState()}");
            }
        }

        return $"出現中の敵 {active}体 / NavMeshAgentで動いている {onMesh}体\n{builder}";
    }
    #endregion
#endif

    #region Private
    //StageDataからEnemyのスポーンスポットを読み取る
    private void BuildEntries(StageData stage)
    {
        _entries.Clear();

        foreach (var spot in stage.SpawnSpots)
        {
            if (spot.Kind != SpawnKind.Enemy)
            {
                continue;
            }

            SpawnEntry entry = new SpawnEntry();
            entry.PoolId = spot.Id;
            entry.Center = spot.Position;
            entry.Radius = spot.Radius;
            entry.Interval = Mathf.Max(MinInterval, spot.SpawnInterval);
            _entries.Add(entry);
        }
    }

    private void UpdateSpawnTimers()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            SpawnEntry entry = _entries[i];
            if (entry.Stopped)
            {
                continue;
            }

            entry.Timer += Time.deltaTime;
            if (entry.Timer < entry.Interval)
            {
                continue;
            }

            entry.Timer = 0f;
            Spawn(entry);
        }
    }

    private void Spawn(SpawnEntry entry)
    {
        if (!_poolManager.TryRequest(entry.PoolId, out EnemyController enemy))
        {
            // 在庫切れ。再開の通知が来るまで、このスポットは停止
            Debug.LogWarning($"[EntitySpawnManager] Spawn : Pool is empty or not found. spot {entry.PoolId} is stopped (EnemyList / Count is set in StageData?)");
            entry.Stopped = true;
            return;
        }

        // 出現させる敵の位置を、スポットの円の中から選ぶ
        if (!TryPickSpawnPosition(entry, enemy, out Vector3 position))
        {
            // 通れる場所がなければ、NavMeshの外に出さない。プールへ戻して次の間隔で再度試す
            Debug.LogWarning($"[EntitySpawnManager] Spawn : NavMesh is not found in spot {entry.PoolId} ({enemy.name}). Returned to pool");
            _poolManager.Return(enemy);
            return;
        }

        enemy.Spawn(position);
        //Debug.Log($"[EntitySpawnManager] Spawn : {enemy.name} spawned at {position} (spot {entry.PoolId})");
    }

    private bool TryPickSpawnPosition(SpawnEntry entry, EnemyController enemy, out Vector3 position)
    {
        for (int i = 0; i < _pickTryCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * entry.Radius;
            Vector3 point = entry.Center + new Vector3(offset.x, 0f, offset.y);

            if (enemy.TryFindSpawnPosition(point, out position))
            {
                return true;
            }
        }

        position = entry.Center;
        return false;
    }

    private void OnRestocked(int poolId)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].PoolId == poolId)
            {
                _entries[i].Stopped = false;
            }
        }
    }
    #endregion
}