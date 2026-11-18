using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;


/// <summary>
/// PoolManagerからの発注を受けて、EnemyControllerを1体作って出荷する
/// 見た目はSpriteのみ。Prefabは使わず、GameObjectを一から組み立てる
/// </summary>
public class EnemyFactory
{
    private readonly HitWorld _world;
    private readonly Transform _parent;
    public EnemyFactory(HitWorld world,Transform parent)
    {
        _world = world;
        _parent = parent;
    }

    public EnemyController Create(EnemyData data,SizeClass size)
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
        obj.transform.SetParent(_parent);

        SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = data.Sprite;

        //EnemyControllerのRequireComponentで、NavMeshAgentも一緒に付く
        EnemyController enemy = obj.AddComponent<EnemyController>();

        NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
        enemy.Initialize(agent, data,size, _world);

        return enemy;
    }

   
}