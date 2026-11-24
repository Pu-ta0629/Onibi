using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StageDataのEnemyの湧きスポットごとに、一定間隔でPoolManagerへ申請して敵を出す
/// Buildで、PoolManagerへ敵の生成（Pool構築）を発注し、位置指定の敵も出す
/// 在庫切れでfalseが返ったスポットは止まり、PoolManagerの再開通知で動き出す
/// 出ている敵のTickも、ここから毎フレームPoolManagerへ渡す
/// </summary>
public class EntitySpawnManager : MonoBehaviour
{
    private class SpawnEntry
    {
        public int PoolId;
        public Vector3 Center;
        public float Radius;
        public float Interval;

        public float Timer;
        public bool Stopped;
        public bool IsKodama;
        public int Capacity;
    }

    [SerializeField] private PoolManager _poolManager;

    [Tooltip("湧く位置を決めるとき、スポットの円の中で通れる場所を探す回数")]
    [SerializeField, Min(1)] private int _pickTryCount = 5;

    [Header("木霊")]
    [Tooltip("木霊の出現位置を探すとき、円の中の点から、迷子のNavMeshまでを、通れる道とみなす距離")]
    [SerializeField,Min(0.01f)] private float _kodamaSampleDistance = 0.5f;
    [Tooltip("木霊の出現位置を捜す回数")]
    [SerializeField, Min(1)] private int _kodamaPickTryCount = 20;
    private readonly List<SpawnEntry> _entries = new();
    private LostChildController _lostChild;
    private bool _isStopped;
    private bool _isBuilt;

    #region Build
    /// <summary>
    /// StageGeneratorから呼ぶ。PoolManagerへ敵の生成を発注し、StageDataからEnemyの湧きスポットを読み取る
    /// FieldSpawnManager.Initializeの後（NavMeshができた後）に呼ぶこと
    /// </summary>
    public void Build(StageContext context)
    {
        if (context == null || context.Stage == null || context.World == null || _poolManager == null)
        {
            Debug.LogError($"[EntitySpawnManager] Build : Reference is Null!!! context = {context == null}, poolManager = {_poolManager == null}");
            return;
        }

        _entries.Clear();
        _isStopped = false;
        _lostChild = context.LostChildController;

        // 敵を生成してPoolに仕分ける（これを呼ばないと、敵が1体も作られない）
        _poolManager.Build(context.Stage, context.World, context.Root);
        _poolManager.BuildKodama(context.Stage,context.World,context.Root);

        foreach (SpawnSpotPlacement spot in context.Stage.SpawnSpots)
        {
            bool isEnemy = spot.Kind == SpawnKind.Enemy;
            bool isKodama = spot.Kind == SpawnKind.Kodama;
            if(!isEnemy && !isKodama)
            {
                continue;
            }

            SpawnEntry entry = new SpawnEntry();
            entry.PoolId = spot.Id;
            entry.Center = spot.Position;
            entry.Radius = spot.Radius;
            entry.Interval = spot.SpawnInterval;
            entry.IsKodama = isKodama;
            entry.Capacity = isKodama ? spot.KodamaCount : 0;
            _entries.Add(entry);
        }

        // 木霊は、開始時に、上限まで、スポットに点在させる。出し切れなかった分は、間隔ごとに、１つずつ出す
        for(int i = 0; i < _entries.Count;i++)
        {
            SpawnEntry entry = _entries[i];
            if(!entry.IsKodama)
            {
                continue;
            }

            int spawned = 0;
            for (int n = 0; n < entry.Capacity; n++)
            {
                if(!SpawnKodama(entry))
                {
                    break;
                }
                spawned++;
            }

            if(spawned < entry.Capacity)
            {
                Debug.Log($"[EntitySpawnManager] Build : Kodama spawned {spawned} / {entry.Capacity}. SpawnSpot Id = {entry.PoolId} (not enough walkable space, or the Pool is smaller)");
            }
        }
        // 位置指定で配置した敵は、最初に全員出す
        _poolManager.SpawnPlacedEnemies();
        _isBuilt = true;
    }
    #endregion

