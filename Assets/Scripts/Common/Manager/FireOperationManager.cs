//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 火の選択・固定・マウス追従と、迷子への追従先の受け渡しをまとめる管理クラス
///// Stateの取得・変更はすべてTypeManager経由で行う
///// </summary>
//public class FireOperationManager : MonoBehaviour
//{
//    private readonly Dictionary<TypeManager.FireColor, FireController> _fires = new Dictionary<TypeManager.FireColor, FireController>();
//    private TypeManager _typeManager;
//    private InputManager _inputManager;
//    private LostChildController _maigoController;
//    private FireInventory _fireInventory;
//    private bool _isInitialized;

//    #region Initialize
//    /// <summary>
//    /// InitializeManagerから一度だけ呼ぶ
//    /// </summary>
//    public void Initialize(TypeManager typeManager, InputManager inputManager, LostChildController maigoController, FireInventory fireInventory)
//    {
//        if (typeManager == null || inputManager == null || maigoController == null || fireInventory == null)
//        {
//            Debug.LogError("[FireOperationManager] Initialize : Reference is Null!!! ");
//            return;
//        }

//        _typeManager = typeManager;
//        _inputManager = inputManager;
//        _maigoController = maigoController;
//        _fireInventory = fireInventory;

//        _typeManager.OnFireStateChanged += HandleFireStateChanged;
//        _typeManager.OnFollowTargetChanged += HandleFollowTargetChanged;
//        _inputManager.OnLeftClick += HandleLeftClick;
//        _inputManager.OnMiddleClick += HandleMiddleClick;
//        _isInitialized = true;
//    }

//    /// <summary>
//    /// 初期装備の火を登録する。所持している色だけ登録される
//    /// </summary>
//    public void RegisterInitialFires(IReadOnlyList<FireController> fires)
//    {
//        for (int i = 0; i < fires.Count; i++)
//        {
//            if (fires[i] != null && _fireInventory.Has(fires[i].color))
//            {
//                RegisterFire(fires[i]);
//            }
//        }
//    }
//    #endregion

//    #region Public
//    /// <summary>
//    /// 火を新しく獲得したときに呼ぶ。所持リストへ追加し、ラジアルに項目が増える
//    /// </summary>
//    public bool AcquireFire(FireController fire)
//    {
//        if (!_isInitialized || fire == null)
//        {
//            Debug.LogError("[FireOperationManager] AcquireFire : Not initialized or fire is Null!!! ");
//            return false;
//        }

//        if (!_fireInventory.TryAdd(fire.color))
//        {
//            Debug.LogWarning($"[FireOperationManager] AcquireFire : {fire.color} is already owned!!");
//            return false;
//        }

//        RegisterFire(fire);
//        return true;
//    }

//    /// <summary>
//    /// ラジアルメニューで火が選ばれたときに呼ぶ（選択中の火をもう一度選ぶと選択解除）
//    /// </summary>
//    public void ToggleSelectFire(TypeManager.FireColor color)
//    {
//        if (!_fires.ContainsKey(color))
//        {
//            Debug.LogWarning($"[FireOperationManager] ToggleSelectFire : {color} is not registered!!");
//            return;
//        }

//        _typeManager.ToggleSelectFire(color);
//    }

//    /// <summary>
//    /// ラジアルメニューを開いた・閉じたときに呼ぶ（開いている間、選択中の火は停止する）
//    /// </summary>
//    public void SetRadialOpen(bool isOpen)
//    {
//        _typeManager.SetRadialOpen(isOpen);
//    }
//    #endregion

//    #region UnityLifeCycle
//    private void Update()
//    {
//        if (!_isInitialized)
//        {
//            return;
//        }

//        MoveSelectedFire();
//        UpdateMaigoTarget();
//    }

//    private void OnDestroy()
//    {
//        if (!_isInitialized)
//        {
//            return;
//        }

//        _typeManager.OnFireStateChanged -= HandleFireStateChanged;
//        _typeManager.OnFollowTargetChanged -= HandleFollowTargetChanged;
//        _inputManager.OnLeftClick -= HandleLeftClick;
//        _inputManager.OnMiddleClick -= HandleMiddleClick;
//    }
//    #endregion

//    #region Private
//    private void RegisterFire(FireController fire)
//    {
//        if (fire.color == TypeManager.FireColor.None)
//        {
//            Debug.LogError($"[FireOperationManager] RegisterFire : FireColor is None!!! name = {fire.name}");
//            return;
//        }

//        if (_fires.ContainsKey(fire.color))
//        {
//            Debug.LogWarning($"[FireOperationManager] RegisterFire : {fire.color} is already registered!!");
//            return;
//        }

//        fire.Initialize(_main);
//        _fires.Add(fire.color, fire);
//        fire.ApplyState(_typeManager.GetFireState(fire.color));
//    }
//    #endregion

//    #region Update
//    //選択中かつ固定されていない火を、マウス位置の地面へ移動させる
//    private void MoveSelectedFire()
//    {
//        TypeManager.FireColor color = _typeManager.SelectedColor;
//        if (_typeManager.IsRadialOpen || color == TypeManager.FireColor.None)
//        {
//            return;
//        }

//        if (_typeManager.GetFireState(color).IsFixed)
//        {
//            return;
//        }

//        if (_fires.TryGetValue(color, out FireController fire) && _inputManager.TryGetGroundPoint(out Vector3 point))
//        {
//            fire.MoveTo(point);
//        }
//    }

//    //迷子へ追従先の座標を渡す
//    private void UpdateMaigoTarget()
//    {
//        TypeManager.FireColor color = _typeManager.FollowTarget;
//        if (color == TypeManager.FireColor.None)
//        {
//            return;
//        }

//        if (_fires.TryGetValue(color, out FireController fire))
//        {
//            _maigoController.Tick(fire.position);
//        }
//    }
//    #endregion

//    #region Handler
//    private void HandleLeftClick()
//    {
//        //ラジアルを開いている間のクリックは固定の切り替えに使わない
//        if (_typeManager.IsRadialOpen)
//        {
//            return;
//        }

//        _typeManager.ToggleFixedSelectedFire();
//    }

//    private void HandleMiddleClick()
//    {
//        _typeManager.ToggleFollowMode();
//    }

//    private void HandleFireStateChanged(TypeManager.FireColor color, TypeManager.FireState state)
//    {
//        if (_fires.TryGetValue(color, out FireController fire))
//        {
//            fire.ApplyState(state);
//        }
//    }

//    private void HandleFollowTargetChanged(TypeManager.FireColor color)
//    {
//        _maigoController.ResetDestination();
//    }
//    #endregion
//}