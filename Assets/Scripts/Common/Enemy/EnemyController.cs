using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵の基底となるMonobehaviour。各敵はこれを継承する
/// EnemyFactoryがGameObjectに付けて、EnemyDataからデータを引き継ぐ
/// 移動は迷子・火と共通のMover、HPはHealth、当たり判定はHitDetector、ダメージ処理はAttackProcessorに任せる
/// 攻撃対象は迷子だけ。迷子に近づいたら止まり、Attack1をAttackTimelineの時間どおりに出す
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IHittable, IHitTarget, IFireTarget
{
    /// <summary>HPが0になったときに通知する（PoolManagerが受け取って、プールへ戻す）</summary>
    public event Action<EnemyController> OnDead;

    private readonly List<HitInfo> _hits = new List<HitInfo>();
    private readonly AttackProcessor _attackProcessor = new AttackProcessor();
    private readonly AttackSequence _attackSequence = new AttackSequence();

    private EnemyData _data;
    private NavMeshAgent _agent;
    private Mover _mover;
    private HitDetector _hit;
    private Health _health;
    private BurnState _burn;
    private MovementProfile _movementProfile;

    private AttackTimeline _timeline;
    private float _stopDistance;
    private bool _isInitialized;
    private bool _isActive;
    private bool _lastFollowOk = true;   //前のフレームにNavMeshで追従できていたか（変わったときだけ警告を出すため）

    public EnemyData data => _data;
    public int hp => _health != null ? _health.current : 0;
    public bool isDead => _health == null || _health.isDead;

    /// <summary>IHitTarget: 火傷の状態</summary>
    public BurnState Burn => _burn;

    /// <summary>ステージに出ていて、動いているか（プールに戻っている間はfalse）</summary>
    public bool isActive => _isActive;

    #region Initialize
    /// <summary>
    /// 敵を生成する側（EnemyFactory）から、一度だけ呼ぶ。この時点では、まだステージに出ていない（Spawnで出す）
    /// 移動の調整値は、dataのMoveConfigから読む
    /// </summary>
    public void Initialize(
        NavMeshAgent agent,
        EnemyData data,
        SizeClass size,
        HitWorld world,
        BurnState burn = null
        )
    {
        if (agent == null || data == null || data.MoveConfig == null || world == null)
        {
            Debug.LogError($"[EnemyController] Initialize : Reference is Null!!! agent={agent == null}, data={data == null}, moveConfig={data == null || data.MoveConfig == null}, world={world == null}");
            return;
        }

        EnemyMoveConfig config = data.MoveConfig;

        _agent = agent;
        _data = data;
        _stopDistance = config.StopDistance;
        _movementProfile = new MovementProfile(data.MoveType, size, Faction.Enemy);

        _mover = new Mover(agent, data.MoveSpeed, config.GroundMask, config.NavMeshSampleDistance, config.RepathDistance, _movementProfile, acceleration: _data.Acceleration);
        _hit = new HitDetector(world, this, Faction.Enemy, data.Colliders);
        _hit.SetActive(false);

        _burn = burn;

        _health = new Health(data.MaxHP);
        _health.OnDead += HandleDead;

        EnemyAttack attack = data.Attack1;
        _timeline = new AttackTimeline(attack.ChargeTime, attack.ActiveTime, attack.RecoveryTime, attack.Cooldown);

        _isInitialized = true;
    }

    /// <summary>
    /// 指定した座標の近くで、この敵が通れるNavMeshの上の位置を探す
    /// 見つからなければfalse。NavMeshの準備ができる前は、見つからない
    /// </summary>
    public bool TryFindSpawnPosition(Vector3 point, out Vector3 result)
    {
        if (!_isInitialized)
        {
            result = point;
            return false;
        }

        return _mover.TryFindSpawnPosition(point, out result);
    }

    /// <summary>
    /// 敵をステージに出す。HPと攻撃の進行を初期状態に戻して、指定した位置から動き始める
    /// positionは、TryFindSpawnPositionで決めた位置を渡すと、NavMeshの上から始められる
    /// </summary>
    public void Spawn(Vector3 position, float rotationY = 0f)
    {
        if (!_isInitialized)
        {
            return;
        }

        _health.Reset();
        _burn?.Reset();
        _attackSequence.Reset();
        _mover.Stop();
        _lastFollowOk = true;

        // 非表示のうちに位置と向きを決めてから、有効にする
        // Agentは無効のまま。最初のTickで、Moverが、NavMeshに乗せてから有効にする
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        gameObject.SetActive(true);

        _hit.SetTransform(position, rotationY);
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

        // 次のSpawnで、NavMeshの外で有効にならないように、Agentを無効に戻す
        _agent.enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 敵が破棄されるときに、Unityが自動で呼ぶ
    /// </summary>
    private void OnDestroy()
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
    public void Tick(Vector3 targetPosition)
    {
        if (!_isInitialized || !_isActive)
        {
            return;
        }

        _burn?.Tick(Time.deltaTime, this);
        if (!_isActive)
        {
            return;
        }

        _hit.SetTransform(transform.position, transform.eulerAngles.y);
        _attackSequence.Tick(Time.deltaTime);

        // 溜め・有効・硬直の間は、立ち止まって攻撃に集中する
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

        // 対象に近づいたら止まる（高さは無視して、XZ平面での距離で決める）
        Vector3 delta = targetPosition - transform.position;
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

        //迷子が、この敵のNavMeshの外にいても、NavMeshの上で迷子に一番近い点までは向かう
        bool followOk = _mover.FollowNearest(targetPosition);
        WarnIfCannotFollow(followOk, targetPosition);
    }

    /// <summary>
    /// IHittable: 命中情報を受け取り、効果を順に適用する
    /// </summary>
    public void OnHit(AttackHit hit)
    {
        // 初期化前と、すでに倒れている敵には、効果を適用しない
        if (!_isInitialized || isDead)
        {
            return;
        }

        hit.ApplyTo(this);
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
       // Debug.Log($"[EnemyController] TakeDamage : name = {name}, hp = {_health.current}");
    }
    #endregion

#if UNITY_EDITOR
    #region Debug
    /// <summary>
    /// NavMesh確認用。
    /// Agentが有効でNavMesh上にあり、StageDataから作った自分のプロファイルのNavMeshに乗っているかを確認する
    /// （直進のフォールバックで動いているときは、Agentが無効になるのでfalse）
    /// </summary>
    public bool isOnExpectedNavMesh
    {
        get
        {
            if (!_isInitialized || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return false;
            }

            StageNavMeshSet set = StageNavMeshProvider.Current;
            return set != null && set.TryGetAgentTypeId(_movementProfile, out int expected) && _agent.agentTypeID == expected;
        }
    }

    /// <summary>
    /// NavMesh確認用。NavMeshAgentの状態を文字列にする（毎フレームは呼ばない）
    /// </summary>
    public string DescribeNavState()
    {
        if (!_isInitialized)
        {
            return "not initialized";
        }

        int expected = -1;
        StageNavMeshProvider.Current?.TryGetAgentTypeId(_movementProfile, out expected);
        bool onMesh = _agent.enabled && _agent.isOnNavMesh;

        return $"profile={_movementProfile}, agent(enabled={_agent.enabled}, onNavMesh={onMesh}, typeID={_agent.agentTypeID}/expected={expected}, "
             + $"hasPath={(onMesh && _agent.hasPath)}, speed={_agent.velocity.magnitude:0.##}), pos={transform.position}";
    }
    #endregion
#endif

    #region Private
    // 対象の方向を向いてから、攻撃を開始する
    // コライダーは前方に合わせてあるので、先に向きを変える
    private void BeginAttack(Vector3 delta)
    {
        if (delta.sqrMagnitude > 1e-6f)
        {
            transform.rotation = Quaternion.LookRotation(delta.normalized);
        }

        _attackProcessor.Begin();
        _attackSequence.Begin(_timeline);
    }

    // 攻撃用コライダーで迷子に接触したかを調べ、接触していたらダメージを渡す
    // （1回の攻撃で複数回ダメージが発生しないようにする）
    private void ApplyAttack()
    {
        EnemyAttack attack = _data.Attack1;
        if (_hit.Detect<LostChildController>(attack.ColliderKey, _hits))
        {
            _attackProcessor.Apply(_hits, _data.CalDamage(attack));
        }
    }

    // NavMeshで追従できなくなったとき（敵自身の近くにNavMeshがない）に、変わった瞬間だけ警告を出す
    // 迷子がNavMeshの外にいる場合は、FollowNearestが一番近い点へ向かうので、警告にはならない
    private void WarnIfCannotFollow(bool followOk, Vector3 targetPosition)
    {
        if (followOk == _lastFollowOk)
        {
            return;
        }

        _lastFollowOk = followOk;
        if (!followOk)
        {
            Debug.LogWarning($"[EnemyController] Tick : {name} cannot follow by NavMesh. NavMesh is not found near this enemy (moving straight instead).\n{_mover.DescribeNavState(targetPosition)}");
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