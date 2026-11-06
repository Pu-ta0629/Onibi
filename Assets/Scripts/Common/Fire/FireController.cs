using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// 火1つぶんの窓口（MonoBehaviour）。Inspectorの設定・見た目（ビルボード）・Stateの反映・移動の入口を持つ
/// 中身は役割ごとのクラスに任せる
///   ・移動 ＝ Mover（共通）
///   ・周回の計算 ＝ FireOrbit
///   ・突進攻撃（索敵・木霊・クールタイム） ＝ FireDashAttack
///   ・Stateの保持と変更 ＝ TypeManager（ここは通知された結果を受け取るだけで、Stateを自分では決めない）
/// 選択中でも固定中でもない火は、迷子の周りを円形に回り、攻撃対象を見つけたら突進する。HPは持たない
/// 明るさのレベルを実際の光に変えるのはLightManager。ここは初期レベルを持つだけ
/// </summary>
public class FireController : MonoBehaviour
{
    private const int MaxFireCount = 7;   //火の最大数（周回の初期角度をずらすのに使う）

    #region Fields
    [Header("FireConfig")]
    [Tooltip("この火の色。FireManagerの所持リスト・ラジアルと対応する（None == NULL）")]
    [SerializeField] private FireColor _color;

    [Tooltip("移動速度。Initialize時にMoverへ反映する")]
    [SerializeField, Range(0.1f, 20f)] private float _moveSpeed = 5f;

    [Header("NavMesh")]
    [Tooltip("目的地の近くのNavMeshを探す距離。地面の高さがずれていても届くよう、大きめにする")]
    [SerializeField, Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Tooltip("目的地がこの距離以上動いたら、経路を更新する（経路計算の回数を減らす）")]
    [SerializeField, Range(0.05f, 2f)] private float _destinationUpdateDistance = 0.3f;

    [Header("Ground")]
    [Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに合わせるのに使う")]
    [SerializeField] private LayerMask _groundMask;

    [Header("Orbit")]
    [Tooltip("迷子を中心に周回する円の半径")]
    [SerializeField, Range(0.5f, 20f)] private float _orbitRadius = 3f;

    [Tooltip("周回の速さ（度/秒）。移動速度より速く回ることはできない")]
    [SerializeField, Range(10f, 180f)] private float _orbitSpeed = 60f;

    [Header("Brightness")]
    [Tooltip("この火の最初の明るさレベル。各レベルの効果はLightManagerで共通に設定する。再生中に変えても、Stateへ反映される（デバッグ用）")]
    [FormerlySerializedAs("_brightness")]
    [SerializeField] private FireBrightness _initialBrightness = FireBrightness.Level2;

    [Header("Combat")]
    [Tooltip("この火のマスターデータ（コライダー・木霊の消費量・クールタイム・火傷威力を読む）")]
    [SerializeField] private FireStateData _data;

    private NavMeshAgent _agent;
    private Mover _mover;
    private Camera _billboardCamera;
    private FireOrbit _orbit;
    private FireDashAttack _dash;

    private FireState _state;
    private FireAIPermission _aiPermission;

    private FireBrightness _syncedBrightness;   //TypeManagerへ渡し済みのInspectorの値
    private bool _isInitialized;
    #endregion

    #region Properties
    /// <summary>再生中にInspectorで明るさレベルが変えられたときに通知する（デバッグ用）</summary>
    public event Action<FireColor, FireBrightness> OnBrightnessEdited;

    public FireColor color => _color;
    public Vector3 position => transform.position;
    public FireAIPermission aiPermission => _aiPermission;
    public FireBrightness brightness => _state.Brightness;

    /// <summary>Inspectorで設定した、最初の明るさレベル</summary>
    public FireBrightness initialBrightness => _initialBrightness;
    #endregion

    #region Initialize
    /// <summary>
    /// FireRegistryから呼ぶ。カメラはInitializeManagerが持つので、FireManager経由で受け取る
    /// </summary>
    public void Initialize(Camera mainCamera)
    {
        TryGetComponent(out _agent);
        _billboardCamera = mainCamera;

        if (!NullChecker())
        {
            return;
        }

        _syncedBrightness = _initialBrightness;
        _mover = new Mover(_agent, _moveSpeed, _groundMask, _navMeshSampleDistance, _destinationUpdateDistance);
        _agent.updateRotation = false;   //向きはビルボードで決めるので、Agentの回転は切る
        _orbit = new FireOrbit((int)_color * 360f / MaxFireCount);   //火ごとに角度をずらして重ならないようにする
        _isInitialized = true;
    }

    /// <summary>
    /// Initializeの後に、FireRegistryから呼ぶ。戦闘（当たり判定・索敵・突進）の準備をする
    /// world: 全員で共有するHitWorld / lostChild: 索敵結果を借りる迷子 / kodama: 突進で消費する木霊
    /// </summary>
    public void InitializeCombat(HitWorld world, LostChildController lostChild, KodamaStock kodama)
    {
        if (!_isInitialized || world == null || lostChild == null || kodama == null || _data == null)
        {
            Debug.LogError($"[FireController] InitializeCombat : Reference is Null!!! color = {_color}");
            return;
        }

        _dash = new FireDashAttack(this, transform, _data, _mover, world, lostChild, kodama);
    }

    //Initializeに必要な参照が揃っているか確認する。足りなければエラーを出してfalse
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

        if (_color == FireColor.None)
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

    private void OnDestroy()
    {
        _dash?.Dispose();
    }

#if UNITY_EDITOR
    //再生中にInspectorで明るさレベルが変えられたら、TypeManagerへ反映してもらうために通知する
    private void OnValidate()
    {
        if (!Application.isPlaying || !_isInitialized || _initialBrightness == _syncedBrightness)
        {
            return;
        }

        _syncedBrightness = _initialBrightness;
        OnBrightnessEdited?.Invoke(_color, _initialBrightness);
    }
#endif
    #endregion

    #region Public
    /// <summary>
    /// 毎フレーム、周回の中心（迷子の座標）を渡す。移動が許可されていれば、中心の周りの円を回る
    /// 突進中は、周回せずに突進を進める。周回はNavMeshを使わず、円周上の点へ直進で向かう（軽量）
    /// </summary>
    public void Tick(Vector3 center)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (_dash != null && _dash.Tick(_aiPermission))
        {
            return;
        }

        if (!_aiPermission.CanMove)
        {
            return;
        }

        _mover.MoveTowards(_orbit.Step(center, _orbitRadius, _orbitSpeed));
    }

    /// <summary>
    /// Stateが変わったときに呼ぶ。これまでの経路を捨てて、Stateに合わせて移動を許可／停止する
    /// 選択中の火はマウスを追って動く（固定中は止まる）。選択中でなければAIの許可に従う
    /// </summary>
    public void ApplyState(FireState state)
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
    #endregion
}