    #region Public
    public void Stop()
    {
        _isStopped = true;
    }
    #endregion

    #region UnityLifeCycle
    private void OnEnable()
    {
        _poolManager.Restocked += OnRestocked;
        _poolManager.KodamaRestocked += OnKodamaRestocked;
    }

    private void OnDisable()
    {
        _poolManager.Restocked -= OnRestocked;
        _poolManager.KodamaRestocked -= OnKodamaRestocked;
    }

    private void OnDestroy()
    {
        if (_isBuilt)
        {
            _poolManager.Clear();
        }
    }

    private void Update()
    {
        if (!_isBuilt || _isStopped)
        {
            return;
        }

        // 出ている敵を動かす（迷子の座標を1回だけ読んで、全員に渡す）
        if (_lostChild != null)
        {
            _poolManager.Tick(_lostChild.transform.position);
        }

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
    #endregion

    #region Private
    private void Spawn(SpawnEntry entry)
    {
        if(entry.IsKodama)
        {
            SpawnKodama(entry);
            return;
        }

        if (!_poolManager.TryRequest(entry.PoolId, out EnemyController enemy))
        {
            entry.Stopped = true;
            return;
        }

        // 出てきた敵が通れる場所を、スポットの円の中から選ぶ
        enemy.Spawn(PickSpawnPosition(entry, enemy));
    }

    private bool SpawnKodama(SpawnEntry entry)
    {
        if(!TryPickKodamaPosition(entry,out Vector3 position))
        {
            return false;
        }

        if(!_poolManager.TryRequestKodama(entry.PoolId,position))
        {
            entry.Stopped = true;
            return false;
        }

        return true;
    }

    private bool TryPickKodamaPosition(SpawnEntry entry,out Vector3 result)
    {
        result = entry.Center;
        if(_lostChild == null)
        {
            return false;
        }

        // 迷子の体の大きさ分、間隔をあける
        float extraDistance = _lostChild.LostChild != null ? _lostChild.LostChild.Colliders.BodyBoundingRadius * 2f : 0f;
        float radiusSqr = entry.Radius * entry.Radius;

        for(int i = 0; i < _kodamaPickTryCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * entry.Radius;
            Vector3 point = entry.Center + new Vector3(offset.x, 0f, offset.y);

            if(!_lostChild.TryFindWalkablePoint(point,_kodamaSampleDistance,out Vector3 onNavMesh))
            {
                continue;
            }

            float dx = onNavMesh.x - entry.Center.x;
            float dz = onNavMesh.z - entry.Center.z;
            if(dx * dx + dz * dz >radiusSqr)
            {
                continue;
            }

            if (!_poolManager.IsKodamaSpaceFree(entry.PoolId, onNavMesh, extraDistance))
            {
                continue;
            }

            result = onNavMesh;
            return true;

        }

        return false;
    }

    private Vector3 PickSpawnPosition(SpawnEntry entry, EnemyController enemy)
    {
        Vector3 point = entry.Center;

        for (int i = 0; i < _pickTryCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * entry.Radius;
            point = entry.Center + new Vector3(offset.x, 0f, offset.y);

            if (enemy.TryFindSpawnPosition(point, out Vector3 onNavMesh))
            {
                return onNavMesh;
            }
        }
        return point;
    }

    private void OnRestocked(int poolId)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (!_entries[i].IsKodama && _entries[i].PoolId == poolId)
            {
                _entries[i].Stopped = false;
            }
        }
    }

    private void OnKodamaRestocked(int poolId)
    {
        for(int i = 0;i < _entries.Count;i++)
        {
            if (_entries[i].IsKodama && _entries[i].PoolId == poolId)
            {
                _entries[i].Stopped = false;
            }
        }
    }
    #endregion
}
