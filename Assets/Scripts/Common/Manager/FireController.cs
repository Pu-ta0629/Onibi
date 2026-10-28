using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 火1体ぶんの挙動。Stateから変換した「AIが使える行動の許可」を保持する
/// 今後のAI（索敵・移動・攻撃）は、行動の前にaiPermissionを確認する
/// 見た目（2Dスプライト）は、常にカメラの向きに合わせる（ビルボード）
/// </summary>
public class FireController : MonoBehaviour
{
    #region Fields
    [Header("FireConfig")]
    [Tooltip("この火の色。FireManagerの所持リスト・ラジアルと対応する（None == NULL）")]
    [SerializeField] private TypeManager.FireColor _color;

    [Tooltip("移動速度。Initialize時にNavMeshAgentのSpeedへ反映する")]
    [SerializeField, Range(0.1f, 20f)] private float _moveSpeed = 5f;

    [Header("NavMesh")]
    [Tooltip("マウス位置の近くでNavMeshを探す距離。地面の高さが少しずれていても届く大きさにする")]
    [SerializeField, Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Tooltip("マウス位置がこの距離以上動いたら、目的地を更新する（経路計算の回数を減らす）")]
    [SerializeField, Range(0.05f, 2f)] private float _destinationUpdateDistance = 0.3f;

    private NavMeshAgent _agent;
    private Camera _billboardCamera;
    private Transform _billboardTarget;

    private TypeManager.FireState _state;
    private TypeManager.FireAIPermission _aiPermission;

    private Vector3 _lastTarget;
    private bool _hasDestination;
    private bool _hasWarnedOffNavMesh;
    private bool _isInitialized;
    #endregion

    #region Properties
    public TypeManager.FireColor color => _color;
    public Vector3 position => transform.position;
    public TypeManager.FireAIPermission aiPermission => _aiPermission;
    #endregion

    #region Initialize
    /// <summary>
    /// FireManagerから呼ぶ。カメラはInitializeManagerが持つものをFireManager経由で受け取る
    /// </summary>
    public void Initialize(Camera mainCamera)
    {
        TryGetComponent(out _agent);
        _billboardCamera = mainCamera;

        if (!NullChecker())
        {
            return;
        }

        _agent.speed = _moveSpeed;
        InitializeBillboard();
        _isInitialized = true;
    }

    //Initializeに必要な参照がそろっているか確認する。足りなければエラーを出してfalse
    private bool NullChecker()
    {
        bool isValid = true;

        if (_agent == null)
        {
            Debug.LogError($"[FireController] Initialize : NavMeshAgent is Null!!! color = {_color}");
            isValid = false;
        }

        if (_billboardCamera == null)
        {
            Debug.LogError($"[FireController] Initialize : Camera is Null!!! color = {_color}");
            isValid = false;
        }

        if (_color == TypeManager.FireColor.None)
        {
            Debug.LogError($"[FireController] Initialize : FireColor is None!!! name = {name}");
            isValid = false;
        }

        return isValid;
    }

    //ビルボードの対象を決める。未設定なら自分自身
    private void InitializeBillboard()
    {
        _billboardTarget = transform;

        //自分自身を回す場合、Agentが進行方向へ回転させるとビルボードと競合するので、Agentの回転は切る
        if (_billboardTarget == transform)
        {
            _agent.updateRotation = false;
        }
    }
    #endregion

    #region UnityLifeCycle
    //カメラが動いた後に向きを合わせるため、LateUpdateで行う
    private void LateUpdate()
    {
        if (!_isInitialized)
        {
            return;
        }

        //カメラと完全に同じ向き（角度も含めて）にそろえ、スプライトがカメラに正対するようにする
        _billboardTarget.rotation = _billboardCamera.transform.rotation;
    }
    #endregion

    #region Public
    /// <summary>
    /// Stateが変わったときに呼ぶ。それまでの経路を捨てて、Stateに合わせてAgentを止める／動かす
    /// </summary>
    public void ApplyState(TypeManager.FireState state)
    {
        _state = state;
        _aiPermission = TypeManager.ToAIPermission(state);
        RefreshAgent();
    }

