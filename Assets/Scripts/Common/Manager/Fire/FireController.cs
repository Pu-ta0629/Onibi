using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 火1つぶんの挙動。Stateから変換した「AIが使ってよい行動の許可」を保持し、移動はMoverに任せる
/// 選択中でも固定中でもない火は、迷子の周りを円形に回り、攻撃対象を見つけたら突進して敵にダメージを与える
/// 突進するときは木霊を消費する。HPは持たない
/// 見た目（2Dスプライト）は、常にカメラの向きに合わせる（ビルボード）
/// 火の明るさはレベル（Level1-3）だけを持ち、実際の値はLightManagerが共通の設定から決める
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
    [Tooltip("この火の明るさレベル。各レベルの効果はLightManagerで共通に設定する。開始時に反映され、再生中に変えても反映される")]
    [SerializeField] private FireBrightness _brightness = FireBrightness.Level2;

    [Header("Combat")]
    [Tooltip("この火のマスターデータ（コライダー・木霊の消費量・クールタイム・火傷威力を読む）")]
    [SerializeField] private FireStateData _data;

    private readonly List<HitInfo> _hits = new List<HitInfo>();
    private readonly AttackProcessor _attackProcessor = new AttackProcessor();

    private NavMeshAgent _agent;
    private Mover _mover;
    private Camera _billboardCamera;

    private FireState _state;
    private FireAIPermission _aiPermission;

    private FireBrightness _syncedBrightness;   //TypeManagerへ渡し済みのInspectorの値
    private float _orbitAngle;   //周回中の角度（度）
    private bool _isInitialized;

    private HitDetector _hit;
    private FireTargetSearch _targetSearch;
    private KodamaStock _kodama;
    private float _cooldown;      //次の突進までの残り時間（秒）
    private float _facingYaw;     //突進の向き（度）。攻撃のコライダーの向きに使う
    private bool _isDashing;
    private bool _isCombatInitialized;
    #endregion

    #region Properties
    public FireColor color => _color;
    public Vector3 position => transform.position;
    public FireAIPermission aiPermission => _aiPermission;
    public FireBrightness brightness => _state.Brightness;

    /// <summary>Inspectorで設定した明るさレベル</summary>
    public FireBrightness inspectorBrightness => _brightness;
    #endregion

    #region Initialize
    /// <summary>
    /// FireManagerから呼ぶ。カメラはInitializeManagerが持つので、FireManager経由で受け取る
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

    /// <summary>
    /// Initializeの後に、FireManagerから呼ぶ。戦闘（当たり判定・索敵・突進）の準備をする
    /// world: 全員で共有するHitWorld / lostChild: 索敵結果を借りる迷子 / kodama: 突進で消費する木霊
    /// </summary>
    public void InitializeCombat(HitWorld world, LostChildController lostChild, KodamaStock kodama)
    {
        if (!_isInitialized || world == null || lostChild == null || kodama == null || _data == null)
        {
            Debug.LogError($"[FireController] InitializeCombat : Reference is Null!!! color = {_color}");
            return;
        }

        _kodama = kodama;
        _hit = new HitDetector(world, this, Faction.Fire, _data.Colliders);
        _targetSearch = new FireTargetSearch(new WeightedSearch<EnemyController>(_hit), lostChild);
        _isCombatInitialized = true;
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
        _hit?.Dispose();
    }
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

        if (_isCombatInitialized)
        {
            TickCombat();
            if (_isDashing)
            {
                return;
            }
        }

        if (!_aiPermission.CanMove)
        {
            return;
        }

        _orbitAngle = Mathf.Repeat(_orbitAngle + _orbitSpeed * Time.deltaTime, 360f);

        float radian = _orbitAngle * Mathf.Deg2Rad;
        Vector3 point = center + new Vector3(Mathf.Cos(radian), 0f, Mathf.Sin(radian)) * _orbitRadius;
        _mover.MoveTowards(point);
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

    /// <summary>
    /// 再生中にInspectorの明るさレベルが変えられていたら、その値を返してtrueにする（FireManagerが毎フレーム確認する）
    /// </summary>
    public bool TryConsumeBrightnessChange(out FireBrightness level)
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

    #region Combat
    //体の位置の更新、索敵、突進の開始と進行を行う
    private void TickCombat()
    {
        _hit.SetTransform(transform.position, _facingYaw);
        _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

        if (_isDashing)
        {
            TickDash();
            return;
        }

        if (_aiPermission.CanSearch)
        {
            _targetSearch.Tick(transform.position);
        }

        if (_aiPermission.CanDash && _cooldown <= 0f)
        {
            TryBeginDash();
        }
    }

    //攻撃対象（迷子と火の索敵結果を合わせた、一番重みが大きい敵）がいて、木霊が足りるなら、突進を始める
    private void TryBeginDash()
    {
        if (!_targetSearch.TryGetTarget(out SearchResult target))
        {
            return;
        }

        if (!_kodama.TryConsume(_data.NormalCost))
        {
            return;
        }

        Vector3 direction = target.Position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 1e-6f)
        {
            _facingYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        _mover.BeginStraightTo(target.Position);   //突進の目的地は、始めた瞬間の敵の位置
        _attackProcessor.Begin();
        _isDashing = true;
    }

    //目的地へ直進しながら、攻撃のコライダーで敵に当たったか調べる。着いたら終わる
    private void TickDash()
    {
        if (!_aiPermission.CanDash)
        {
            EndDash();
            return;
        }

        bool isMoving = _mover.MoveStraightToTarget();
        _hit.SetTransform(transform.position, _facingYaw);

        if (_hit.Detect<EnemyController>(_data.NormalAttackKey, _hits))
        {
            _attackProcessor.Apply(_hits, _data.BurnPotency);
        }

        if (!isMoving)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        _isDashing = false;
        _cooldown = _data.NormalCooldown;
    }
    #endregion
}