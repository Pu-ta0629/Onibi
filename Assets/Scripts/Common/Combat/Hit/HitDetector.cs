using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 当たり判定で見つかった1件分の情報。索敵も攻撃も、この情報をもとに処理する
/// </summary>
public readonly struct HitInfo
{
    public readonly object Owner;       //当たった相手の持ち主（EnemyController、LostChildControllerなど）
    public readonly Vector3 Position;   //当たった相手の位置

    public HitInfo(object owner, Vector3 position)
    {
        Owner = owner;
        Position = position;
    }
}

/// <summary>
/// 当たり判定クラス。ColliderRuntimeを包んで、「どの種類の相手に当たったか」だけを返す
/// 結果は、キャラごとに1件へまとめ、自分自身は除く
/// 結果を入れるリストは呼ぶ側が持っておいて使い回す（毎回newしない）
/// 表示用に、全ての HitDetector を All で見られる。判定の記録は、DebugRecord が true の間だけ行う（HitDebugView が切り替える）
/// </summary>
public class HitDetector : IDisposable
{
    private static readonly List<HitDetector> Instances = new List<HitDetector>();

    /// <summary>生きている全ての HitDetector（表示用）</summary>
    public static IReadOnlyList<HitDetector> All => Instances;

    /// <summary>true の間だけ、判定の記録（最後に判定した時刻や当たった位置）を残す</summary>
    public static bool DebugRecord;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instances.Clear();
        DebugRecord = false;
    }

    private readonly ColliderRuntime _runtime;
    private readonly ColliderSet _set;
    private readonly object _owner;
    private readonly List<HitBody> _bodies = new List<HitBody>();
    private readonly List<ColliderDef> _defs = new List<ColliderDef>();

    private Vector3 _position;
    private float _yaw;
    private bool _isActive = true;

    #region Debug
    /// <summary>持ち主（表示や警告に使う）</summary>
    public object Owner => _owner;
    public ColliderSet Set => _set;
    public Vector3 Position => _position;
    public float Yaw => _yaw;
    public bool IsActive => _isActive;

    /// <summary>名前（Key）で最後に判定したコライダーの名前と時刻</summary>
    public string LastQueryKey { get; private set; }
    public float LastQueryTime { get; private set; } = -999f;

    /// <summary>最後に当たりがあった時刻と、そのときの相手の位置</summary>
    public float LastHitTime { get; private set; } = -999f;
    public readonly List<Vector3> LastHitPositions = new List<Vector3>();

    /// <summary>一覧に無かった名前（Keyの食い違いの検出用）</summary>
    public string MissingKey { get; private set; }
    #endregion

    /// <param name="world">全員で共有するHitWorld</param>
    /// <param name="owner">持ち主（当たった側が、誰に当たったか調べるのに使う）</param>
    /// <param name="faction">自分の体の陣営</param>
    /// <param name="set">コライダーの一覧</param>
    public HitDetector(HitWorld world, object owner, Faction faction, ColliderSet set)
    {
        _owner = owner;
        _set = set;
        _runtime = new ColliderRuntime(world, owner, faction, set);
        Instances.Add(this);
    }

    /// <summary>
    /// falseの間は、体が判定の対象にならない（倒れた、プールに戻っている、など）
    /// </summary>
    public void SetActive(bool isActive)
    {
        _isActive = isActive;
        _runtime.Active = isActive;
    }

    /// <summary>
    /// 毎フレーム、キャラの位置と向き（yaw: 度）を渡す。体の位置と、判定の起点が更新される
    /// </summary>
    public void SetTransform(Vector3 position, float yaw)
    {
        _position = position;
        _yaw = yaw;
        _runtime.SetTransform(position, yaw);
    }

    /// <summary>
    /// 名前（Key）で選んだコライダーで判定する。T型の持ち主だけを、resultsへ入れる
    /// その名前のコライダーがなければfalse
    /// </summary>
    public bool Detect<T>(string key, List<HitInfo> results) where T : class
    {
        results.Clear();
        if (!_runtime.TryQuery(key, _bodies))
        {
            if (DebugRecord)
            {
                MissingKey = key;
            }
            return false;
        }

        Append<T>(results);

        if (DebugRecord)
        {
            RecordQuery(key, results);
        }
        return true;
    }

    /// <summary>
    /// 役割（Searchなど）が同じコライダーを全て判定する。T型の持ち主だけを、resultsへ入れる
    /// その役割のコライダーがなければfalse
    /// </summary>
    public bool Detect<T>(ColliderRole role, List<HitInfo> results) where T : class
    {
        results.Clear();
        _set.Collect(role, _defs);

        for (int i = 0; i < _defs.Count; i++)
        {
            _runtime.Query(_defs[i], _bodies);
            Append<T>(results);
        }

        return _defs.Count > 0;
    }

    /// <summary>
    /// キャラが消えるときに必ず呼ぶ（HitWorldから体を外す）
    /// </summary>
    public void Dispose()
    {
        Instances.Remove(this);
        _runtime.Dispose();
    }

    //判定の記録（表示用）
    private void RecordQuery(string key, List<HitInfo> results)
    {
        LastQueryKey = key;
        LastQueryTime = Time.time;

        if (results.Count == 0)
        {
            return;
        }

        LastHitTime = Time.time;
        LastHitPositions.Clear();
        for (int i = 0; i < results.Count; i++)
        {
            LastHitPositions.Add(results[i].Position);
        }
    }

    //見つかった体のうち、T型の持ち主だけを、重複なしで追加する
    private void Append<T>(List<HitInfo> results) where T : class
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            HitBody body = _bodies[i];
            if (!(body.Owner is T) || ReferenceEquals(body.Owner, _owner) || ContainsOwner(results, body.Owner))
            {
                continue;
            }

            results.Add(new HitInfo(body.Owner, body.Position));
        }
    }

    private static bool ContainsOwner(List<HitInfo> results, object owner)
    {
        for (int i = 0; i < results.Count; i++)
        {
            if (ReferenceEquals(results[i].Owner, owner))
            {
                return true;
            }
        }

        return false;
    }
}