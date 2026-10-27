using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 迷子。渡された追従先の座標へNavMeshAgentで向かう
/// 追従先が一定距離以上動いたときだけ目的地を更新し、追従先に近づいたら止まる
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class LostChildController : MonoBehaviour
{
    [Header("FollowConfig")]
    [SerializeField, Tooltip("追従先がこの距離以上動いたら目的地を更新する"), Range(0.1f, 5f)] private float _repathDistance = 0.5f;
    [SerializeField, Tooltip("追従先にこの距離（水平）まで近づいたら止まる。離れたら再び動く"), Range(0.1f, 20f)] private float _stopDistance = 2f;

    private NavMeshAgent _agent;
    private Vector3 _lastDestination;
    private bool _hasDestination;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize()
    {
        if (_agent == null && !TryGetComponent(out _agent))
        {
            Debug.LogError("[LostChildController] Initialize : NavMeshAgent is Null!!! ");
        }
    }
    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
    #endregion

    #region Public
    /// <summary>
    /// 毎フレーム、追従先の座標を渡す
    /// </summary>
    public void Tick(Vector3 targetPosition)
    {
        if (_agent == null || !_agent.isOnNavMesh)
        {
            return;
        }

        //追従先に近づいたら止まる（高さは無視して、XZ平面での距離で見る）
        Vector3 delta = targetPosition - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= _stopDistance * _stopDistance)
        {
            StopMoving();
            return;
        }

        if (_hasDestination && (targetPosition - _lastDestination).sqrMagnitude < _repathDistance * _repathDistance)
        {
            return;
        }

        if (_agent.SetDestination(targetPosition))
        {
            _lastDestination = targetPosition;
            _hasDestination = true;
        }
    }

    /// <summary>
    /// 追従先が切り替わったときに呼ぶ。次のTickで必ず目的地を更新する
    /// </summary>
    public void ResetDestination()
    {
        _hasDestination = false;
    }
    #endregion

    #region Private
    //今の経路を捨てて止まる。離れたときに再び目的地を設定できるよう、目的地の記録も消す
    private void StopMoving()
    {
        if (_hasDestination || _agent.hasPath)
        {
            _agent.ResetPath();
        }

        _hasDestination = false;
    }
    #endregion
}