    /// <summary>
    /// 指定した地面の座標を目的地にして、NavMeshAgentで移動させる（選択中の火がマウスを追いかける用）
    /// 近くにNavMeshがなければ（NavMeshの外を指しているときなど）falseを返し、目的地は変えない
    /// </summary>
    public bool MoveTo(Vector3 point)
    {
        //NavMesh上にいなければ、近くのNavMesh上へ乗せる。乗れなければ動かせない
        if (!EnsureOnNavMesh())
        {
            return false;
        }

        //ほとんど動いていないマウスのたびに、経路を計算し直さない
        if (_hasDestination && (point - _lastTarget).sqrMagnitude < _destinationUpdateDistance * _destinationUpdateDistance)
        {
            return true;
        }

        //座標がNavMeshから遠いと目的地にできないので、近くのNavMesh上の点を探してそこへ向かう
        if (!TrySamplePosition(point, out Vector3 destination))
        {
            return false;
        }

        if (!_agent.SetDestination(destination))
        {
            return false;
        }

        _lastTarget = point;
        _hasDestination = true;
        return true;
    }

    /// <summary>
    /// 今の経路を捨てて、その場で止まる（ラジアルを開いている間など）
    /// </summary>
    public void StopMoving()
    {
        _hasDestination = false;

        if (_isInitialized && _agent.isOnNavMesh && _agent.hasPath)
        {
            _agent.ResetPath();
        }
    }
    #endregion

    #region Private
    //Stateに合わせてAgentを止める／動かす。NavMesh上にいないときは何もしない
    //選択中の火はマウスを追って動く（固定中は止まる）。選択中でなければAIの許可に従う
    private void RefreshAgent()
    {
        if (!_isInitialized || !_agent.isOnNavMesh)
        {
            return;
        }

        StopMoving();

        bool canMove = _state.IsSelected ? !_state.IsFixed : _aiPermission.CanMove;
        _agent.isStopped = !canMove;
    }

    //NavMesh上にいなければ、今の位置の近くのNavMesh上へ乗せる。近くにNavMeshがなければfalse
    private bool EnsureOnNavMesh()
    {
        if (!_isInitialized)
        {
            return false;
        }

        if (_agent.isOnNavMesh)
        {
            return true;
        }

        if (TryPlaceOnNavMesh())
        {
            return true;
        }

        WarnOffNavMesh();
        return false;
    }

    //Agentを一度無効にして位置を置き直し、NavMesh上に乗せる（Warpより許容範囲が広い）
    private bool TryPlaceOnNavMesh()
    {
        if (!TrySamplePosition(transform.position, out Vector3 onNavMesh))
        {
            return false;
        }

        _agent.enabled = false;
        transform.position = onNavMesh;
        _agent.enabled = true;

        if (!_agent.isOnNavMesh)
        {
            return false;
        }

        Debug.Log($"[FireController] EnsureOnNavMesh : {_color} was placed on NavMesh. position = {onNavMesh}");
        RefreshAgent();   //乗れた時点のStateを反映する
        return true;
    }

    //NavMesh上に乗れないときの警告。毎フレーム出ないよう、最初の1回だけ出す
    private void WarnOffNavMesh()
    {
        if (_hasWarnedOffNavMesh)
        {
            return;
        }

        _hasWarnedOffNavMesh = true;
        Debug.LogWarning(
            $"[FireController] EnsureOnNavMesh : {_color} is not near NavMesh!! " +
            $"position = {transform.position}, " +
            $"sampleDistance = {_navMeshSampleDistance}, " +
            $"agentTypeID = {_agent.agentTypeID}");
    }

    //指定した座標の近くにあるNavMesh上の点を探す（このAgentの種類で探す）
    private bool TrySamplePosition(Vector3 point, out Vector3 result)
    {
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, _navMeshSampleDistance, filter);
        result = hit.position;
        return found;
    }
    #endregion
}