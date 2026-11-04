using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LightTransport;

// ゲーム中の、1体分のコライダー。
// 体の役割(Hit)のコライダーは、HitBody にして、HitWorld に登録する。
// 判定する側の役割(Attack、Search など)は、Query で、いまの位置と向きから判定する。
public sealed class ColliderRuntime : IDisposable
{
    private readonly HitWorld _world;
    private readonly ColliderSet _set;
    private readonly List<HitBody> _bodies = new();

    private Vector3 _position;
    private float _yaw;

    public ColliderRuntime(HitWorld world, object owner, Faction faction, ColliderSet set)
    {
        _world = world;
        _set = set;

        foreach (var def in set.All)
        {
            if (def == null || !ColliderRules.IsBody(def.Role)) continue;

            var body = new HitBody(owner, faction, def);
            _bodies.Add(body);
            world.Add(body);
        }
    }

    // 毎フレーム、キャラの位置と向き(yaw: 度)に合わせて、体の位置を更新する
    public void SetTransform(Vector3 position, float yaw)
    {
        _position = position;
        _yaw = yaw;
        foreach (var body in _bodies) body.SetTransform(position, yaw);
    }

    // 名前(Key)で選んだ、判定する側のコライダーを、いまの位置と向きで判定する
    // その名前のコライダーがなければ false
    public bool TryQuery(string key, List<HitBody> results)
    {
        results.Clear();
        if (!_set.TryFind(key, out var def)) return false;

        def.Query(_world, _position, _yaw, results);
        return true;
    }

    // コライダー1つを直接指定して、判定する
    public void Query(ColliderDef def, List<HitBody> results)
    {
        def.Query(_world, _position, _yaw, results);
    }

    // false の間は、体が判定の対象にならない(倒れた、隠れている、など)
    public bool Active
    {
        set
        {
            foreach (var body in _bodies) body.Active = value;
        }
    }

    // キャラが消えるときに、必ず呼ぶ(HitWorld から体を外す)
    public void Dispose()
    {
        foreach (var body in _bodies) _world.Remove(body);
        _bodies.Clear();
    }
}

public static class HitResults
{
    // 同じキャラの体が、複数当たっているときに、キャラごとに1つにまとめる
    public static void DistinctOwners(List<HitBody> hits, List<object> owners)
    {
        owners.Clear();
        foreach (var hit in hits)
        {
            if (!owners.Contains(hit.Owner)) owners.Add(hit.Owner);
        }
    }
}