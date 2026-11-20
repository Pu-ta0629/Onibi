    using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StageDataのEnemyの湧きスポットごとに、一定間隔でPoolManagerへ申請して敵を出す
/// 在庫切れでfalseが返ったスポットは止まり、PoolManagerの再開通知で動き出す
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
    }

    [SerializeField] private PoolManager _poolManager;

    [Tooltip("湧く位置を決めるとき、スポットの円の中で通れる場所を探す回数")]
    [SerializeField, Min(1)] private int _pickTryCount = 5;

    private readonly List<SpawnEntry> _entries = new();

    ///<summary>StageDataから、Enemyの湧きスポットを読み取る</summary>
    public void Build(StageData stageData)
    {
        _entries.Clear();

        foreach(var spot in stageData.SpawnSpots)
        {
            if(spot.Kind != SpawnKind.Enemy)
            {
                continue;
            }

            SpawnEntry entry = new SpawnEntry();
            entry.PoolId = spot.Id;
            entry.Center = spot.Position;
            entry.Radius = spot.Radius;
            entry.Interval = spot.SpawnInterval;
            _entries.Add(entry);
        }
    }

    private void OnEnable()
    {
        _poolManager.Restocked += OnRestocked;
    }

    private void OnDisable()
    {
        _poolManager.Restocked -= OnRestocked;
    }

    private void Update()
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
        if(!_poolManager.TryRequest(entry.PoolId,out EnemyController enemy))
        {
            entry.Stopped = true;
            return;
        }
        //出てきた敵が通れる場所を、スポットの円の中から選ぶ
        enemy.Spawn(PickSpawnPosition(entry,enemy));
    }

    private Vector3 PickSpawnPosition(SpawnEntry entry,EnemyController enemy)
    {
        Vector3 point = entry.Center;

        for(int i = 0; i < _pickTryCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * entry.Radius;
            point = entry.Center + new Vector3(offset.x, 0f, offset.y);

            if(enemy.TryFindSpawnPosition(point,out Vector3 onNavMesh))
            {
                return onNavMesh;
            }
        }
        return point;
    }

    private void OnRestocked(int poolId)
    {
        for(int i = 0;i < _entries.Count; i++)
        {
            if (_entries[i].PoolId == poolId)
            {
                _entries[i].Stopped = false;
            }
        }
    }
}
