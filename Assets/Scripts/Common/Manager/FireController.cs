using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 火1体ぶんの挙動。Stateから変換した「AIが使える行動の許可」を保持する
/// 今後のAI（索敵・移動・攻撃）は、行動の前にaiPermissionを確認する
/// 見た目（2Dスプライト）は、常にカメラの向きに合わせる（ビルボード）
/// </summary>
public class FireController : MonoBehaviour
{
    [Header("FireConfig")]
    [SerializeField] private TypeManager.FireColor _color;
    [Tooltip("マウス位置の近くでNavMeshを探す距離。地面の高さが少しずれていても届く大きさにする")]
    [SerializeField,Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;
    [Tooltip("マウス位置がこの距離以上動いたら、目的地を更新する（経路計算の回数を減らす）")]
    [SerializeField, Range(0.05f, 2f)] private float _destinationUpdareDistance = 0.3f;
    private NavMeshAgent _agent;

    [Header("Billboard")]
    [Tooltip("カメラに向けるスプライトのTransform。子オブジェクト推奨。未設定なら自分自身")]
    [SerializeField] private Transform _billboardTarget;

    private Camera _billboardCamera;
    private TypeManager.FireState _state;
    private TypeManager.FireAIPermission _aiPermission;
    private Vector3 _lastTarget;
    private bool _hasDestination;

    public TypeManager.FireColor color => _color;
    public Vector3 position => transform.position;
    public TypeManager.FireAIPermission aiPermission => _aiPermission;

    #region Initialize
    /// <summary>
    /// FireManagerで呼ぶ。カメラはInitializeManagerが持つものをFireManager経由で受け取る
    /// </summary>
    public void Initialize(Camera mainCamera)
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null && !TryGetComponent(out _agent))
        {
            Debug.LogError($"[FireController] Initialize : NavMeshAgent is Null!!! color = {_color}");
        }

        InitializeBillboard(mainCamera);
    }

    //ビルボードの準備。対象とカメラを決める
    private void InitializeBillboard(Camera mainCamera)
    {
        if (_billboardTarget == null)
        {
            _billboardTarget = transform;
        }

        _billboardCamera = mainCamera;
        if (_billboardCamera == null)
        {
            Debug.LogError($"[FireController] InitializeBillboard : Camera is Null!!! color = {_color}");
        }

        //自分自身を回す場合、Agentが進行方向へ回転させるとビルボードと競合するので、Agentの回転は切る
        if (_billboardTarget == transform && _agent != null)
        {
            _agent.updateRotation = false;
        }
    }
    #endregion

    #region UnityLifeCycle
    //カメラが動いた後に向きを合わせるため、LateUpdateで行う
    private void LateUpdate()
    {
        if (_billboardCamera == null)
        {
            return;
        }

        //カメラと完全に同じ向き（角度も含めて）にそろえ、スプライトがカメラに正対するようにする
        _billboardTarget.rotation = _billboardCamera.transform.rotation;
    }
    #endregion

    #region Public
    /// <summary>
    /// 
    /// </summary>
    public void ApplyState(TypeManager.FireState state)
    {
        _state = state;
        _aiPermission = TypeManager.ToAIPermission(state);
        RefreshAgent();
    }

    /// <summary>
    /// 指定した地面の座標へ火を移動させる（選択中の火をマウスに追従させる用）
    /// 近くにNavMeshがなければ（NavMeshの外を指しているときなど）falseを返し、動かさない
    /// </summary>
    public bool MoveTo(Vector3 point)
    {
        if (_agent == null)
        {
            return false;
        }

        //Warpは、NavMeshから遠い座標だとエラーを出す。先に近くのNavMesh上の点を探し、その点へ移動する
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, _navMeshSampleDistance, filter))
        {
            return false;
        }

        bool moved = _agent.Warp(hit.position);
        if (moved)
        {
            //NavMesh上に乗れた直後に、Stateに合わせたAgentの停止状態を反映し直す
            RefreshAgent();
        }

        return moved;
    }
    #endregion

    #region Private
    //AIの許可に合わせてAgentを止める／動かす。NavMesh上にいないときは何もしない
    private void RefreshAgent()
    {
        if (_agent == null || !_agent.isOnNavMesh)
        {
            return;
        }

        if (!_aiPermission.CanMove)
        {
            _agent.ResetPath();
        }

        _agent.isStopped = !_aiPermission.CanMove;
    }
    #endregion
}