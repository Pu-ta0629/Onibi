using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 1つの種族の敵のプール（純粋なC#クラス）。Prefab・Data・数はEnemyPoolEntryから受け取る
/// 最初にpoolCount体ぶんを作って非表示にしておき、出すときに取り出す。倒れた敵は、自動でプールへ戻る
/// 敵のクラス（Enemy_???）は、種族からEnemyFactoryが選ぶ
/// </summary>
public class EnemyPool : PoolBase<EnemyController>
{
    private readonly EnemyPoolEntry _entry;
    private readonly Transform _parent;
    private readonly EnemyMoveConfig _config;
    private readonly HitWorld _world;

    /// <summary>このプールの種族</summary>
    public EnemyType type => _entry.type;

    /// <param name="entry">Prefab・Data・数の設定</param>
    /// <param name="parent">生成した敵を入れる親</param>
    /// <param name="config">敵の移動の調整値（全ての敵で共通）</param>
    /// <param name="world">全員で共有するHitWorld</param>
    public EnemyPool(EnemyPoolEntry entry, Transform parent, EnemyMoveConfig config, HitWorld world)
    {
        _entry = entry;
        _parent = parent;
        _config = config;
        _world = world;

        Fill(entry.poolCount);
    }

    #region Public
    /// <summary>
    /// 出ている全ての敵を、1フレーム進める。迷子の座標は、呼ぶ側が1回だけ読んで渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        IReadOnlyList<EnemyController> items = activeItems;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            //Tickの中で他の敵が戻された場合に、範囲外を読まない
            if (i >= items.Count)
            {
                continue;
            }

            items[i].Tick(targetPosition);
        }
    }
    #endregion

    #region Override
    protected override EnemyController Create(int index)
    {
        NavMeshAgent agent = Object.Instantiate(_entry.prefab, _parent);
        agent.gameObject.name = $"Object_Enemy_{_entry.type}_{index}";
        agent.gameObject.SetActive(false);

        EnemyController enemy = EnemyFactory.Create(_entry.type);
        enemy.Initialize(agent, _entry.data, _config, _world);
        enemy.OnDead += Release;   //倒れたら自動でプールへ戻る
        return enemy;
    }

    protected override void OnSpawn(EnemyController item, Vector3 position)
    {
        item.Spawn(position);
    }

    protected override void OnRelease(EnemyController item)
    {
        item.Despawn();
    }

    protected override void OnDisposeItem(EnemyController item)
    {
        item.OnDead -= Release;
        item.Dispose();
    }
    #endregion
}
