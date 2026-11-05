using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 迷子。渡された追従先の座標へ、Moverで向かう
/// 追従先に近づいたら止まり、離れたら再び動く
/// ライフ（ライフ型のHP）を持ち、敵の攻撃を受けるとライフが1つ減る。攻撃手段は持たない
/// 周りの敵を重み付き索敵して、結果を火へ渡す（火の攻撃対象になる）
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class LostChildController : MonoBehaviour, IDamageable
{
    private static readonly SearchResult[] EmptyResults = new SearchResult[0];

    [Header("MoveConfig")]
    [SerializeField, Tooltip("移動速度。Initialize時にMoverへ反映する"), Range(0.1f, 20f)] private float _moveSpeed = 5f;
    [SerializeField, Tooltip("目的地の近くのNavMeshを探す距離"), Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Header("Ground")]
    [SerializeField, Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに合わせるのに使う")] private LayerMask _groundMask;

    [Header("FollowConfig")]
    [SerializeField, Tooltip("追従先がこの距離以上動いたら目的地を更新する"), Range(0.1f, 5f)] private float _repathDistance = 0.5f;
    [SerializeField, Tooltip("追従先にこの距離（水平）まで近づいたら止まる。離れたら再び動く"), Range(0.1f, 20f)] private float _stopDistance = 2f;

    [Header("Combat")]
    [SerializeField, Tooltip("迷子のマスターデータ（コライダーを読む）")] private LostChilde _data;
    [SerializeField, Tooltip("ライフの数。敵の攻撃が1回当たるたびに、1つ減る"), Range(1, 20)] private int _maxLife = 3;

    private Mover _mover;
    private HitDetector _hit;
    private WeightedSearch<EnemyController> _search;
    private Health _life;
    private bool _isInitialized;

    /// <summary>迷子のライフ</summary>
    public Health life => _life;

    /// <summary>迷子が見つけた敵（近い順に重み付き）。火の攻撃対象の索敵に使われる</summary>
    public IReadOnlyList<SearchResult> searchResults => _search != null ? _search.results : EmptyResults;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。world: 全員で共有するHitWorld
    /// </summary>
    public void Initialize(HitWorld world)
    {
        if (!TryGetComponent(out NavMeshAgent agent) || world == null || _data == null)
        {
            Debug.LogError("[LostChildController] Initialize : Reference is Null!!! ");
            return;
        }

        _mover = new Mover(agent, _moveSpeed, _groundMask, _navMeshSampleDistance, _repathDistance);

        //体のコライダーはHitWorldへ登録され、敵の攻撃が当たる。Searchのコライダーで敵を探す
        _hit = new HitDetector(world, this, Faction.LostChild, _data.Colliders);
        _search = new WeightedSearch<EnemyController>(_hit);

        _life = new Health(_maxLife);
        _life.OnDead += HandleDead;
        _isInitialized = true;
    }
    #endregion

    #region UnityLifeCycle
    private void OnDestroy()
    {
        if (_life != null)
        {
            _life.OnDead -= HandleDead;
        }

        _hit?.Dispose();
    }
    #endregion

    #region Public
    /// <summary>
    /// 毎フレーム、追従先の座標を渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        if (!_isInitialized)
        {
            return;
        }

        //追従先に近づいたら止まる（高さは無視して、XZ平面での距離で決める）
        Vector3 delta = targetPosition - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= _stopDistance * _stopDistance)
        {
            _mover.Stop();
            return;
        }

        _mover.Follow(targetPosition);
    }

    /// <summary>
    /// 毎フレーム呼ぶ（追従できないときも）。体の位置を更新して、周りの敵を索敵する
    /// 火が結果を読むので、火のTickより先に呼ぶ
    /// </summary>
    public void UpdateCombat()
    {
        if (!_isInitialized)
        {
            return;
        }

        _hit.SetTransform(transform.position, transform.eulerAngles.y);
        _search.Tick(transform.position);
    }

    /// <summary>
    /// 敵の攻撃を受ける。ライフ型なので、ダメージの大きさに関わらず、ライフが1つ減る
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (!_isInitialized || _life.isDead)
        {
            return;
        }

        _life.TakeDamage(1);
        Debug.Log($"[LostChildController] TakeDamage : life = {_life.current}/{_life.max}");
    }

    /// <summary>
    /// 追従先が切り替わったときに呼ぶ。次のTickで必ず目的地を更新する
    /// </summary>
    public void ResetDestination()
    {
        if (_isInitialized)
        {
            _mover.Stop();
        }
    }

    /// <summary>
    /// 移動を許可／停止する。止めている間は、Tickが呼ばれても動かない（暗い場所など、追従を禁止したいときに使う）
    /// ライフが0のときは、許可しても動かない
    /// </summary>
    public void SetCanMove(bool canMove)
    {
        if (_isInitialized)
        {
            _mover.SetCanMove(canMove && !_life.isDead);
        }
    }
    #endregion

    #region Private
    //ライフが0になったら、動きを止める（ゲームオーバーの処理は、あとで足す）
    private void HandleDead()
    {
        Debug.Log("[LostChildController] HandleDead : life is 0");
        _mover.SetCanMove(false);
        _hit.SetActive(false);
    }
    #endregion
}
