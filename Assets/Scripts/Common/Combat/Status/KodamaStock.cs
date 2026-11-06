using System;
using UnityEngine;

/// <summary>
/// 木霊の所持量。火が攻撃するときに消費する純粋なC#クラス
/// いまは量だけ（木霊の種類・火の進化条件は、あとで足す）
/// </summary>
public class KodamaStock
{
    /// <summary>木霊の量が変わったときに通知する（引数は変更後の量）</summary>
    public event Action<int> OnChanged;

    private int _count;

    public int count => _count;

    public KodamaStock(int initialCount)
    {
        _count = Mathf.Max(0, initialCount);
    }

    /// <summary>
    /// 木霊を増やす
    /// </summary>
    public void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _count += amount;
        OnChanged?.Invoke(_count);
    }

    /// <summary>
    /// 木霊を消費する。足りなければ何も減らさずfalseを返す
    /// </summary>
    public bool TryConsume(int amount)
    {
        if (amount < 0 || _count < amount)
        {
            return false;
        }

        _count -= amount;
        OnChanged?.Invoke(_count);
        return true;
    }
}
