using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 迷子。渡された追従先の座標へ、Moverで向かう
/// 追従先に近づいたら止まり、離れたら再び動く
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class LostChildController : MonoBehaviour
{
    [Header("MoveConfig")]
    [SerializeField, Tooltip("移動速度。Initialize時にNavMeshAgentのSpeedへ反映する"), Range(0.1f, 20f)] private float _moveSpeed = 5f;
    [SerializeField, Tooltip("目的地の近くでNavMeshを探す距離"), Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Header("Ground")]
    [SerializeField, Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに沿わせるのに使う")] private LayerMask _groundMask;

    [Header("FollowConfig")]
    [SerializeField, Tooltip("追従先がこの距離以上動いたら目的地を更新する"), Range(0.1f, 5f)] private float _repathDistance = 0.5f;
    [SerializeField, Tooltip("追従先にこの距離（水平）まで近づいたら止まる。離れたら再び動く"), Range(0.1f, 20f)] private float _stopDistance = 2f;

    private Mover _mover;
    private bool _isInitialized;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize()
    {
        if (!TryGetComponent(out NavMeshAgent agent))
        {
            Debug.LogError("[LostChildController] Initialize : NavMeshAgent is Null!!! ");
            return;
        }

        _mover = new Mover(agent, _moveSpeed, _groundMask, _navMeshSampleDistance, _repathDistance);
        _isInitialized = true;
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

        //追従先に近づいたら止まる（高さは無視して、XZ平面での距離で見る）
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
    /// 移動を許可する／止める。止めている間は、Tickが呼ばれても動かない（暗い場所など、追従を禁止したいときに使う）
    /// </summary>
    public void SetCanMove(bool canMove)
    {
        if (_isInitialized)
        {
            _mover.SetCanMove(canMove);
        }
    }
    #endregion
}