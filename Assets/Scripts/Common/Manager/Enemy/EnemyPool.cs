using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵のプール。最初にpoolCount体ぶんを作って非表示にしておき、必要なときに取り出して出す
/// 最大数はpoolCount。全員が出ている間は、新しく出せない。倒れた敵は、自動でプールへ戻る
/// </summary>
public class EnemyPool : IDisposable
{
    private readonly List<EnemyController> _all = new List<EnemyController>();
    private readonly Stack<EnemyController> _free = new Stack<EnemyController>();

    /// <summary>プールの最大数</summary>
    public int count => _all.Count;

    /// <summary>いまステージに出ている数</summary>
    public int activeCount => _all.Count - _free.Count;

    /// <param name="prefab">敵のPrefab（NavMeshAgent付き）</param>
    /// <param name="parent">生成した敵を入れる親</param>
    /// <param name="create">敵の種類ごとのクラスを作る処理（例 () => new EnemyOni()）</param>
    public EnemyPool(NavMeshAgent prefab, Transform parent, EnemyData data, EnemyMoveConfig config, HitWorld world, int poolCount, Func<EnemyController> create)
    {
        for (int i = 0; i < poolCount; i++)
        {
            NavMeshAgent agent = UnityEngine.Object.Instantiate(prefab, parent);
            agent.gameObject.SetActive(false);

            EnemyController enemy = create();
            enemy.Initialize(agent, data, config, world);
            enemy.OnDead += Release;

            _all.Add(enemy);
            _free.Push(enemy);
        }
    }

    /// <summary>
    /// 敵を1体出す。全員が出ていて出せなければfalse
    /// </summary>
    public bool TrySpawn(Vector3 position, out EnemyController enemy)
    {
        enemy = null;
        if (_free.Count == 0)
        {
            return false;
        }

        enemy = _free.Pop();
        enemy.Spawn(position);
        return true;
    }

    /// <summary>
    /// 敵をプールへ戻す（倒れたときは自動で呼ばれる）
    /// </summary>
    public void Release(EnemyController enemy)
    {
        if (enemy == null || _free.Contains(enemy))
        {
            return;
        }

        enemy.Despawn();
        _free.Push(enemy);
    }

    /// <summary>
    /// 出ている全ての敵を、1フレーム進める。迷子の座標は、呼ぶ側が1回だけ読んで渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            _all[i].Tick(targetPosition);
        }
    }

    /// <summary>
    /// プールが不要になったときに呼ぶ（敵のGameObjectは、親と一緒に消える）
    /// </summary>
    public void Dispose()
    {
        for (int i = 0; i < _all.Count; i++)
        {
            _all[i].OnDead -= Release;
            _all[i].Dispose();
        }

        _all.Clear();
        _free.Clear();
    }
}
