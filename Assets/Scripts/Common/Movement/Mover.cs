using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 移動をまとめた純粋なC#クラス。移動に必要な対象（座標・方向）は、呼ぶ側が確定させて渡す
/// ・直進系（MoveStraight / MoveTowards / BeginStraightTo）：Agentを使わず位置を直接動かし、地面の高さに沿わせる（軽い）
/// ・追従（Follow）：対象が確定してから、NavMeshAgentで経路を探して向かう
///   近くにNavMeshがない間はエラーにせず、NavMeshがあるところまで直進系で向かう
/// ・NavMesh専用の追従（FollowOnNavMesh）：NavMeshがない間は、直進に切り替えず、動かない（迷子など）
///   追従先がNavMeshの外にあるときは、広めに探した最寄りのNavMesh上の点へ向かう
/// NavMeshAgentは普段は無効にしておき、追従できるときだけ有効にする
/// </summary>
public class Mover
{
    [Header("Const")]
    private const float RetryInterval = 0.1f;   //NavMeshを探し直す間隔（秒）
    private const float RayUp = 2f;             //地面を探すRayの開始位置（今の高さからの上方向の距離）
    private const float RayLength = 10f;        //Rayの長さ
    private const float ArriveDistance = 0.01f;
    private const float NavOnlyTargetSampleScale = 3f;   //NavMesh専用の追従で、追従先を探す範囲（sampleDistanceの何倍か）
    private const float DiagnosticWideDistance = 20f;    //診断で、広く探す範囲

    [Header("Read Only")]
    private readonly NavMeshAgent _agent;
    private readonly Transform _transform;
    private readonly LayerMask _groundMask;
    private readonly float _speed;
    private readonly float _hoverHeight;      //地面から軸までの高さ（AgentのBaseOffset）
    private readonly float _sampleDistance;   //この距離の中にNavMeshがあれば「近くにある」とみなす
    private readonly float _repathDistance;
    private readonly MovementProfile? _profile;   //if(_profile == null)  {AgentTypeはそのまま}

    private Vector3 _lastPoint;       //追従の最後の目的地
    private Vector3 _straightPoint;   //対象有りの直進の目標
    private float _nextTryTime;       //次にNavMeshを探してよい時刻
    private  float _followSpeed;               //Follow系(Agent)で使う速度
    private int _typeId = int.MinValue;   // プロファイルから決めたAgentTypeID(Agentの読み取り値に頼らない)
    private bool _hasDestination;
    private bool _canMove = true;
    private StageNavMeshSet _typeSet;    //Agentの種類を合わせた時のNavMeshの組み合わせ

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
    /// <param name="profile">nullならそのままのType</param>>
    public Mover(NavMeshAgent agent, float speed, LayerMask groundMask, float sampleDistance, float repathDistance, MovementProfile? profile = null)
    {
        _profile = profile;
        _agent = agent;
        _transform = agent.transform;
        _speed = speed;
        _followSpeed = speed;
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
    /// <summary>Follow(navMeshAgent)で使う速度を変える。直進系の速度には影響しない</summary>
    /// <param name="speed">Follow Speed</param>
    public void SetFollowSpeed(float speed)
    {
        _followSpeed = speed;
        _agent.speed = speed;
    }
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

        return SetAgentDestination(point, _sampleDistance);
    }

