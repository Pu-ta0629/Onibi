using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 迷子が取得した木霊数。通常の木霊と、特殊木霊の種類ごとに、べつべつに積む
/// </summary>
public class KodamaStack
{
    private readonly Dictionary<SpecialKodamaKind, int> _specialCounts = new();
    private int _normalCount;

    ///<summary>数が変わったときに通知する</summary>
    public event Action Changed;

    ///<summary>通常の木霊の数</summary>
    public int NormalCount  => _normalCount;

    ///<summary>特殊木霊の、指定した種類の数</summary>
    public int GetSpecialCount(SpecialKodamaKind kind)
    {
        return _specialCounts.TryGetValue(kind,out int count) ? count : 0;
    }

    ///<summary>木霊を、指定した量だけ積む</summary>
    public void Add(KodamaData data,int amount = 1)
    {

        if(data == null || amount <= 0)
        {
            return;
        }

        if(data.IsSpecial)
        {
            _specialCounts[data.SpecialKind] = GetSpecialCount(data.SpecialKind) + amount;
        }
        else
        {
            _normalCount += amount;
        }

        Debug.Log($"[KodamaStack] Add : {data.name} + {amount} (normal = {_normalCount})");
        Changed?.Invoke();
    }

    ///<summary>すべて空にする</summary>
    public void Clear()
    {
        _normalCount = 0;
        _specialCounts.Clear();
        Changed?.Invoke();
    }

}
