using System.Collections.Generic;

/// <summary>
/// 木霊のPool。湧きスポット1つ分。在庫と、出ている木霊を持つ。追加の生産はしない
/// </summary>
public class KodamaPool
{
    private readonly List<KodamaController> _stock = new();
    private readonly List<KodamaController> _active = new();
    private readonly List<KodamaController> _all = new();

    public int PoolId { get; }
    public bool IsStopped { get; set; }
    public int Stock => _stock.Count;
    public IReadOnlyList<KodamaController> Active => _active;
    public IReadOnlyList<KodamaController> All => _all;

    public KodamaPool(int poolId)
    {
        PoolId = poolId;
    }

    /// <summary>Factoryが作った木霊を、在庫に入れる</summary>
    public void Add(KodamaController kodama)
    {
        if(kodama == null)
        {
            return;
        }
        _all.Add(kodama);
        _stock.Add(kodama);
    }

    /// <summary>在庫から1つ出して、出ているリストに入れる。在庫がなければ、Falseを返す</summary>
    public bool TryTake(out KodamaController kodama)
    {
        kodama = null;
        if(_stock.Count == 0)
        {
            return false;
        }

        int last = _stock.Count - 1;
        kodama = _stock[last];
        _stock.RemoveAt(last);
        _active.Add(kodama);
        return true;
    }

    ///<summary>木霊をPoolに戻す。出ていない木霊は、何もしない</summary>
    public void Return(KodamaController kodama)
    {
        if(kodama == null || !_active.Remove(kodama))
        {
            return;
        }

        kodama.Despawn();
        _stock.Add(kodama);
    }
}
