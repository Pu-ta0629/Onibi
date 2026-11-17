using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 火1つぶんの窓口（MonoBehaviour）。見た目（ビルボード）・Stateの反映・移動の入口を持つ
/// Inspectorで持つのは「色(_color)」と「FireStateData(_data)」、シーン参照(_sprite・Light)だけ。
/// 移動速度・周回・明るさ・Effect・コライダーなどの情報は、全てFireStateDataから読む
/// 進化・退化のときは、MorphManagerがChangeDataで_dataを入れ替える（色は変わらない）
/// 中身は役割ごとのクラスに任せる
///   ・移動 ＝ Mover（共通）
///   ・周回の計算 ＝ FireOrbit
///   ・突進攻撃（索敵・木霊・クールタイム） ＝ FireDashAttack
///   ・Effect ＝ FireEffectPlayer
///   ・Stateの保持と変更 ＝ TypeManager（ここは通知された結果を受け取るだけで、Stateを自分では決めない）
/// 特殊木霊だけは、火ごとに種類が違い、進化のタイミングも火ごとに違うので、この火が持つ
/// 選択中でも固定中でもない火は、迷子の周りを円形に回り、攻撃対象を見つけたら突進する。HPは持たない
/// 明るさのレベルを実際の光に変えるのはLightManager
/// </summary>
public class FireController : MonoBehaviour
{
    private const int MaxFireCount = 7;   //火の最大数（周回の初期角度をずらすのに使う）

    #region Fields
    [Header("FireConfig")]
    [Tooltip("この火の色。FireManagerの所持リスト・ラジアルと対応する（None == NULL）。Editorで固定")]
    [SerializeField] private FireColor _color;
    [SerializeField] private GameObject _sprite;

    [Header("Combat")]
    [Tooltip("この火の最初のマスターデータ（種火）。以降の進化・退化では、MorphManagerが入れ替える")]
    [SerializeField] private FireStateData _data;

    [Header("Light")]
    [Tooltip("明るさ判定＋見た目用のSpotLight。CullingMaskコードでGroundのみにする。迷子用のPointLight。未設定なら自動で作る")]
    [SerializeField] private Light _spotLight;
    [Tooltip("迷子用のPointLight。未設定なら自動で作る")]
    [SerializeField] private Light _pointLight;

    private NavMeshAgent _agent;
    private Mover _mover;
    private Camera _billboardCamera;
    private FireOrbit _orbit;
    private FireDashAttack _dash;
    private FireEffectPlayer _effect;

    private FireState _state;
    private FireAIPermission _aiPermission;
    private SpecialKodamaKind _special = SpecialKodamaKind.None;   //この火が持つ特殊木霊
    private KodamaStock _kodama;   //この火が持つ木霊の所持量（火ごとに別。突進の消費も、進化・退化の判定も、この量で行う）

    private bool _isInitialized;

#if UNITY_EDITOR
    //Inspectorで見るためだけの表示（実行中に毎フレーム上書きされる。ここを書き換えても火には反映されない）
    [Header("Debug (表示専用・実行中に更新)")]
    [SerializeField] private FireKind _debugKind;
    [SerializeField] private int _debugKodamaCount;
    [SerializeField] private SpecialKodamaKind _debugSpecialKodama;
    private int _shownKodama;                  //最後に表示した値（これと違えば、Inspectorで書き換えられたと判断する）
    private SpecialKodamaKind _shownSpecial;
#endif
    #endregion

    #region Properties
    public FireColor Color => _color;
    public Vector3 Position => transform.position;
    public FireAIPermission AIPermission => _aiPermission;
    public FireBrightness Brightness => _state.Brightness;

    /// <summary>今のマスターデータ（進化・退化で入れ替わる）</summary>
    public FireStateData Data => _data;

    /// <summary>最初の明るさレベル（今のデータのもの）</summary>
    public FireBrightness initialBrightness => _data != null ? _data.InitialBrightness : FireBrightness.Level2;

    /// <summary>この火が持っている特殊木霊（Normal = 未取得）</summary>
    public SpecialKodamaKind SpecialKodama => _special;

    /// <summary>特殊木霊を取れるか。すでに持っていれば、神社でインタラクトできず、拾えない</summary>
    public bool CanAcquireSpecialKodama => _special == SpecialKodamaKind.None;

    /// <summary>この火が持つ木霊（火ごとに別。InitializeCombatの前はnull）</summary>
    public KodamaStock Kodama => _kodama;

    /// <summary>この火の特殊木霊が変わったときに通知する（FireRegistry→MorphManagerが、進化・退化の再判定に使う）</summary>
    public event Action OnSpecialKodamaChanged;

    public Light SpotLight => _spotLight;
    public Light PointLight => _pointLight;
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

