using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StageDataを読み、EnemyFactoryへ発注し、生成された敵をPoolに仕分ける管理クラス（EntitySpawnManagerから使用）
/// 在庫の全体管理（申請の照合、出庫リスト、敵のTick、生産再開の通知）を担当する
/// </summary>
public class PoolManager : MonoBehaviour
{
    private class PlacedEnemy
    {
        public EnemyController Enemy;
        public Vector3 Position;
        public float RotationY;
    }


    private readonly Dictionary<int, EnemyPool> _pools = new();
    private readonly Dictionary<EnemyController, EnemyPool> _owners = new();
    private readonly List<PlacedEnemy> _placedEnemies = new List<PlacedEnemy>();
    private readonly HashSet<EnemyController> _outList = new();
    private readonly List<EnemyController> _tickBuffer = new();
    private readonly Queue<EnemyController> _returnQueue = new();

    private EnemyFactory _factory;


    private readonly Dictionary<int, KodamaPool> _kodamaPools = new();
    private KodamaFactory _kodamaFactory;

    private Transform _enemyParent;
    private Transform _kodamaParent;

    /// <summary>在庫が再開閾値まで戻ったPoolのIDを通知する</summary>
    public event Action<int> Restocked;

    /// <summary>木霊のPoolに空きができたときに、湧きスポットのIDを通知する</summary>
    public event Action<int> KodamaRestocked;

    /// <summary>現在ステージに出現している敵の数（位置指定で配置した敵を含む）</summary>
    public int outCount => _outList.Count;

    

    #region Build
    /// <summary>
    /// StageDataを読み取り、敵スポットごとにPoolを構築して、EnemyFactoryへ生成を発注する
    /// parent: 生成した敵のGameObjectの親
    /// </summary>
    public void Build(StageData stageData, HitWorld world, Transform parent)
    {
        if (stageData == null || world == null || parent == null)
        {
            Debug.LogError("[PoolManager] Build : Reference is Null!!! ");
            return;
        }

        _factory = new EnemyFactory(world);
        _enemyParent = CreateChild(parent, "EnemyPoolParent");

        // 敵スポットのリスト、スポットID、EnemyDataのList、再開の閾値
        foreach (var spot in stageData.SpawnSpots)
        {
            if (spot.Kind != SpawnKind.Enemy)
            {
                continue;
            }

            if (_pools.ContainsKey(spot.Id))
            {
                Debug.LogError($"[PoolManager] Build : PoolId is duplicated = {spot.Id}");
                continue;
            }

            EnemyPool pool = new EnemyPool(spot.Id, spot.ResumeThreshold);
            _pools.Add(spot.Id, pool);

            Transform poolRoot = CreateChild(_enemyParent, $"Pool_Enemy_{spot.Id}");

            foreach (EnemyCount entry in spot.EnemyList)
            {
                if (entry == null || entry.EnemyData == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    EnemyController enemy = _factory.Create(entry.EnemyData, spot.EnemySize,poolRoot);
                    if (enemy == null)
                    {
                        continue;
                    }

                    pool.Add(enemy);
                    _owners.Add(enemy, pool);
                    enemy.OnDead += HandleDead;
                }
            }
        }

        Transform otherRoot = CreateChild(_enemyParent, "Pool_Enemy_Other");

        // 位置指定で配置する敵：生成して、位置と向きと一緒に記録しておく
        foreach (var placement in stageData.Enemies)
        {
            if (placement == null || placement.Enemy == null)
            {
                continue;
            }

            EnemyController enemy = _factory.Create(placement.Enemy, placement.Size,otherRoot);
            if (enemy == null)
            {
                continue;
            }

            PlacedEnemy placed = new PlacedEnemy();
            placed.Enemy = enemy;
            placed.Position = placement.Position;
            placed.RotationY = placement.RotationY;
            _placedEnemies.Add(placed);

            enemy.OnDead += HandleDead;
        }
    }

