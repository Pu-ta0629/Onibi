using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火に関する管理をまとめるクラス
/// シーン上の火・所持リスト・初期選択・ラジアルメニューとの連携・マウスでの操作（選択／固定／追従）・迷子への追従先の受け渡しを行う
/// Stateの取得・変更はすべてTypeManager経由で行う
/// </summary>
public class FireManager : MonoBehaviour
{
    [Header("Fires")]
    [Tooltip("シーンに置いてある火。所持している色だけ登録される")]
    [SerializeField] private FireController[] _sceneFires;
    [Tooltip("ステージ開始時にラジアルで選択済みにする火（迷子の最初の追従先になる）")]
    [SerializeField] private TypeManager.FireColor _initialSelectColor = TypeManager.FireColor.Red;

    [Header("RadialMenu")]
    [SerializeField] private RadialMenu _radialMenu;
    [Tooltip("ラジアルに表示する火の文字・アイコン。設定のない火は色の名前で表示する")]
    [SerializeField] private FireRadialEntry[] _radialEntries;

    private readonly Dictionary<TypeManager.FireColor, FireController> _fires = new Dictionary<TypeManager.FireColor, FireController>();
    private TypeManager _typeManager;
    private InputManager _inputManager;
    private LostChildController _maigoController;
    private Camera _mainCamera;
    private FireInventory _fireInventory;
    private FireRadialMenuBinder _radialMenuBinder;
    private bool _isInitialized;

    public FireInventory fireInventory => _fireInventory;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize(TypeManager typeManager, InputManager inputManager, LostChildController maigoController, Camera mainCamera)
    {
        if (typeManager == null || inputManager == null || maigoController == null || mainCamera == null || _radialMenu == null)
        {
            Debug.LogError("[FireManager] Initialize : Reference is Null!!! ");
            return;
        }

        _typeManager = typeManager;
        _inputManager = inputManager;
        _maigoController = maigoController;
        _mainCamera = mainCamera;

        _fireInventory = new FireInventory();
        _fireInventory.Initialize();

        _typeManager.OnFireStateChanged += HandleFireStateChanged;
        _typeManager.OnFollowTargetChanged += HandleFollowTargetChanged;
        _inputManager.OnLeftClick += HandleLeftClick;
        _inputManager.OnMiddleClick += HandleMiddleClick;
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
            if (_sceneFires[i] != null && _fireInventory.Has(_sceneFires[i].color))
            {
                RegisterFire(_sceneFires[i]);
            }
        }
    }

    //ステージ開始時に、指定した火（通常は赤）を選択済みにする。これで迷子の追従先も決まる
    private void SelectInitialFire()
    {
        if (_initialSelectColor != TypeManager.FireColor.None)
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

        if (!_fireInventory.TryAdd(fire.color))
        {
            Debug.LogWarning($"[FireManager] AcquireFire : {fire.color} is already owned!!");
            return false;
        }

        RegisterFire(fire);
        return true;
    }

    /// <summary>
    /// ラジアルメニューで火が選ばれたときに呼ぶ（選択中の火をもう一度選ぶと選択解除）
    /// </summary>
    public void ToggleSelectFire(TypeManager.FireColor color)
    {
        if (!_fires.ContainsKey(color))
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
    #endregion

    #region UnityLifeCycle
    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        MoveSelectedFire();
        UpdateLostChildTarget();
    }

    private void OnDestroy()
    {
        _radialMenuBinder?.Dispose();

        if (!_isInitialized)
        {
            return;
        }

        _typeManager.OnFireStateChanged -= HandleFireStateChanged;
        _typeManager.OnFollowTargetChanged -= HandleFollowTargetChanged;
        _inputManager.OnLeftClick -= HandleLeftClick;
        _inputManager.OnMiddleClick -= HandleMiddleClick;
    }
    #endregion

    #region Private
    private void RegisterFire(FireController fire)
    {
        if (fire.color == TypeManager.FireColor.None)
        {
            Debug.LogError($"[FireManager] RegisterFire : FireColor is None!!! name = {fire.name}");
            return;
        }

        if (_fires.ContainsKey(fire.color))
        {
            Debug.LogWarning($"[FireManager] RegisterFire : {fire.color} is already registered!!");
            return;
        }

        fire.Initialize(_mainCamera);
        _fires.Add(fire.color, fire);
        fire.ApplyState(_typeManager.GetFireState(fire.color));
    }
    #endregion

    #region Update
    //選択中かつ固定されていない火に、マウス位置の地面を目的地として渡す（火はNavMeshAgentでそこへ向かう）
    private void MoveSelectedFire()
    {
        TypeManager.FireColor color = _typeManager.SelectedColor;
        if (color == TypeManager.FireColor.None || !_fires.TryGetValue(color, out FireController fire))
        {
            return;
        }

        //ラジアルを開いている間は、その場で止める
        if (_typeManager.IsRadialOpen)
        {
            fire.StopMoving();
            return;
        }

        //固定中は、Stateが変わった時点で止まっているので何もしない
        if (_typeManager.GetFireState(color).IsFixed)
        {
            return;
        }

        if (_inputManager.TryGetGroundPoint(out Vector3 point))
        {
            fire.MoveTo(point);
        }
    }

    //迷子へ追従先の座標を渡す
    private void UpdateLostChildTarget()
    {
        TypeManager.FireColor color = _typeManager.FollowTarget;
        if (color == TypeManager.FireColor.None)
        {
            return;
        }

        if (_fires.TryGetValue(color, out FireController fire))
        {
            _maigoController.Tick(fire.position);
        }
    }
    #endregion

    #region Handler
    private void HandleLeftClick()
    {
        //ラジアルを開いている間のクリックは固定の切り替えに使わない
        if (_typeManager.IsRadialOpen)
        {
            return;
        }

        _typeManager.ToggleFixedSelectedFire();
    }

    private void HandleMiddleClick()
    {
        _typeManager.ToggleFollowMode();
    }

    private void HandleFireStateChanged(TypeManager.FireColor color, TypeManager.FireState state)
    {
        if (_fires.TryGetValue(color, out FireController fire))
        {
            fire.ApplyState(state);
        }
    }

    private void HandleFollowTargetChanged(TypeManager.FireColor color)
    {
        _maigoController.ResetDestination();
    }
    #endregion
}