using UnityEngine;
using UnityEngine.AI;


/// <summary>
/// PoolManagerからの発注を受けて、EnemyControllerを1体作って出荷する
/// 見た目はSpriteのみ。Prefabは使わず、GameObjectを一から組み立てる
/// 親は、Poolごとに作った親を、Createの旅に受け取る
/// </summary>
public class EnemyFactory
{
    private readonly HitWorld _world;
    public EnemyFactory(HitWorld world)
    {
        _world = world;
    }

    public EnemyController Create(EnemyData data,SizeClass size,Transform parent)
    {
        if (data == null)
        {
            Debug.LogWarning("[EnemyFactory] Create : EnemyData is Null!!!!!");
            return null;
        }

        GameObject obj = !string.IsNullOrWhiteSpace(data.DisplayName) ? new GameObject($"Enemy_{data.DisplayName}") : new GameObject($"Enemy_{data.Type}");
        
        // 先に非表示にしてからコンポーネントを足す
        // (有効なまま足すと、NavMeshの外でNavMeshAgentが起動し、警告が出る
        obj.SetActive(false);
        obj.transform.SetParent(parent,false);

        SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = data.Sprite;

        //EnemyControllerのRequireComponentで、NavMeshAgentも一緒に付く
        EnemyController enemy = obj.AddComponent<EnemyController>();

        NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
        enemy.Initialize(agent, data,size, _world);

        return enemy;
    }

   
}