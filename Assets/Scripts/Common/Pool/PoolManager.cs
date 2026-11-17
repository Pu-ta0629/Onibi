using System;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;

/// <summary>
/// StageDataを読み、EnemyFactoryへ発注し、出荷された敵をPoolに仕分ける
/// 在庫の全体管理(申請の照合、出庫リスト、敵のTick、生産再開の通知)も担当
/// </summary>
public class PoolManager : MonoBehaviour
{
    private class PlacedEnemy
    {
        public EnemyController Enemy;
        public Vector3 Position;
        public float RotationY;
    }

    [Tooltip("在庫がこの数まで戻ったら、生産再開を通知する")]
    [SerializeField, Min(1)] private int _resumeThreshoId = 3;

    private readonly Dictionary<int, EnemyPool> _pools = new();
    private readonly Dictionary<EnemyController, EnemyPool> _owners = new();
    private readonly List<PlacedEnemy> _placedEnemies = new List<PlacedEnemy>();
    private readonly HashSet<EnemyController> _outList = new();
    private readonly List<EnemyController> _tickBuffer = new();
    private readonly Queue<EnemyController> _returnQueue = new();

    private EnemyFactory _factory;

    ///<summary>在庫が閾値まで戻ったpoolのIDを通知する</summary>
    public event Action<int> Restocked;

    ///<summary>
    ///StageDataを読み取り、湧きスポットごとにPoolを作って、EnemyFactoryへ発注
    /// </summary>
    public void Build(StageData stageData, EnemyMoveConfig config, HitWorld world)
    {
        _factory = new EnemyFactory(config, world, transform);

        // (湧きスポットのリスト、スポットのID、EnemyDataのList、再開の閾値)
        foreach(var spot in stageData.SpawnSpots)
        {
            if(spot.Kind != SpawnKind.Enemy)
            {
                continue;
            }

            if(_pools.ContainsKey(spot.Id))
            {
                Debug.LogError($"[PoolManager] Build : PoolId is duplicated = {spot.Id}");
                continue;
            }

            EnemyPool pool = new EnemyPool(spot.Id, _resumeThreshoId);
            _pools.Add(spot.Id, pool);

            foreach(EnemyCount entry in spot.EnemyList)
            {
                if(entry == null || entry.EnemyData == null)
                {
                    continue;
                }

                for(int i = 0; i < entry.Count; i++)
                {
                    EnemyController enemy = _factory.Create(entry.EnemyData, spot.EnemySize);
                    if(enemy == null)
                    {
                        continue;
                    }

                    pool.Add(enemy);
                    _owners.Add(enemy, pool);
                    enemy.OnDead += HandleDead;
                }
            }
        }

        // 位置で置く敵 : 発注して、位置と向きと一緒に控えておく
        foreach(var placement in stageData.Enemies)
        {
            if(placement == null || placement.Enemy == null)
            {
                continue;
            }

            EnemyController enemy = _factory.Create(placement.Enemy, placement.Size);
            if(enemy == null)
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

    public void SpawnPlacedEnemies()
    {
        for(int i = 0; i < _placedEnemies.Count;i++)
        {
            PlacedEnemy placed = _placedEnemies[i];
            if(placed.Enemy == null || _outList.Contains(placed.Enemy))
            {
                continue;
            }

            // 通れる場所に寄せる
            Vector3 position = placed.Position;
            if(placed.Enemy.TryFindSpawnPosition(position,out Vector3 onNavMesh))
            {
                position = onNavMesh;
            }

            placed.Enemy.Spawn(position, placed.RotationY);
            _outList.Add(placed.Enemy);
        }
    }

    /// <summary>
    /// SpawnManagerからの申請。IDが一致するpoolだけが対象で、在庫がなければfalse
    /// </summary>
    public bool TryRequest(int poolId,out EnemyController enemy)
    {
        enemy = null;
        if(!_pools.TryGetValue(poolId,out EnemyPool pool))
        {
            return false;
        }

        if(!pool.TryTake(out enemy))
        {
            pool.IsStopped = true;
            return false;
        }

        _outList.Add(enemy);
        return true;
    }

    /// <summary>
    /// 毎フレーム呼ぶ。ターゲットの座標を受け取り、出庫中の敵全員のTickへ渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        // Tick中に死んでもリストが壊れないよう、コピーを回す
        _tickBuffer.Clear();
        _tickBuffer.AddRange(_outList);

        for(int i = 0; i < _tickBuffer.Count;i++)
        {
            _tickBuffer[i].Tick(targetPosition);
        }

        FlushReturns();
    }

    public void ReturnAll()
    {
        foreach(EnemyController enemy in _outList)
        {
            _returnQueue.Enqueue(enemy); 
        }

        FlushReturns();
    }

    /// <summary>
    /// ステージを閉じるとき。敵のGameObjectを全部破棄
    /// </summary>
    public void Clear()
    {
        foreach(EnemyController enemy in _owners.Keys)
        {
            DestroyEnemy(enemy);
        }

        for (int i = 0; i < _returnQueue.Count; i++)
        {
            DestroyEnemy(_placedEnemies[i].Enemy);
        }

        _pools.Clear();
        _owners.Clear();
        _placedEnemies.Clear();
        _outList.Clear();
        _tickBuffer.Clear();
        _returnQueue.Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }

    private  void DestroyEnemy(EnemyController enemy)
    {
        if(enemy == null)
        {
            return;
        }

        enemy.OnDead -= HandleDead;
        Destroy(enemy.gameObject);
    }

    // 敵のHPが0になったときに呼ばれる。返却はTickの最後にまとめて行う
    private void HandleDead(EnemyController enemy)
    {
        if(_outList.Contains(enemy))
        {
            _returnQueue.Enqueue(enemy);
        }
    }

    private void FlushReturns()
    {
        while (_returnQueue.Count > 0)
        {
            EnemyController enemy = _returnQueue.Dequeue();
            if(!_outList.Remove(enemy))
            {
                continue;
            }

            if(!_owners.TryGetValue(enemy,out EnemyPool pool))
            {
                enemy.Despawn();
                continue;
            }

            pool.Return(enemy);

            if(pool.IsStopped && pool.Stock >= pool.ResumeThreshold)
            {
                pool.IsStopped = false;
                Restocked?.Invoke(pool.poolId);
            }
        }
    }

#if UNITY_EDITOR

    ///<summary>動作確認用。各Poolの在庫数、停止中かどうか、出ている敵の数を文字にする</summary>
    public string DescribeStocks()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach(KeyValuePair<int,EnemyPool> pair in _pools)
        {
            EnemyPool pool = pair.Value;
            builder.AppendLine($"Pool {pair.Key}: 在庫{pool.Stock} / {pool.All.Count}, 停止中={pool.IsStopped}, 再開は在庫{pool.ResumeThreshold}以上");
        }

        builder.Append($"出ている敵: {_outList.Count}体(位置で置いた敵を含む)");
        return builder.ToString();
    }
#endif
}