    /// <summary>
    /// StageDataを読み取り、木霊のスポットごとにPoolを作って、KodamaFactoryへ生成を発注する
    /// 
    /// </summary>
    public void BuildKodama(StageData stageData,HitWorld world,Transform parent)
    {
        if(stageData == null ||  world == null || parent == null)
        {
            Debug.LogError("[PoolManager] BuildKodama : Reference is NULL!!!");
            return;
        }


        _kodamaFactory = new KodamaFactory(world);
        _kodamaParent = CreateChild(parent, "KodamaPoolParent");

        foreach(var spot in stageData.SpawnSpots)
        {
            if(spot.Kind != SpawnKind.Kodama)
            {
                continue;
            }

            if(_kodamaPools.ContainsKey(spot.Id))
            {
                Debug.LogError($"[PoolManager] BuildKodama : PoolId is duplicated = {spot.Id}");
                continue;
            }

            KodamaData data = spot.KodamaData;
            if(data == null)
            {
                Debug.LogError($"[PoolManager] BuildKodama : KodamaData is not set.  SpawnSpot Id = {spot.Id}");
                continue;
            }

            if (data.IsSpecial)
            {
                Debug.LogError($"[PoolManager] BuildKodama : {data.name} is Special. A normal kodamaData is required. SpawnSpot Id = {spot.Id}");
                continue;
            }

            KodamaPool pool = new KodamaPool(spot.Id, spot.ResumeThreshold);
            _kodamaPools.Add(spot.Id, pool);

            Transform poolRoot = CreateChild(_kodamaParent, $"Pool_Kodama_{spot.Id}");

            for (int i = 0; i < _kodamaPools.Count; i++)
            {
                pool.Add(_kodamaFactory.Create(data, poolRoot));
            }
        }
    }

    
    #endregion

    #region Public
    /// <summary>
    /// 位置指定で配置した敵（StageDataのEnemies）を、全員出現させる。すでに出現している敵は処理しない
    /// </summary>
    public void SpawnPlacedEnemies()
    {
        for (int i = 0; i < _placedEnemies.Count; i++)
        {
            PlacedEnemy placed = _placedEnemies[i];
            if (placed.Enemy == null || _outList.Contains(placed.Enemy))
            {
                continue;
            }

            // 通れる場所に補正する
            Vector3 position = placed.Position;
            if (placed.Enemy.TryFindSpawnPosition(position, out Vector3 onNavMesh))
            {
                position = onNavMesh;
            }
            else
            {
                Debug.LogWarning($"[PoolManager] SpawnPlacedEnemies : NavMesh is not found near {position} ({placed.Enemy.name})");
            }

            

            placed.Enemy.Spawn(position, placed.RotationY);
            _outList.Add(placed.Enemy);
        }
    }

    /// <summary>
    /// 敵スポットからの生成申請。IDが一致するPoolのみが対象で、在庫がなければfalseを返す
    /// </summary>
    public bool TryRequest(int poolId, out EnemyController enemy)
    {
        enemy = null;
        if (!_pools.TryGetValue(poolId, out EnemyPool pool))
        {
            return false;
        }

        if (!pool.TryTake(out enemy))
        {
            pool.IsStopped = true;
            return false;
        }

        _outList.Add(enemy);
        return true;
    }

