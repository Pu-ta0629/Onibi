using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

/// <summary>
/// 火に関する管理の窓口（MonoBehaviour）。Inspectorの設定を受け取り、役割ごとのクラスを組み立てて、毎フレームの順番を決める
///   ・FireRegistry ＝ 火の登録・取得
///   ・FireStateBinder ＝ Stateの変更を火とLightへ配る
///   ・FireInputHandler ＝ マウス操作（固定・追従モード・選択中の火の移動）
///   ・LostChildFollowLink ＝ 迷子へ追従先を渡す
///   ・FireRadialMenuBinder ＝ ラジアルメニューとの連携
/// Stateの取得・変更はすべてTypeManager経由で行う
/// </summary>
public class FireManager : MonoBehaviour
{
    [Header("Fires")]
    [Tooltip("シーンに置いてある火。所持している色だけ登録される")]
    [SerializeField] private FireController[] _sceneFires;
    [Tooltip("ステージ開始時にラジアルで選択済みにする火（迷子の最初の追従先になる）")]
    [SerializeField] private FireColor _initialSelectColor = FireColor.Red;

    [Header("RadialMenu")]
    [SerializeField] private RadialMenu _radialMenu;
    [Tooltip("ラジアルに表示する火の文字・アイコン。設定のない火は色の名前で表示する")]
    [SerializeField] private FireRadialEntry[] _radialEntries;

    [Header("Light")]
    [Tooltip("火に付属するLightを管理する。明るさレベルごとの光量は全ての火で共通で、ここで設定する")]
    [SerializeField] private LightManager _lightManager;

    [Header("Darkness")]
    [Tooltip("迷子の周りの暗さの判定設定。暗いと迷子は追従しなくなる")]
    [SerializeField] private DarknessConfig _darknessConfig = new DarknessConfig();

    private TypeManager _typeManager;
    private LostChildController _lostChild;
    private FireInventory _fireInventory;
    private FireRegistry _registry;
    private FireStateBinder _stateBinder;
    private FireInputHandler _inputHandler;
    private LostChildFollowLink _followLink;
    private FireRadialMenuBinder _radialMenuBinder;
    private DarknessSensor _darkness;
    private DarkDamageReaction _darkDamage;
    private bool _isInitialized;

    /// <summary>ラジアルのMenu項目が選ばれたときに通知する（Menu画面を開く側が購読する）</summary>
    public event System.Action OnMenuRequested;

    public FireInventory fireInventory => _fireInventory;

    /// <summary>迷子の周りの暗さの検知（暗いときの処理はAddReactionで追加できる）</summary>
    public DarknessSensor darkness => _darkness;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。hitWorld: 全員で共有する当たり判定の世界 / kodamaStock: 火が突進で消費する木霊
    /// </summary>
    public void Initialize(TypeManager typeManager, InputManager inputManager, LostChildController lostChild, Camera mainCamera, HitWorld hitWorld, KodamaStock kodamaStock)
    {
        if (typeManager == null || inputManager == null || lostChild == null || mainCamera == null || hitWorld == null || kodamaStock == null || _radialMenu == null || _lightManager == null)
        {
            Debug.LogError("[FireManager] Initialize : Reference is Null!!! ");
            return;
        }

        _typeManager = typeManager;
        _lostChild = lostChild;

        _fireInventory = new FireInventory();
        _fireInventory.Initialize();
        _lightManager.Initialize();
        _lightManager.SetDarkThreshold(_darknessConfig.DarkThreshold);

        //迷子の周りの明るさを検知する。明るいと停止、暗いとダメージ。やりたいことが増えたら、ここにReactionを足す
        _darkness = new DarknessSensor(_lostChild.transform, _lightManager.GetLightLevelAt, _darknessConfig);
        _darkness.AddBrightReaction(new StopFollowReaction(_lostChild));
        _darkDamage = new DarkDamageReaction(_lostChild, _darknessConfig);
        _darkness.AddReaction(_darkDamage);

        _registry = new FireRegistry(_typeManager, _lightManager, mainCamera, hitWorld, _lostChild, kodamaStock);
        _stateBinder = new FireStateBinder(_typeManager, _registry, _lightManager);
        _inputHandler = new FireInputHandler(_typeManager, inputManager, _registry);
        _followLink = new LostChildFollowLink(_typeManager, _registry, _lostChild, _darkness);
        _isInitialized = true;

        RegisterInitialFires();
        SelectInitialFire();

        //所持している火をラジアルの項目として登録する（選択済みの火の反映もここで行う）
        _radialMenuBinder = new FireRadialMenuBinder();
        _radialMenuBinder.Initialize(_radialMenu, _typeManager, _fireInventory, this, _radialEntries);
    }