        _mover = new Mover(_agent, _data.MoveSpeed, _data.GroundMask, _data.NavMeshSampleDistance, _data.DestinationUpdateDistance, FireStateData.Fire, _data.FollowSpeed);
        _agent.updateRotation = false;   //向きはビルボードで決めるので、Agentの回転は切る
        _orbit = new FireOrbit((int)_color * 360f / MaxFireCount);   //火ごとに角度をずらして重ならないようにする
        _isInitialized = true;
    }

    /// <summary>
    /// Initializeの後に、FireRegistryから呼ぶ。戦闘（当たり判定・索敵・突進）の準備をする
    /// world: 全員で共有するHitWorld / lostChild: 索敵結果を借りる迷子 / initialKodama: この火が最初に持つ木霊の量（木霊はこの火だけのもの）
    /// </summary>
    public void InitializeCombat(HitWorld world, LostChildController lostChild, int initialKodama)
    {
        if (!_isInitialized || world == null || lostChild == null || _data == null)
        {
            Debug.LogError($"[FireController] InitializeCombat : Reference is Null!!! color = {_color}");
            return;
        }

        _kodama = new KodamaStock(initialKodama);
#if UNITY_EDITOR
        //前回の再生で残った表示値を、変更と誤認しないよう、今の値で初期化する
        _debugKodamaCount = _shownKodama = _kodama.count;
        _debugSpecialKodama = _shownSpecial = _special;
#endif
        _dash = new FireDashAttack(this, transform, _data, _mover, world, lostChild, _kodama);
    }

    /// <summary>
    /// Initializeの後に、FireRegistryから呼ぶ。今のデータのEffectを、火の色で着色して出す
    /// </summary>
    public void InitializeEffect(FireEffectCatalog catalog)
    {
        if (!_isInitialized || catalog == null)
        {
            Debug.LogError($"[FireController] InitializeEffect : Not initialized or catalog is Null!!! color = {_color}");
            return;
        }

        _effect = new FireEffectPlayer(transform, _color, catalog);
        _effect.Apply(_data);
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

        if (_data == null)
        {
            Debug.LogError($"[FireController] Initialize : FireStateData is Null!!! color = {_color}");
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
        _sprite.transform.rotation = _billboardCamera.transform.rotation;
    }

    private void OnDestroy()
    {
        _dash?.Dispose();
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

#if UNITY_EDITOR
        SyncDebugFields();
#endif

        if (_dash != null && _dash.Tick(_aiPermission))
        {
            return;
        }

        if (!_aiPermission.CanMove)
        {
            return;
        }

        _mover.MoveTowards(_orbit.Step(center, _data.OrbitRadius, _data.OrbitSpeed));
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
    /// 進化・退化で、データを入れ替える（MorphManagerが、TypeManagerの認可の後に呼ぶ）
    /// 色・特殊木霊・シーンの参照はそのまま。移動の設定・戦闘（コライダー）・Effectを、新しいデータに合わせる
    /// </summary>
    public void ChangeData(FireStateData data)
    {
        if (!_isInitialized || data == null || data == _data)
        {
            return;
        }

        _data = data;
        _mover.Configure(data.MoveSpeed, data.GroundMask, data.NavMeshSampleDistance, data.DestinationUpdateDistance, data.FollowSpeed);
        _dash?.ChangeData(data);
        _effect?.Apply(data);
    }

    /// <summary>
    /// 特殊木霊を取得する（神社のインタラクト・特殊木霊アイテムを拾ったとき）。すでに持っていればfalse
    /// 進化の判定は、MorphManager側で行う（FireManagerが再判定を依頼する）
    /// </summary>
    public bool TryAcquireSpecialKodama(SpecialKodamaKind type)
    {
        if (type == SpecialKodamaKind.None || !CanAcquireSpecialKodama)
        {
            return false;
        }

        _special = type;
        OnSpecialKodamaChanged?.Invoke();
        return true;
    }

#if UNITY_EDITOR
    //Inspectorの表示欄と実際の値をそろえる
    //表示欄がさっき表示した値と違えば、Inspectorで書き換えられたので、実際の値へ反映する（木霊は火ごとなので、この火だけに効く）
    //変わらなければ、実際の値を表示欄へ写す
    private void SyncDebugFields()
    {
        if (_kodama != null)
        {
            _debugKodamaCount = Mathf.Max(0, _debugKodamaCount);
            int diff = _debugKodamaCount - _shownKodama;
            if (diff > 0)
            {
                _kodama.Add(diff);
            }
            else if (diff < 0)
            {
                _kodama.TryConsume(-diff);
            }

            _debugKodamaCount = _shownKodama = _kodama.count;
        }

        if (_debugSpecialKodama != _shownSpecial)
        {
            _special = _debugSpecialKodama;   //Noneにして、持っていない状態に戻すこともできる
            OnSpecialKodamaChanged?.Invoke();
        }

        _debugKind = _data.FireKind;
        _debugSpecialKodama = _shownSpecial = _special;
    }
#endif

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