    public bool TryRequestKodama(int poolId,Vector3 position)
    {
        if(!_kodamaPools.TryGetValue(poolId,out var pool))
        {
            return false;
        }

        if(!pool.TryTake(out var kodama))
        {
            pool.IsStopped = true;
            return false;
        }

        if(!kodama.Spawn(position))
        {
            pool.Return(kodama);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 毎フレーム呼び出す。ターゲットの座標を受け取り、出庫中の敵全員のTickへ渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        // Tick中に敵が死亡してもリストが変更されないよう、コピーする
        _tickBuffer.Clear();
        _tickBuffer.AddRange(_outList);

        for (int i = 0; i < _tickBuffer.Count; i++)
        {
            _tickBuffer[i].Tick(targetPosition);
        }

        FlushReturns();
        CheckKodamaTouch();
    }

    /// <summary>
    /// 出現中の敵を、指定した1体だけプールへ戻す（スポーン位置を変更したい場合など）
    /// </summary>
    public void Return(EnemyController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        _returnQueue.Enqueue(enemy);
        FlushReturns();
    }

    /// <summary>
    /// 出現中の敵を、全員プールへ戻す（全滅処理）
    /// </summary>
    public void ReturnAll()
    {
        foreach (EnemyController enemy in _outList)
        {
            _returnQueue.Enqueue(enemy);
        }

        FlushReturns();

        foreach(var pool in _kodamaPools.Values)
        {
            for(int i = pool.Active.Count - 1;i >= 0;i--)
            {
                pool.Return(pool.Active[i]);
            }
        }
    }

    /// <summary>
    /// ステージを閉じるときに呼び出す。敵のGameObjectをすべて破棄する
    /// </summary>
    public void Clear()
    {
        foreach (EnemyController enemy in _owners.Keys)
        {
            DestroyEnemy(enemy);
        }

        for (int i = 0; i < _placedEnemies.Count; i++)
        {
            DestroyEnemy(_placedEnemies[i].Enemy);
        }

        foreach(var pool in _kodamaPools.Values)
        {
            for(int i = 0; i <  pool.All.Count; i++)
            {
                if (pool.All[i] != null)
                {
                    UnityEngine.Object.Destroy(pool.All[i].gameObject);
                }
            }
        }

        _enemyParent = null;
        _kodamaParent = null;

        _kodamaPools.Clear();
        _kodamaFactory = null;

        _pools.Clear();
        _owners.Clear();
        _placedEnemies.Clear();
        _outList.Clear();
        _tickBuffer.Clear();
        _returnQueue.Clear();
        _factory = null;
    }
    #endregion

    #region Private

    private static Transform CreateChild(Transform parent,string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private void DestroyEnemy(EnemyController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.OnDead -= HandleDead;
        UnityEngine.Object.Destroy(enemy.gameObject);
    }

    // 敵のHPが0になったときに呼ばれる。返却はTickの最後にまとめて行う
    private void HandleDead(EnemyController enemy)
    {
        if (_outList.Contains(enemy))
        {
            _returnQueue.Enqueue(enemy);
        }
    }

    private void FlushReturns()
    {
        while (_returnQueue.Count > 0)
        {
            EnemyController enemy = _returnQueue.Dequeue();
            if (!_outList.Remove(enemy))
            {
                continue;
            }

            if (!_owners.TryGetValue(enemy, out EnemyPool pool))
            {
                enemy.Despawn();
                continue;
            }

            pool.Return(enemy);

            if (pool.IsStopped && pool.Stock >= pool.ResumeThreshold)
            {
                pool.IsStopped = false;
                Restocked?.Invoke(pool.poolId);
            }
        }
    }

    private void CheckKodamaTouch()
    {
        foreach(KodamaPool pool in _kodamaPools.Values)
        {
            for(int i = pool.Active.Count -1;i >= 0; i--)
            {
                KodamaController kodama = pool.Active[i]; 
                if(!kodama.TryDetectToucher(out var toucher))
                {
                    continue;
                }

                var data = kodama.Data;
                pool.Return(kodama);
                toucher.CollectKodama(data);

                if(pool.IsStopped && pool.Stock >= pool.ResumeThreshold)
                {
                    pool.IsStopped = false;
                    KodamaRestocked?.Invoke(pool.PoolId);
                }
            }
        }
    }
    #endregion

#if UNITY_EDITOR
    #region Debug
    /// <summary>動作確認用。各Poolの在庫数、停止中かどうか、出現中の敵の数を文字列にする</summary>
    public string DescribeStocks()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach (KeyValuePair<int, EnemyPool> pair in _pools)
        {
            EnemyPool pool = pair.Value;
            builder.AppendLine($"Pool {pair.Key}: 在庫{pool.Stock} / {pool.All.Count}, 停止中={pool.IsStopped}, 再開は在庫{pool.ResumeThreshold}以上");
        }

        foreach(var pair in _kodamaPools)
        {
            builder.AppendLine($"木霊Pool {pair.Key}: 在庫{pair.Value.Stock} / {pair.Value.All.Count}, 停止中={pair.Value.IsStopped}"); ;
        }

        builder.Append($"出現中の敵: {_outList.Count}体（位置指定で配置した敵を含む）");
        return builder.ToString();
    }
    #endregion
#endif
}