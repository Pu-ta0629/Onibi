using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 移動をまとめた純粋なC#クラス。移動に必要な対象（座標・方向）は、呼ぶ側が確定させて渡す
/// ・直進系（MoveStraight / MoveTowards / BeginStraightTo）：Agentを使わず位置を直接動かし、地面の高さに沿わせる（軽い）
/// ・追従（Follow）：対象が確定してから、NavMeshAgentで経路を探して向かう
///   近くにNavMeshがない間はエラーにせず、NavMeshがあるところまで直進系で向かう
/// NavMeshAgentは普段は無効にしておき、追従できるときだけ有効にする
/// </summary>
public class Mover
{
    private const float RetryInterval = 0.1f;   //NavMeshを探し直す間隔（秒）
    private const float RayUp = 2f;             //地面を探すRayの開始位置（今の高さからの上方向の距離）
    private const float RayLength = 10f;        //Rayの長さ
    private const float ArriveDistance = 0.01f; 

    private readonly NavMeshAgent _agent;
    private readonly Transform _transform;
    private readonly LayerMask _groundMask;
    private readonly float _speed;
    private readonly float _hoverHeight;      //地面から軸までの高さ（AgentのBaseOffset）
    private readonly float _sampleDistance;   //この距離の中にNavMeshがあれば「近くにある」とみなす
    private readonly float _repathDistance;

    private Vector3 _lastPoint;       //追従の最後の目的地
    private Vector3 _straightPoint;   //対象有りの直進の目標
    private float _nextTryTime;       //次にNavMeshを探してよい時刻
    private bool _hasDestination;
    private bool _canMove = true;

    #region Initialize
    /// <summary>
    /// groundMask：地面のLayer（地形に沿わせるのに使う）／sampleDistance：NavMeshを探す範囲
    /// repathDistance：追従先がこの距離動いたら経路を更新する
    /// </summary>
    /// <param name="agent">動かすNavMeshAgent</param>
    /// <param name="speed">移動速度</param>
    /// <param name="groundMask">地面のLayer</param>
    /// <param name="sampleDistance">NavMeshを探す範囲</param>
    /// <param name="repathDistance">追従先がこの距離動いたら経路を更新する</param>
    public Mover(NavMeshAgent agent, float speed, LayerMask groundMask, float sampleDistance, float repathDistance)
    {
        _agent = agent;
        _transform = agent.transform;
        _speed = speed;
        _groundMask = groundMask;
        _hoverHeight = agent.baseOffset;
        _sampleDistance = sampleDistance;
        _repathDistance = repathDistance;

        _agent.speed = speed;
        _agent.enabled = false;   //追従できるときだけ有効にする（NavMesh外での有効化エラーを防ぐ）
    }
    #endregion

    #region Straight
    /// <summary>対象なしの直進。指定した方向へ1フレーム分進む（毎フレーム呼ぶ）</summary>
    public void MoveStraight(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 1e-6f)
        {
            Step(direction.normalized, float.MaxValue);
        }
    }

    /// <summary>指定座標へ1フレーム分向かう（毎フレーム呼ぶ）。着いたらfalse</summary>
    public bool MoveTowards(Vector3 point)
    {
        Vector3 delta = point - _transform.position;
        delta.y = 0f;

        float distance = delta.magnitude;
        if (distance < ArriveDistance)
        {
            return false;
        }

        Step(delta / distance, distance);
        return distance > _speed * Time.deltaTime;
    }

    /// <summary>対象有りの直進の開始。呼んだ瞬間の対象の位置を目標にする（以降、対象が動いても目標は変えない）</summary>
    public void BeginStraightTo(Vector3 targetPosition)
    {
        _straightPoint = targetPosition;
    }

    /// <summary>BeginStraightToで決めた目標へ直進する（毎フレーム呼ぶ）。着いたらfalse</summary>
    public bool MoveStraightToTarget()
    {
        return MoveTowards(_straightPoint);
    }
    #endregion

    #region Follow
    /// <summary>
    /// 対象の追従（毎フレーム呼んでよい）。NavMeshで向かえていればtrue
    /// 近くにNavMeshがない間は、Agentを使わず直進で向かい、falseを返す
    /// </summary>
    public bool Follow(Vector3 point)
    {
        if (!TryEnableAgent())
        {
            MoveTowards(point);
            return false;
        }

        //目的地がほとんど動いていないときは、経路を計算し直さない
        if (_hasDestination && (point - _lastPoint).sqrMagnitude < _repathDistance * _repathDistance)
        {
            return true;
        }

        if (!TrySamplePosition(point, out Vector3 destination) || !_agent.SetDestination(destination))
        {
            return false;
        }

        _lastPoint = point;
        _hasDestination = true;
        return true;
    }
    #endregion

    #region Control
    /// <summary>今の経路を捨てて止まる（直進は、呼ばれなければ止まっている）</summary>
    public void Stop()
    {
        _hasDestination = false;

        if (_agent.enabled && _agent.isOnNavMesh && _agent.hasPath)
        {
            _agent.ResetPath();
        }
    }

    /// <summary>移動を許可する／止める。それまでの経路は捨てる</summary>
    public void SetCanMove(bool canMove)
    {
        _canMove = canMove;
        Stop();

        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = !canMove;
        }
    }
    #endregion

    #region Private
    //向きと最大距離を指定して1フレーム分進む。Agentを止めて、位置を直接動かす
    private void Step(Vector3 direction, float maxDistance)
    {
        if (!_canMove)
        {
            return;
        }

        SetAgentEnabled(false);

        Vector3 position = _transform.position + direction * Mathf.Min(_speed * Time.deltaTime, maxDistance);
        position.y = GetGroundY(position);
        _transform.position = position;
    }

    //真上から下へRayを飛ばして地面の高さを求める。地面がなければ今の高さのまま
    private float GetGroundY(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * RayUp;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayLength, _groundMask)
            ? hit.point.y + _hoverHeight
            : position.y;
    }

    //近くにNavMeshがあればAgentを有効にして乗せる。なければfalse（探し直しは一定間隔をあける）
    private bool TryEnableAgent()
    {
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            return true;
        }

        if (Time.time < _nextTryTime)
        {
            return false;
        }

        if (TrySamplePosition(_transform.position, out Vector3 onNavMesh))
        {
            //Agentの足元（軸からBaseOffsetだけ下）がNavMeshに乗るように置いてから有効にする
            SetAgentEnabled(false);
            _transform.position = onNavMesh + Vector3.up * _hoverHeight;
            SetAgentEnabled(true);

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = !_canMove;
                return true;
            }
        }

        _nextTryTime = Time.time + RetryInterval;
        return false;
    }

    private void SetAgentEnabled(bool isEnabled)
    {
        if (_agent.enabled != isEnabled)
        {
            _hasDestination = false;
            _agent.enabled = isEnabled;
        }
    }

    //指定した座標の近くにあるNavMesh上の点を探す（このAgentの種類で探す）
    private bool TrySamplePosition(Vector3 point, out Vector3 result)
    {
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, _sampleDistance, filter);
        result = hit.position;
        return found;
    }
    #endregion
}