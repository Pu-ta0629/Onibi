using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 種族ごとのEnemyPoolをまとめて管理するクラス
/// 敵を出すときは、種族を指定する（TrySpawn）か、出せる種族からランダムに選ぶ（TrySpawnRandom）
/// 新しい種族を足すときは、EnemyPoolEntryをInspectorで足すだけでよい（EnemyFactoryにclassを足すのは別）
/// 投げ物・木霊などのObject用Poolは、PoolBaseを継承して別のManagerとして足す
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    [Header("Pool")]
    [Tooltip("敵の種族ごとのプール。同じ種族は1つだけ登録できる")]
    [SerializeField] private EnemyPoolEntry[] _entries;

    [Header("Move")]
    [Tooltip("敵の移動の調整値（全ての敵で共通）")]
    [SerializeField] private EnemyMoveConfig _moveConfig = new EnemyMoveConfig();

    private readonly Dictionary<EnemyType, EnemyPool> _pools = new Dictionary<EnemyType, EnemyPool>();
    private readonly List<EnemyPool> _poolList = new List<EnemyPool>();
    private readonly List<EnemyPool> _candidates = new List<EnemyPool>();
    private bool _isInitialized;

    public bool isInitialized => _isInitialized;
    public int poolTypeCount => _poolList.Count;

    /// <summary>いまステージに出ている敵の総数</summary>
    public int activeCount
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _poolList.Count; i++)
            {
                sum += _poolList[i].activeCount;
            }

            return sum;
        }
    }

    #region Initialize
    /// <summary>
    /// EntitySpawnerから一度だけ呼ぶ。設定した種族ごとにプールを作る
    /// </summary>
    public void Initialize(HitWorld world)
    {
        if (world == null)
        {
            Debug.LogError("[EnemyPoolManager] Initialize : HitWorld is Null!!! ");
            return;
        }

        if (_entries == null || _entries.Length == 0)
        {
            Debug.LogError("[EnemyPoolManager] Initialize : Entries is Empty!!! ");
            return;
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            CreatePool(_entries[i], i, world);
        }

        _isInitialized = _poolList.Count > 0;
        Debug.Log($"[EnemyPoolManager] Initialize : poolTypeCount = {_poolList.Count}");
    }

    private void CreatePool(EnemyPoolEntry entry, int index, HitWorld world)
    {
        if (entry == null || !entry.isValid)
        {
            Debug.LogWarning($"[EnemyPoolManager] CreatePool : Entry is invalid (Prefab / Data / Data.Type)!! index = {index}");
            return;
        }

        if (_pools.ContainsKey(entry.type))
        {
            Debug.LogWarning($"[EnemyPoolManager] CreatePool : {entry.type} is already registered!! index = {index}");
            return;
        }

        Transform parent = new GameObject($"Object_Pool_Enemy_{entry.type}").transform;
        parent.SetParent(transform, false);

        EnemyPool pool = new EnemyPool(entry, parent, _moveConfig, world);
        _pools.Add(entry.type, pool);
        _poolList.Add(pool);
    }
    #endregion

    #region Public
    /// <summary>
    /// 種族が登録されているか
    /// </summary>
    public bool Has(EnemyType type)
    {
        return _pools.ContainsKey(type);
    }

    /// <summary>
    /// 指定した種族の敵を1体出す。未登録・全員が出ていて出せなければfalse
    /// </summary>
    public bool TrySpawn(EnemyType type, Vector3 position, out EnemyController enemy)
    {
        enemy = null;
        if (!_isInitialized)
        {
            return false;
        }

        if (!_pools.TryGetValue(type, out EnemyPool pool))
        {
            Debug.LogWarning($"[EnemyPoolManager] TrySpawn : {type} is not registered!!");
            return false;
        }

        return pool.TrySpawn(position, out enemy);
    }

    /// <summary>
    /// 出せる種族の中からランダムに1種族選んで、敵を1体出す。全ての種族が出し切っていればfalse
    /// </summary>
    public bool TrySpawnRandom(Vector3 position, out EnemyController enemy)
    {
        enemy = null;
        if (!_isInitialized)
        {
            return false;
        }

        _candidates.Clear();
        for (int i = 0; i < _poolList.Count; i++)
        {
            if (_poolList[i].hasFree)
            {
                _candidates.Add(_poolList[i]);
            }
        }

        if (_candidates.Count == 0)
        {
            return false;
        }

        EnemyPool pool = _candidates[Random.Range(0, _candidates.Count)];
        return pool.TrySpawn(position, out enemy);
    }

    /// <summary>
    /// 出ている全ての敵を、1フレーム進める。迷子の座標は、呼ぶ側が1回だけ読んで渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        if (!_isInitialized)
        {
            return;
        }

        for (int i = 0; i < _poolList.Count; i++)
        {
            _poolList[i].Tick(targetPosition);
        }
    }

    /// <summary>
    /// 出ている全ての敵をプールへ戻す（リトライ・ステージ切り替え用）
    /// </summary>
    public void ReleaseAll()
    {
        for (int i = 0; i < _poolList.Count; i++)
        {
            _poolList[i].ReleaseAll();
        }
    }
    #endregion

    #region UnityLifeCycle
    private void OnDestroy()
    {
        for (int i = 0; i < _poolList.Count; i++)
        {
            _poolList[i].Dispose();
        }

        _poolList.Clear();
        _pools.Clear();
    }
    #endregion
}
