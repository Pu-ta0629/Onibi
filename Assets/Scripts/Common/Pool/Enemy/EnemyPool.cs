using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IDを振った箱。EnemyFactoryから出荷された敵を保管する。追加生産はしない
/// 
/// </summary>
public class EnemyPool
{
    private readonly int _poolId;
    private readonly int _resumeThreshold;
    private readonly List<EnemyController> _stock = new();
    private readonly List<EnemyController> _all = new();

    public int poolId => _poolId;
    public int Stock => _stock.Count;

    ///<summary>この在庫数まで戻ったら生産開始を通知する</summary>
    public int ResumeThreshold => Mathf.Clamp(_resumeThreshold, 1, Mathf.Max(1, _all.Count));

    ///<summary>在庫切れでfalseを返した状態</summary>
    public bool IsStopped { get; set; }

    public IReadOnlyList<EnemyController> All => _all;

    public EnemyPool(int poolId,int resumeThreshoId)
    {
        _poolId = poolId;
        _resumeThreshold = resumeThreshoId;
    }

    ///<summary>出荷された敵を、在庫として登録する</summary>
    public void Add(EnemyController enemy)
    {
        _all.Add(enemy);
        _stock.Add(enemy);
    }

    ///<summary>在庫からランダムに1体出す。在庫がなければfalse</summary>
    public bool TryTake(out EnemyController enemy)
    {
        enemy = null;
        if(_stock.Count == 0)
        {
            return false;
        }

        int index = Random.Range(0, _stock.Count);
        enemy = _stock[index];
        _stock[index] = _stock[_stock.Count - 1];
        _stock.RemoveAt(_stock.Count - 1);
        return true;
    }

    ///<summary>在庫に戻す。Despwanで非表示、次のSpawnでHPなどがリセット</summary>
    public void Return(EnemyController enemy)
    {
        enemy.Despawn();
        _stock.Add(enemy);
    }
}

