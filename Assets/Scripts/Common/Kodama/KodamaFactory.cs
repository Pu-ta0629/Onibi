using UnityEngine;

/// <summary>
/// 木霊を１つ作って出荷する。
/// </summary>
public class KodamaFactory
{
    private readonly HitWorld _world;
    private int _createdCount;

    public KodamaFactory(HitWorld world)
    {
        if(world == null)
        {
            Debug.LogError("[KodamaFactory] Reference is NUll!!!!");
        }

        _world = world;
    }

    /// <summary>非アクティブの木霊を一つ生成。</summary>
    public KodamaController Create(KodamaData data,Transform parent)
    {
        if(data == null || data.Prefab == null)
        {
            Debug.LogError("[KodamaFactory] Create : data or Prefab is NULL!!!");
            return null;
        }

        GameObject gameObject = new GameObject($"Kodama_{_createdCount++}");
        gameObject.SetActive(false);
        gameObject.transform.SetParent(parent, false);

        // 見た目のParticleを、子として付ける
        GameObject visual = Object.Instantiate(data.Prefab, gameObject.transform, false);

        KodamaController kodama = gameObject.AddComponent<KodamaController>();
        kodama.Initialize(data, visual, _world);
        return kodama;
    }
}
