using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 木霊1つ分。動かない。毎フレーム、接触の判定(Search)に迷子が入っていないかを調べる
/// 世界に体は登録しない。見た目はFactoryが差し込む
/// </summary>
public class KodamaController : MonoBehaviour
{
    private readonly List<ColliderDef> _touchDefs = new ();
    private readonly List<HitBody> _hits = new();

    private HitWorld _world;
    private KodamaData _data;
    private GameObject _visual;
    private ParticleSystem[] _systems;

    public KodamaData Data => _data;
    public bool IsActive { get; private set; }

    /// <summary>Factoryから、一度だけ呼ぶ</summary>
    public void Initialize(KodamaData data,GameObject visual,HitWorld world)
    {
        _data = data;
        _visual = visual;
        _world = world;
        _systems = visual.GetComponentsInChildren<ParticleSystem>(true);

        _touchDefs.Clear();
        data.Colliders.Collect(ColliderRole.Search, _touchDefs);

        _touchDefs.Clear();
        data.Colliders.Collect(ColliderRole.Search, _touchDefs);
        if(_touchDefs.Count == 0)
        {
            Debug.LogError($"[KodamaController] Initialize : {data.name} has no Search collider!!!");
        }
    }

    ///<summary>木霊をステージに出す。接触の判定がなければFalse</summary>
    public bool Spawn(Vector3 position)
    {
        if(_touchDefs.Count == 0)
        {
            return false;
        }

        transform.position = position;
        gameObject.SetActive(true);

        for(int i = 0;i < _systems.Length;i++)
        {
            _systems[i].Clear(true);
            _systems[i].Play(true);
        }

        IsActive = true;
        return true;
    }

    ///<summary>木霊をステージから外す(取得されたときや、強制回収の時に、PoolManagerが呼ぶ)</summary>
    public void Despown()
    {
        IsActive = false;

        for(int i = 0;i < _systems.Length;i++)
        {
            _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 接触の判定に入っている迷子を探す。いれば、その迷子を返す
    /// </summary>
    public bool TryDetectToucher(out LostChildController toucher)
    {
        toucher = null;
        if(!IsActive)
        {
            return false;
        }

        for (int i = 0; i < _touchDefs.Count; i++)
        {
            _hits.Clear();
            _touchDefs[i].Query(_world,transform.position,0f,_hits);

            for (int j = 0; j < _hits.Count; j++)
            {
                if (_hits[j].Owner is LostChildController child)
                {
                    toucher = child;
                    return true;
                }
            }
        }
        return false;
    }
}
