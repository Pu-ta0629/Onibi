using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プールの共通基底（純粋なC#クラス）。敵・投げ物・木霊など、種類ごとのPoolはこれを継承する
/// 空き（Stack）と出ている物（List）を別々に持つので、取り出し・返却で全探索をしない
/// 継承先は Create（1つ作る）を実装し、必要なら OnSpawn / OnRelease / OnDisposeItem をoverrideする
/// 出せる数の上限はpoolCount。全員が出ている間は出せない（canExpand=trueなら足りない分だけ増やす）
/// </summary>
public abstract class PoolBase<T> : IDisposable where T : class
{
    private readonly List<T> _all = new List<T>();
    private readonly List<T> _active = new List<T>();
    private readonly Stack<T> _free = new Stack<T>();
    private readonly bool _canExpand;

    /// <summary>プールが持っている総数</summary>
    public int count => _all.Count;

    /// <summary>いまステージに出ている数</summary>
    public int activeCount => _active.Count;

    /// <summary>いま取り出せる数</summary>
    public int freeCount => _free.Count;

    /// <summary>新しく1つ出せるか（空きがある、または増やせる）</summary>
    public bool hasFree => _free.Count > 0 || _canExpand;

    /// <summary>出ている物の一覧。Tickなどで回すときは、後ろから回す（回している間に返却されても安全なため）</summary>
    protected IReadOnlyList<T> activeItems => _active;

    /// <param name="canExpand">trueなら、空きがないときに1つ増やして出す</param>
    protected PoolBase(bool canExpand = false)
    {
        _canExpand = canExpand;
    }

    #region Public
    /// <summary> 1つ出す。出せなければfalse。取り出した物は、OnSpawnで出す準備をしてから返す </summary>
    public bool TrySpawn(Vector3 position, out T item)
    {
        item = null;
        if (_free.Count == 0)
        {
            if (!_canExpand)
            {
                return false;
            }

            Add();
        }

        item = _free.Pop();
        _active.Add(item);
        OnSpawn(item, position);
        return true;
    }

    /// <summary> プールへ戻す。出ていない物・nullは何もしない（二重返却しても安全）</summary>
    public void Release(T item)
    {
        if (item == null || !_active.Remove(item))
        {
            return;
        }

        OnRelease(item);
        _free.Push(item);
    }

    /// <summary> 出ている物を全てプールへ戻す（ステージの切り替え・リトライ用）</summary>
    public void ReleaseAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Release(_active[i]);
        }
    }

    /// <summary> プールが不要になったときに呼ぶ</summary>
    public void Dispose()
    {
        for (int i = 0; i < _all.Count; i++)
        {
            OnDisposeItem(_all[i]);
        }

        _all.Clear();
        _active.Clear();
        _free.Clear();
    }
    #endregion

    #region Protected
    /// <summary>
    /// 最初にpoolCount個ぶん作る。継承先のコンストラクタの最後で呼ぶ
    /// （コンストラクタの中でvirtualを呼ぶと、継承先の変数が未設定のため分けている）
    /// </summary>
    protected void Fill(int poolCount)
    {
        for (int i = 0; i < poolCount; i++)
        {
            Add();
        }
    }

    /// <summary>1つ作る（非表示の状態で返す）。indexは作った順の番号</summary>
    protected abstract T Create(int index);

    /// <summary>出すときの準備（位置を決めて表示する、など）</summary>
    protected virtual void OnSpawn(T item, Vector3 position) { }

    /// <summary>戻すときの後始末（非表示にする、など）</summary>
    protected virtual void OnRelease(T item) { }

    /// <summary>プールを破棄するときの、1つぶんの後始末（イベント解除など）</summary>
    protected virtual void OnDisposeItem(T item) { }
    #endregion

    #region Private
    private void Add()
    {
        T item = Create(_all.Count);
        _all.Add(item);
        _free.Push(item);
    }
    #endregion
}
