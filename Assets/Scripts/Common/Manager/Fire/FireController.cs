using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 火1体ぶんの挙動。Stateから変換した「AIが使える行動の許可」を保持し、移動はMoverに任せる
/// 選択中でも固定中でもない火は、迷子の周りを円形に動き続ける（円の計算はここで行う）
/// 見た目（2Dスプライト）は、常にカメラの向きに合わせる（ビルボード）
/// 火自体が発光体。持つのは明るさのレベル（Level1-3）だけで、光の値はLightManagerが共通の設定から決める
/// </summary>
public class FireController : MonoBehaviour
{
    private const int MaxFireCount = 7;   //火の最大数（周回の初期角度をずらすのに使う）

    #region Fields
    [Header("FireConfig")]
    [Tooltip("この火の色。FireManagerの所持リスト・ラジアルと対応する（None == NULL）")]
    [SerializeField] private TypeManager.FireColor _color;

    [Tooltip("移動速度。Initialize時にNavMeshAgentのSpeedへ反映する")]
    [SerializeField, Range(0.1f, 20f)] private float _moveSpeed = 5f;

    [Header("NavMesh")]
    [Tooltip("目的地の近くでNavMeshを探す距離。地面の高さが少しずれていても届く大きさにする")]
    [SerializeField, Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Tooltip("目的地がこの距離以上動いたら、経路を更新する（経路計算の回数を減らす）")]
    [SerializeField, Range(0.05f, 2f)] private float _destinationUpdateDistance = 0.3f;

    [Header("Ground")]
    [Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに沿わせるのに使う")]
    [SerializeField] private LayerMask _groundMask;

    [Header("Orbit")]
    [Tooltip("迷子を中心に周回する円の半径")]
    [SerializeField, Range(0.5f, 20f)] private float _orbitRadius = 3f;

    [Tooltip("周回の速さ（度/秒）。移動速度より速い周回はできない")]
    [SerializeField, Range(10f, 180f)] private float _orbitSpeed = 60f;

    [Header("Brightness")]
    [Tooltip("この火の明るさレベル。各レベルの光量はLightManagerで共通に設定する。開始時に反映され、再生中に変えるとすぐ反映される")]
    [SerializeField] private TypeManager.FireBrightness _brightness = TypeManager.FireBrightness.Level2;

    private NavMeshAgent _agent;
    private Mover _mover;
    private Camera _billboardCamera;

    private TypeManager.FireState _state;
    private TypeManager.FireAIPermission _aiPermission;

    private TypeManager.FireBrightness _syncedBrightness;   //TypeManagerへ渡し済みのInspectorの値
    private float _orbitAngle;   //周回中の角度（度）
    private bool _isInitialized;
    #endregion

    #region Properties
    public TypeManager.FireColor color => _color;
    public Vector3 position => transform.position;
    public TypeManager.FireAIPermission aiPermission => _aiPermission;
    public TypeManager.FireBrightness brightness => _state.Brightness;

    /// <summary>Inspectorで設定した明るさレベル</summary>
    public TypeManager.FireBrightness inspectorBrightness => _brightness;
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

        _syncedBrightness = _brightness;
        _mover = new Mover(_agent, _moveSpeed, _groundMask, _navMeshSampleDistance, _destinationUpdateDistance);
        _agent.updateRotation = false;   //向きはビルボードで決めるので、Agentの回転は切る
        _orbitAngle = (int)_color * 360f / MaxFireCount;   //火ごとに角度をずらして重ならないようにする
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
    #endregion

    #region UnityLifeCycle
    //カメラが動いた後に向きを合わせるため、LateUpdateで行う
    private void LateUpdate()
    {
        if (!_isInitialized)
        {
            return;
        }

        //カメラと完全に同じ向きにそろえ、スプライトがカメラに正対するようにする
        transform.rotation = _billboardCamera.transform.rotation;
    }
    #endregion

    #region Public
    /// <summary>
    /// 毎フレーム、周回の中心（迷子の座標）を渡す。移動が許可されている火だけ、中心の周りの円を回る
    /// 周回はNavMeshを使わず、円周上の点へ直進で向かう（軽い）
    /// </summary>
    public void Tick(Vector3 center)
    {
        if (!_isInitialized || !_aiPermission.CanMove)
        {
            return;
        }

        _orbitAngle = Mathf.Repeat(_orbitAngle + _orbitSpeed * Time.deltaTime, 360f);

        float radian = _orbitAngle * Mathf.Deg2Rad;
        Vector3 point = center + new Vector3(Mathf.Cos(radian), 0f, Mathf.Sin(radian)) * _orbitRadius;
        _mover.MoveTowards(point);
    }

    /// <summary>
    /// Stateが変わったときに呼ぶ。それまでの経路を捨てて、Stateに合わせて移動を許可する／止める
    /// 選択中の火はマウスを追って動く（固定中は止まる）。選択中でなければAIの許可に従う
    /// </summary>
    public void ApplyState(TypeManager.FireState state)
    {
        _state = state;
        _aiPermission = TypeManager.ToAIPermission(state);

        if (_isInitialized)
        {
            _mover.SetCanMove(_state.IsSelected ? !_state.IsFixed : _aiPermission.CanMove);
        }
    }

    /// <summary>
    /// 指定した地面の座標を目的地にして移動させる（選択中の火がマウスを追いかける用）。目的地にできなければfalse
    /// </summary>
    public bool MoveTo(Vector3 point)
    {
        return _isInitialized && _mover.Follow(point);
    }

    /// <summary>
    /// 今の経路を捨てて、その場で止まる（ラジアルを開いている間など）
    /// </summary>
    public void StopMoving()
    {
        if (_isInitialized)
        {
            _mover.Stop();
        }
    }

    /// <summary>
    /// 再生中にInspectorの明るさレベルが変えられていたら、その値を返してtrueにする（FireManagerが毎フレーム確認する）
    /// </summary>
    public bool TryConsumeBrightnessChange(out TypeManager.FireBrightness level)
    {
        level = _brightness;
        if (!_isInitialized || _brightness == _syncedBrightness)
        {
            return false;
        }

        _syncedBrightness = _brightness;
        return true;
    }
    #endregion
}