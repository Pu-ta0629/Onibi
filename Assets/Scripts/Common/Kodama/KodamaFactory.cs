using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 木霊を１つ作って出荷する。
/// </summary>
public class KodamaFactory
{
    private readonly HitWorld _world;
    private readonly Transform _parent;
    private int _createdCount;

    public KodamaFactory(HitWorld world,Transform parent)
    {
        if(world == null || parent == null)
        {
            Debug.LogError("[KodamaFactory] Reference is NUll!!!!");
        }

        _world = world;
        _parent = parent;
    }

    /// <summary>非アクティブの木霊を一つ生成。</summary>
    public KodamaController Create(KodamaData data)
    {
        if(data == null || data.Prefab == null)
        {
            Debug.LogError("[KodamaFactory] Create : data or Prefab is NULL!!!");
            return null;
        }

        GameObject gameObject = new GameObject($"Kodama_{_createdCount++}");
        gameObject.SetActive(false);
        gameObject.transform.SetParent(_parent, false);

        // 見た目のParticleを、子として付ける
        GameObject visual = Object.Instantiate(data.Prefab, gameObject.transform, false);

        KodamaController kodama = gameObject.AddComponent<KodamaController>();
        kodama.Initialize(data, visual, _world);
        return kodama;
    }
}
