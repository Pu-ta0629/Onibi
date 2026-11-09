using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵の移動まわりの調整値（全ての敵で共通）。敵を生成する側（EnemyGeneratorなど）がInspectorで持つ
/// </summary>
[Serializable]
public class EnemyMoveConfig
{
    [Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに合わせるのに使う")]
    public LayerMask GroundMask;

    [Tooltip("目的地の近くのNavMeshを探す距離")]
    [Range(0.1f, 10f)] public float NavMeshSampleDistance = 2f;

    [Tooltip("追従先がこの距離以上動いたら経路を更新する")]
    [Range(0.05f, 5f)] public float RepathDistance = 0.5f;

    [Tooltip("対象にこの距離まで近づいたら止まって、攻撃を始める（攻撃のコライダーが届く距離にする）")]
    [Range(0.1f, 20f)] public float StopDistance = 1.5f;
}

/// <summary>
/// 敵の基底となるC#クラス（継承元なし）。各敵はこれを継承する
/// 移動は迷子・火と共通のMover、HPはHealth、当たり判定はHitDetector、ダメージ処理はAttackProcessorに任せる
/// 攻撃対象は迷子だけ。迷子に近づいたら止まり、Attack1をAttackTimelineの時間どおりに出す
/// </summary>
public abstract class EnemyController : IDamageable, IDisposable
{
    /// <summary>HPが0になったときに通知する（EnemyPoolが受け取って、プールへ戻す）</summary>
    public event Action<EnemyController> OnDead;

    private readonly List<HitInfo> _hits = new List<HitInfo>();
    private readonly AttackProcessor _attackProcessor = new AttackProcessor();
    private readonly AttackSequence _attackSequence = new AttackSequence();

    private EnemyData _data;
    private NavMeshAgent _agent;
    private Mover _mover;
    private Transform _transform;
    private HitDetector _hit;
    private Health _health;
    private AttackTimeline _timeline;
    private float _stopDistance;
    private bool _isInitialized;
    private bool _isActive;

    public EnemyData data => _data;
    public Transform transform => _transform;
    public int hp => _health != null ? _health.current : 0;
    public bool isDead => _health == null || _health.isDead;

    /// <summary>ステージに出ていて、動いているか（プールに戻っている間はfalse）</summary>
    public bool isActive => _isActive;

    /// <summary>継承した敵が、移動を直接操作したいとき用</summary>
    protected Mover mover => _mover;

    #region Initialize
    /// <summary>
    /// 敵を生成する側（EnemyPool）から、一度だけ呼ぶ。この時点では、まだステージに出ていない（Spawnで出す）
    /// </summary>
    public void Initialize(NavMeshAgent agent, EnemyData data, EnemyMoveConfig config, HitWorld world)
    {
        if (agent == null || data == null || config == null || world == null)
        {
            Debug.LogError("[EnemyController] Initialize : Reference is Null!!! ");
            return;
        }

        _agent = agent;
        _data = data;
        _transform = agent.transform;
        _stopDistance = config.StopDistance;

        _mover = new Mover(agent, data.MoveSpeed, config.GroundMask, config.NavMeshSampleDistance, config.RepathDistance, new MovementProfile(data.MoveType, SizeClass.Medium, Faction.Enemy));
        _hit = new HitDetector(world, this, Faction.Enemy, data.Colliders);
        _hit.SetActive(false);

        _health = new Health(data.MaxHP);
        _health.OnDead += HandleDead;

        EnemyAttack attack = data.Attack1;
        _timeline = new AttackTimeline(attack.ChargeTime, attack.ActiveTime, attack.RecoveryTime, attack.Cooldown);

        _isInitialized = true;
    }

    /// <summary>
    /// 敵をステージに出す。HPと攻撃の進行を初期状態に戻して、指定した位置から動き始める
    /// </summary>
    public void Spawn(Vector3 position)
    {
        if (!_isInitialized)
        {
            return;
        }

        _health.Reset();
        _attackSequence.Reset();
        _mover.Stop();
        _transform.gameObject.SetActive(true);

        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Warp(position);
        }
        else
        {
            _transform.position = position;
        }

        _hit.SetTransform(position, _transform.eulerAngles.y);
        _hit.SetActive(true);
        _isActive = true;
    }

    /// <summary>
    /// 敵をステージから外す（プールへ戻すときに、EnemyPoolが呼ぶ）
    /// </summary>
    public void Despawn()
    {
        if (!_isInitialized)
        {
            return;
        }

        _isActive = false;
        _hit.SetActive(false);
        _mover.Stop();
        _transform.gameObject.SetActive(false);
    }

    /// <summary>
    /// 敵が不要になったときに、必ず呼ぶ（HitWorldから体を外す）
    /// </summary>
    public void Dispose()
    {
        if (_health != null)
        {
            _health.OnDead -= HandleDead;
        }

        _hit?.Dispose();
    }
    #endregion

    #region Public
    /// <summary>
    /// 毎フレーム呼ぶ。迷子の座標を受け取り、近づくまでMoverで追従する。近づいたら攻撃する
    /// 座標は呼ぶ側が1回だけ読んで全員に渡す（敵ごとに迷子の位置を読み取らない）
    /// 敵ごとに動きを変えたいときは、継承先でoverrideする
    /// </summary>
    public virtual void Tick(Vector3 targetPosition)
    {
        if (!_isInitialized || !_isActive)
        {
            return;
        }

        _hit.SetTransform(_transform.position, _transform.eulerAngles.y);
        _attackSequence.Tick(Time.deltaTime);

        //溜め・有効・硬直の間は、立ち止まって攻撃に集中する
        AttackPhase phase = _attackSequence.phase;
        if (phase == AttackPhase.Charge || phase == AttackPhase.Active || phase == AttackPhase.Recovery)
        {
            _mover.Stop();
            if (phase == AttackPhase.Active)
            {
                ApplyAttack();
            }
            return;
        }

        //対象に近づいたら止まる（高さは無視して、XZ平面での距離で決める）
        Vector3 delta = targetPosition - _transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= _stopDistance * _stopDistance)
        {
            _mover.Stop();
            if (phase == AttackPhase.Ready)
            {
                BeginAttack(delta);
            }
            return;
        }

        _mover.Follow(targetPosition);
    }

    /// <summary>
    /// ダメージを受ける。HPが0になったら、OnDeadを通知する
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (!_isInitialized || isDead)
        {
            return;
        }

        _health.TakeDamage(damage);
        Debug.Log($"[EnemyController] TakeDamage : name = {_transform.name}, hp = {_health.current}");
    }
    #endregion

    #region Private
    //対象の方を向いて、攻撃を始める。コライダーは向きに合わせて回るので、先に向きを決める
    private void BeginAttack(Vector3 delta)
    {
        if (delta.sqrMagnitude > 1e-6f)
        {
            _transform.rotation = Quaternion.LookRotation(delta.normalized);
        }

        _attackProcessor.Begin();
        _attackSequence.Begin(_timeline);
    }

    //攻撃のコライダーで迷子に当たったか調べ、当たっていたらダメージを渡す（1回の攻撃で同じ相手には1回だけ）
    private void ApplyAttack()
    {
        EnemyAttack attack = _data.Attack1;
        if (_hit.Detect<LostChildController>(attack.ColliderKey, _hits))
        {
            _attackProcessor.Apply(_hits, _data.CalDamage(attack));
        }
    }

    private void HandleDead()
    {
        _isActive = false;
        _hit.SetActive(false);
        _mover.Stop();
        OnDead?.Invoke(this);
    }
    #endregion
}