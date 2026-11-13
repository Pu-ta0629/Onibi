using JetBrains.Annotations;
using System;
using UnityEngine;

/// <summary>
/// 火傷の状態を持つ、PureC#クラス。敵が生成時に受けとって所持する
/// 色(FireColor)ごとに、回数、威力、次のダメージまでのクールタイムを、独立して持つ
/// 【仕様】
/// 付与: 回数も威力も加算 (上限あり)。すでに火傷がある色のクールタイムはそのまま、なかった色は付与の瞬間から数え始める
/// 起爆：威力*消費回数のダメージ。持っている回数より多く指定しても、ある分だけ消費
/// 回数が0になった色は、威力もクールタイムも消える
/// 
/// </summary>
public class BurnState
{
    private struct Slot
    {
        public int Count;
        public int Potency;
        public float Cooldown;
    }

    //FireColorの値をそのまま添字に使う
    private static readonly int SlotLength = GetSlotLength();

    private readonly BurnConfig _config;
    private readonly Slot[] _slots = new Slot[SlotLength];

    public BurnState(BurnConfig config = null)
    {
        _config = config ?? BurnConfig.Default;
    }

    #region Query
    ///<summary>その色の火傷回数</summary>
    public int GetCount(FireColor color)
    {
        return IsBurnColor(color) ? _slots[(int)color].Count : 0;
    }

    ///<summary>その色の火傷威力</summary>
    public int GetPotency(FireColor color)
    {
        return IsBurnColor(color) ? _slots[(int)color].Potency : 0;
    }

    ///<summary>その色の、次のダメージまでの残り時間</summary>
    public float GetRemainingCooldown(FireColor color)
    {
        return IsBurnColor(color) ? _slots[(int)color].Cooldown : 0;
    }

    ///<summary>どれか一色でも、火傷がついているか</summary>
    public bool HasAny
    {
        get
        {
            for(int i = 1;i < _slots.Length;i++)
            {
                if (_slots[i].Count > 0)
                {
                    return true;
                }

            }
            return false;
        }
    }

    #endregion

    #region Public
    ///<summary>
    ///火傷を付与する。回数も威力も加算。上限を超えた分は切り捨てる
    /// </summary>
    public void Add(FireColor color,int count,int potency)
    {
        if(!IsBurnColor(color) || count <= 0)
        {
            return;
        }

        ref Slot slot = ref _slots[(int)color];
        bool wasActive = slot.Count > 0;

        slot.Count = AddClamped(slot.Count, count, _config.MaxCount);
        slot.Potency = AddClamped(slot.Potency, Mathf.Max(0, potency), _config.MaxPotency);

        if(!wasActive)
        {
            slot.Cooldown = _config.Cooldown;
        }
    }

    ///<summary>
    ///毎フレーム呼ぶ。クールタイムが終わった色の分だけ、定期ダメージを出す
    ///同じフレームで出たダメージは、合計して１回だけ、sinkへ渡す
    ///(渡した先で倒れても、この中の処理が途中で崩れないようにするため)
    /// </summary>
    public void Tick(float deltaTime,IDamageSink sink)
    {
        if(deltaTime <= 0f)
        {
            return;
        }

        int total = 0;
        for(int i = 1;i < _slots.Length;i++)
        {
            ref Slot slot = ref _slots[i];
            if(slot.Count <= 0)
            {
                continue;
            }

            slot.Cooldown -= deltaTime;

            //deltaTimeが大きいときも、終わった分だけ繰り返す
            while(slot.Cooldown <= 0f)
            {
                total += slot.Potency;
                slot.Count--;

                if(slot.Count <= 0)
                {
                    slot = default;
                    break;
                }

                slot.Cooldown += _config.Cooldown;
            }
        }

        if(total > 0)
        {
            sink?.TakeDamage(total);
        }
    }

    ///<summary>
    ///起爆する。回数を消費し、威力*消費回数のダメージ量を返す(HPは減らさない。受け取った側が与える)
    ///色がFireColor.Noneなら全色。maxCountは色ごとの上限
    ///火傷が無ければ0を返す(不発)
    ///</summary>
    public int Detonate(FireColor color,int maxCount)
    {
        if(maxCount <= 0)
        {
            return 0;
        }
        if(color == FireColor.None)
        {
            int total = 0;
            for(int i = 1;i<_slots.Length;i++)
            {
                total += Consume(ref _slots[i], maxCount);
            }

            return total;
        }

        return IsBurnColor(color) ? Consume(ref _slots[(int)color], maxCount) : 0;
    }

    ///<summary>
    ///すべての火傷を消す
    /// </summary>
    public void Reset()
    {
        Array.Clear(_slots, 0, _slots.Length);
    }
    #endregion

    #region Private
    //回数を消費して、そのダメージ量を返す。0になったら、威力もクールタイムも消す
    private static int Consume(ref Slot slot,int maxCount)
    {
        if(slot.Count <= 0)
        {
            return 0;
        }

        int used = Mathf.Min(slot.Count, maxCount);
        int damage = slot.Potency * used;

        slot.Count -= used;
        if(slot.Count <= 0)
        {
            slot = default;
        }

        return damage;
    }

    private bool IsBurnColor(FireColor color)
    {
        int index = (int)color;
        return index > 0 && index < _slots.Length;
    }

    private static int AddClamped(int current,int add,int max)
    {
        long sum = (long)current + add;
        return (int)Mathf.Min(sum, max);
    }

    private static int GetSlotLength()
    {
        int max = 0;
        foreach(FireColor color in Enum.GetValues(typeof(FireColor)))
        {
            max = Mathf.Max(max, (int)color);
        }

        return max + 1;
    }
    #endregion;
}
