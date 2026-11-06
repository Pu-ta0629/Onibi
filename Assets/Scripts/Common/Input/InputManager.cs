using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マウス入力の窓口。クリック系の入力をイベントで通知し、マウス位置の地面座標（GroundPointer経由）を返す
/// 右クリックはDown（押した瞬間）・Hold（押している間、毎フレーム）・Up（離した瞬間）を通知する
/// </summary>
public class InputManager : MonoBehaviour
{
    public event Action OnLeftClick;
    public event Action OnMiddleClick;
    public event Action OnRightClickDown;
    public event Action OnRightClickHold;
    public event Action OnRightClickUp;

    [Header("InputActions")]
    [SerializeField] private InputActionReference _leftClickAction;
    [SerializeField] private InputActionReference _middleClickAction;
    [SerializeField] private InputActionReference _rightClickAction;
    [SerializeField] private InputActionReference _pointAction;

    [Header("GroundPoint")]
    [SerializeField, Tooltip("マウス位置を求める地面（XZ平面）の高さ。NavMeshの床の高さに合わせる")] private float _groundY;

    private Camera _mainCamera;
    private bool _isInitialized;
    private bool _isRightClickHeld;

    public bool isRightClickHeld => _isRightClickHeld;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ
    /// </summary>
    public void Initialize(Camera mainCamera)
    {
        Cache(mainCamera);
        NullCheck();

        _leftClickAction.action.performed += HandleLeftClick;
        _middleClickAction.action.performed += HandleMiddleClick;
        _rightClickAction.action.started += HandleRightClickDown;
        _rightClickAction.action.canceled += HandleRightClickUp;
        _leftClickAction.action.Enable();
        _middleClickAction.action.Enable();
        _rightClickAction.action.Enable();
        _pointAction.action.Enable();
        _isInitialized = true;
    }
    private void Cache(Camera mainCamera)
    {
        _mainCamera = mainCamera != null ? mainCamera : Camera.main;
    }
    private void NullCheck()
    {
        if (_leftClickAction == null || _middleClickAction == null || _rightClickAction == null || _pointAction == null)
        {
            Debug.LogError("[InputManager] Initialize : InputActionReference is Null!!! ");
            return;
        }

        if (_mainCamera == null)
        {
            Debug.LogError("[InputManager] Initialize : MainCamera is Null!!! ");
            return;
        }
    }
    #endregion

    #region Public
    /// <summary>
    /// マウス位置から出したRayと、高さ_groundYの地面（XZ平面）との交点を返す。交わらなければfalse
    /// コライダーや物理演算は使わない（GroundPointerが数学的に計算する）
    /// </summary>
    public bool TryGetGroundPoint(out Vector3 point)
    {
        point = default;
        if (!_isInitialized)
        {
            return false;
        }

        return GroundPointer.TryGetGroundPoint(_mainCamera, GetScreenPosition(), _groundY, out point);
    }

    /// <summary>
    /// マウスのスクリーン座標を返す（ラジアルメニューの方向判定用）
    /// </summary>
    public Vector2 GetScreenPosition()
    {
        return _isInitialized ? _pointAction.action.ReadValue<Vector2>() : Vector2.zero;
    }
    #endregion

    #region UnityLifeCycle
    private void Update()
    {
        if (_isRightClickHeld)
        {
            OnRightClickHold?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (!_isInitialized)
        {
            return;
        }

        _leftClickAction.action.performed -= HandleLeftClick;
        _middleClickAction.action.performed -= HandleMiddleClick;
        _rightClickAction.action.started -= HandleRightClickDown;
        _rightClickAction.action.canceled -= HandleRightClickUp;
    }
    #endregion

    #region Handler
    private void HandleLeftClick(InputAction.CallbackContext context)
    {
        //UI/ClickなどのPassThroughのアクションは、離したときにもperformedが呼ばれる。押した瞬間だけ通知する
        if (!context.ReadValueAsButton())
        {
            return;
        }

        OnLeftClick?.Invoke();
    }

    private void HandleMiddleClick(InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton())
        {
            return;
        }

        OnMiddleClick?.Invoke();
    }

    private void HandleRightClickDown(InputAction.CallbackContext context)
    {
        _isRightClickHeld = true;
        OnRightClickDown?.Invoke();
    }

    private void HandleRightClickUp(InputAction.CallbackContext context)
    {
        _isRightClickHeld = false;
        OnRightClickUp?.Invoke();
    }
    #endregion
}