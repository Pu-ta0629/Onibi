using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 迷子。渡された追従先の座標へ、Moverで向かう
/// 追従先に近づいたら止まり、離れたら再び動く
/// NavMeshがないと動かない（Agentを使わない直進には切り替えない）
/// ライフ（ライフ型のHP）を持ち、敵の攻撃を受けるとライフが1つ減る。攻撃手段は持たない
/// 周りの敵を重み付き索敵して、結果を火へ渡す（火の攻撃対象になる）
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class LostChildController : MonoBehaviour, IHitable
{
    private static readonly SearchResult[] EmptyResults = new SearchResult[0];

    [SerializeField, Tooltip("迷子のマスターデータ（コライダーを読む）")] private LostChild _data;

    [Space(10)]
    [SerializeField] private int _currentLife;

    private NavMeshAgent _agent;

    private Mover _mover;
    private HitDetector _hit;
    private WeightedSearch<EnemyController> _search;
    private Health _life;
    private bool _isInitialized;
    private NavFollowResult _lastResult = NavFollowResult.Following;

    /// <summary>迷子のライフ</summary>
    public Health life => _life;

    /// <summary>迷子が見つけた敵（近い順に重み付き）。火の攻撃対象の索敵に使われる</summary>
    public IReadOnlyList<SearchResult> searchResults => _search != null ? _search.results : EmptyResults;
    public LostChild LostChild => _data;

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

        _mover = new Mover(agent, _data.MoveSpeed, _data.GroundMask, _data.NavMsehSampleDistance, _data.RepathDistance, _data.MovementProfile);

        //体のコライダーはHitWorldへ登録され、敵の攻撃が当たる。Searchのコライダーで敵を探す
        _hit = new HitDetector(world, this, Faction.LostChild, _data.Colliders);
        _search = new WeightedSearch<EnemyController>(_hit);


        _agent = agent;
        _life = new Health(_data.MaxLife);
        _currentLife = _life.current;
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
    public void SpawnAt(Vector3 position)
    {
        if (!_isInitialized) return;
        _mover.Stop();
        transform.position = position;

        if (!NavAgentProfile.Apply(_agent, _data.MovementProfile, position))
        {
            //Agentは無効のままになる。Moverが、元のAgentの種類で、近くのNavMeshを探す
            Debug.LogWarning($"[LostChildController] SpawnAt : NavAgentProfile.Apply failed. position = {position}, profile = {_data.MovementProfile}, StageNavMeshProvider.Current is {(StageNavMeshProvider.Current == null ? "null" : "set")}");
        }

        _hit.SetTransform(position, transform.eulerAngles.y);
    }
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
        if (delta.sqrMagnitude <= _data.StopDistance * _data.StopDistance)
        {
            _mover.Stop();
            return;
        }

        //NavMeshがないときは、直進に切り替えず、動かない
        NavFollowResult result = _mover.FollowOnNavMesh(targetPosition);
        if (result != _lastResult)
        {
            _lastResult = result;
            if (result != NavFollowResult.Following)
            {
                WarnCannotFollow(result, targetPosition);
            }
        }
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

        _life.TakeDamage(damage);
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
    //動けない理由を、結果が変わったときだけ警告する（毎フレームは出さない）。状況の詳細も一緒に出す
    private void WarnCannotFollow(NavFollowResult result, Vector3 targetPosition)
    {
        string reason = result == NavFollowResult.SelfOffNavMesh
            ? "NavMesh is not found near LostChild. LostChild does not move without NavMesh!!"
            : "NavMesh is not found near the follow target (even in the wide search). LostChild does not move!!";

        Debug.LogWarning($"[LostChildController] Tick : {reason}\n{_mover.DescribeNavState(targetPosition)}");
    }

    //ライフが0になったら、動きを止める（ゲームオーバーの処理は、あとで足す）
    private void HandleDead()
    {
        Debug.Log("[LostChildController] HandleDead : life is 0");
        _mover.SetCanMove(false);
        _hit.SetActive(false);
    }
    #endregion
}