    //初期装備の火を登録する。所持している色だけ登録される
    private void RegisterInitialFires()
    {
        if (_sceneFires == null)
        {
            return;
        }

        for (int i = 0; i < _sceneFires.Length; i++)
        {
            if (_sceneFires[i] != null && _fireInventory.Has(_sceneFires[i].Color))
            {
                _registry.Register(_sceneFires[i]);
            }
        }
    }

    //ステージ開始時に、指定した火（通常は赤）を選択済みにする。これで迷子の追従先も決まる
    private void SelectInitialFire()
    {
        if (_initialSelectColor != FireColor.None)
        {
            ToggleSelectFire(_initialSelectColor);
        }
    }
    #endregion

    #region Public
    /// <summary>
    /// 火を新しく獲得したときに呼ぶ。所持リストへ追加し、ラジアルに項目が増える
    /// </summary>
    public bool AcquireFire(FireController fire)
    {
        if (!_isInitialized || fire == null)
        {
            Debug.LogError("[FireManager] AcquireFire : Not initialized or fire is Null!!! ");
            return false;
        }

        if (!_fireInventory.TryAdd(fire.Color))
        {
            Debug.LogWarning($"[FireManager] AcquireFire : {fire.Color} is already owned!!");
            return false;
        }

        _registry.Register(fire);
        return true;
    }

    /// <summary>
    /// ラジアルメニューで火が選ばれたときに呼ぶ（選択中の火をもう一度選ぶと選択解除）
    /// </summary>
    public void ToggleSelectFire(FireColor color)
    {
        if (!_registry.Has(color))
        {
            Debug.LogWarning($"[FireManager] ToggleSelectFire : {color} is not registered!!");
            return;
        }

        _typeManager.ToggleSelectFire(color);
    }

    /// <summary>
    /// ラジアルメニューを開いた・閉じたときに呼ぶ（開いている間、選択中の火は停止する）
    /// </summary>
    public void SetRadialOpen(bool isOpen)
    {
        _typeManager.SetRadialOpen(isOpen);
    }

    /// <summary>
    /// 指定した地点の光量（0-15）。所持している全ての火のうち、最も明るい値を返す
    /// </summary>
    public float GetLightLevelAt(Vector3 point)
    {
        return _lightManager.GetLightLevelAt(point);
    }

    /// <summary>
    /// ラジアルのMenu項目が選ばれたときに呼ぶ。Menu画面を開く依頼を通知する
    /// </summary>
    public void RequestMenu()
    {
        Debug.Log("[FireManager] RequestMenu : Menu is requested");
        OnMenuRequested?.Invoke();
    }
    #endregion

    #region UnityLifeCycle
    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        _inputHandler.Tick();            //選択中の火のマウス追従
        _darkness.Tick();                //迷子の周りの暗さの判定
        _darkDamage.Tick(Time.deltaTime);//慈姑間のダメージ
        _followLink.Tick();              //迷子へ追従先を渡す
        _lostChild.UpdateCombat();       //火が迷子の索敵結果を読むので、火のTickより先に更新する
        TickFires();
    }

    private void OnDestroy()
    {
        _radialMenuBinder?.Dispose();
        _followLink?.Dispose();
        _inputHandler?.Dispose();
        _stateBinder?.Dispose();
        _registry?.Dispose();
    }
    #endregion

    #region Private
    //火のAIを進める。迷子の座標を中心に渡す（動いてよいかの判定は各火が行う）
    private void TickFires()
    {
        Vector3 center = _lostChild.transform.position;
        for (int i = 0; i < _registry.all.Count; i++)
        {
            _registry.all[i].Tick(center);
        }
    }
    #endregion
}
