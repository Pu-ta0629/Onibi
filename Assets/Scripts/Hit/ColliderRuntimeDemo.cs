using System.Collections.Generic;
using UnityEngine;

// コライダー一式の、動作確認用。
// 使い方: 空の GameObject に付け、Enemy に確認したい EnemyData を入れ、Targets に Cube などを入れて、再生する。
//         この GameObject の位置と向き(青い軸)が、敵の位置と向きになる。
//         Targets の Cube は、Unit の陣営の、円の体として扱われる。
//         表示の色: 体は緑、攻撃はオレンジ系の赤、索敵は青。
//                   Cube は、攻撃が当たると黄、索敵に入ると青、どちらでもないと白になる。
public class ColliderRuntimeDemo : MonoBehaviour
{
    [Header("確認する敵")]
    [SerializeField] private EnemyData _enemy;

    [Header("敵から見た相手(Unit の陣営。円の体として扱う)")]
    [SerializeField] private Transform[] _targets;
    [SerializeField] private float _targetRadius = 0.5f;

    [Header("敵の体への攻撃を試す(任意。Unit からの攻撃として扱う)")]
    [SerializeField] private Transform _attacker;
    [SerializeField] private ColliderShape _attackerShape = ColliderShape.Sector(3f, 90f);

    private HitWorld _world;
    private ColliderRuntime _runtime;
    private readonly List<HitBody> _targetBodies = new();
    private readonly List<HitBody> _hits = new();
    private bool[] _attacked;
    private bool[] _searched;
    private bool _enemyHit;

    private void Start()
    {
        if (_enemy == null)
        {
            Debug.LogError("[ColliderRuntimeDemo] Enemy が、設定されていません");
            enabled = false;
            return;
        }

        _world = new HitWorld();
        _runtime = new ColliderRuntime(_world, this, Faction.Enemy, _enemy.Colliders);

        foreach (var target in _targets)
        {
            var body = new HitBody(target, Faction.Fire, _targetRadius);
            _world.Add(body);
            _targetBodies.Add(body);
        }

        _attacked = new bool[_targets.Length];
        _searched = new bool[_targets.Length];
    }

    private void OnDestroy()
    {
        _runtime?.Dispose();
    }

    private void Update()
    {
        var position = transform.position;
        var yaw = transform.eulerAngles.y;

        _runtime.SetTransform(position, yaw);
        for (var i = 0; i < _targetBodies.Count; i++)
            _targetBodies[i].Position = _targets[i].position;

        // 判定する側のコライダー(Attack、Search など)を、役割ごとに調べる
        // (Update の中では、毎フレームの無駄なメモリ確保を避けるため、foreach ではなく for を使う)
        for (var i = 0; i < _attacked.Length; i++)
        {
            _attacked[i] = false;
            _searched[i] = false;
        }

        var colliders = _enemy.Colliders.All;
        for (var d = 0; d < colliders.Count; d++)
        {
            var def = colliders[d];
            if (def == null || ColliderRules.IsBody(def.Role)) continue;

            def.Query(_world, position, yaw, _hits);   // 対象は、コライダーの Targets

            for (var h = 0; h < _hits.Count; h++)
            {
                var index = _targetBodies.IndexOf(_hits[h]);
                if (index < 0) continue;

                if (def.Role == ColliderRole.Attack) _attacked[index] = true;
                else if (def.Role == ColliderRole.Search) _searched[index] = true;
            }
        }

        // 敵の体への攻撃(Attacker が、Unit として、前方へ攻撃する)
        _enemyHit = false;
        if (_attacker != null)
        {
            _world.Query(_attackerShape, _attacker.position, _attacker.forward, Faction.Enemy, _hits);
            _enemyHit = _hits.Count > 0;
            HitDebug.DrawShape(_attackerShape, _attacker.position, _attacker.forward, ColliderColors.Attack);
        }

        Draw(position, yaw);
    }

    private void Draw(Vector3 position, float yaw)
    {
        // 敵のコライダー。攻撃が体に当たっているときは、体を黄色にする
        var colliders = _enemy.Colliders.All;
        for (var d = 0; d < colliders.Count; d++)
        {
            var def = colliders[d];
            if (def == null) continue;

            if (_enemyHit && ColliderRules.IsBody(def.Role))
            {
                def.Resolve(position, yaw, out var origin, out var forward);
                HitDebug.DrawShape(def.Shape, origin, forward, ColliderColors.Contact);
            }
            else
            {
                HitDebug.DrawCollider(def, position, yaw);
            }
        }

        // 相手の Cube
        for (var i = 0; i < _targetBodies.Count; i++)
        {
            var color = Color.white;
            if (_attacked[i]) color = ColliderColors.Contact;
            else if (_searched[i]) color = ColliderColors.Search;

            HitDebug.DrawCircle(_targetBodies[i].Position, _targetRadius, color);
        }
    }
}