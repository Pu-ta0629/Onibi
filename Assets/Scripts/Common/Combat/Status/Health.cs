using System;
using UnityEngine;

/// <summary>
/// HPを持つだけの純粋なC#クラス。敵（数値型HP）と迷子（ライフ型）で共通に使う
/// 誰がどれだけ減らすかは、攻撃処理側（AttackProcessor）が決める
/// </summary>
public class Health
{
    /// <summary>HPが変わったときに通知する（引数は変更後のHP）</summary>
    public event Action<int> OnChanged;

    /// <summary>HPが0になったときに通知する</summary>
    public event Action OnDead;

    private readonly int _max;
    private int _current;

    public int current => _current;
    public int max => _max;
    public bool isDead => _current <= 0;

    public Health(int max)
    {
        _max = Mathf.Max(1, max);
        _current = _max;
    }

    /// <summary>
    /// HPを減らす。0になったらOnDeadを通知する（すでに0なら何もしない）
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (isDead)
        {
            return;
        }

        _current = Mathf.Max(0, _current - Mathf.Max(0, amount));
        OnChanged?.Invoke(_current);

        if (isDead)
        {
            OnDead?.Invoke();
        }
    }

    /// <summary>
    /// HPを最大まで戻す（プールで敵を再利用するときに使う）
    /// </summary>
    public void Reset()
    {
        _current = _max;
        OnChanged?.Invoke(_current);
    }
}