    /// <summary>
    /// NavMesh専用の追従（毎フレーム呼んでよい）。結果で、向かえているか、動けない理由が分かる
    /// 自分の近くにNavMeshがない間は、直進にも切り替えず、動かない
    /// 追従先がNavMeshの外のときは、広めに探した最寄りのNavMesh上の点へ向かう（追従先が、飛べる火のように、歩けない所にいる場合）
    /// </summary>
    public NavFollowResult FollowOnNavMesh(Vector3 point)
    {
        if (!TryEnableAgent())
        {
            return NavFollowResult.SelfOffNavMesh;
        }

        return SetAgentDestination(point, _sampleDistance * NavOnlyTargetSampleScale)
            ? NavFollowResult.Following
            : NavFollowResult.TargetOffNavMesh;
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

    #region Debug
    /// <summary>
    /// NavMeshで動けないときの原因調べ用の文字（失敗したときに、1回だけ出す用。文字列を作るので、毎フレームは呼ばない）
    /// Agentの状態、自分と追従先の近くのNavMesh、シーン全体のNavMeshの有無を返す
    /// </summary>
    public string DescribeNavState(Vector3 target)
    {
        Vector3 self = _transform.position;
        float targetDistance = _sampleDistance * NavOnlyTargetSampleScale;

        bool selfNear = TrySamplePosition(self, out _, _sampleDistance);
        bool selfWide = TrySamplePosition(self, out Vector3 selfWideHit, DiagnosticWideDistance);
        bool targetNear = TrySamplePosition(target, out _, targetDistance);
        bool targetWide = TrySamplePosition(target, out Vector3 targetWideHit, DiagnosticWideDistance);
        int vertexCount = NavMesh.CalculateTriangulation().vertices.Length;

        return $"agent(enabled={_agent.enabled}, onNavMesh={_agent.enabled && _agent.isOnNavMesh}, typeID={_agent.agentTypeID}), "
             + $"self={self} near({_sampleDistance:0.#}m)={selfNear} wide({DiagnosticWideDistance:0}m)={(selfWide ? selfWideHit.ToString() : "none")}, "
             + $"target={target} near({targetDistance:0.#}m)={targetNear} wide({DiagnosticWideDistance:0}m)={(targetWide ? targetWideHit.ToString() : "none")}, "
             + $"sceneNavMeshVertices={vertexCount}";
    }
    #endregion

    #region Private
    //Agentが有効でNavMeshに乗っている前提で、目的地を設定する（目的地がほとんど動いていないときは、経路を計算し直さない）
    //targetSampleDistance: 目的地の近くのNavMeshを探す範囲
    private bool SetAgentDestination(Vector3 point, float targetSampleDistance)
    {
        if (_hasDestination && (point - _lastPoint).sqrMagnitude < _repathDistance * _repathDistance)
        {
            return true;
        }

        if (!TrySamplePosition(point, out Vector3 destination, targetSampleDistance) || !_agent.SetDestination(destination))
        {
            return false;
        }

        _lastPoint = point;
        _hasDestination = true;
        return true;
    }

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
        if(!EnsureAgentType()) return false;
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
        return TrySamplePosition(point, out result, _sampleDistance);
    }

    private bool TrySamplePosition(Vector3 point, out Vector3 result, float maxDistance)
    {
        if(!EnsureAgentType())
        {
            result = point;
            return false;
        }
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _profile.HasValue ? _typeId : _agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, maxDistance, filter);
        result = hit.position;
        return found;
    }

    //ステージ読み込み後に、Agentの種類を、プロファイルのNavMeshに合わせる(ステージがかわったら合わせなおす)
    private bool EnsureAgentType()
    {
        if(!_profile.HasValue) return true;

        var set = StageNavMeshProvider.Current;
        if (set == null) return false;
        if (ReferenceEquals(set, _typeSet)) return true;
        if (!NavAgentProfile.ConfigureType(_agent, set, _profile.Value)) return false;

        set.TryGetAgentTypeId(_profile.Value,out int typeId);
        _typeId = typeId;
        _typeSet = set;
        _hasDestination = false;
        return true;
    }
    #endregion
}

/// <summary>Mover.FollowOnNavMeshの結果</summary>
public enum NavFollowResult
{
    Following,          //NavMeshで向かえている
    SelfOffNavMesh,     //自分の近くにNavMeshがなく、動けない
    TargetOffNavMesh,   //追従先の近くに、広めに探してもNavMeshがなく、動けない
}