using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// カメラの追従先を管理する(MonoBehaviour)。迷子と、ラジアルで選択中の火をTargetGroupにまとめ、
/// CinemaChineCameraはそのTargetGroupをFollowする
/// デッドゾーン・ハードリミットはCinemaChineCameraのPosition Composer(Inspecterで設定いする)
/// 火が選択されていない間は、迷子だけをおう。選択が切り替わったら、追従先の火も入れ替える
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [Tooltip("追従先をまとめる targetGroup（別のGameObjectにつける）")]
    [SerializeField] private CinemachineTargetGroup _targetGroup;

    [Header("Weight")]
    [Tooltip("迷子の重み。大きいほど、画面の中心に迷子がよる")]
    [SerializeField, Range(0.1f, 10f)] private float _lostChildWeight = 1f;
    [Tooltip("選択中の火の重み")]
    [SerializeField, Range(0.1f, 10f)] private float _fireWeight = 1f;
    [Tooltip("TargetGroup上の半径（Group　Framingを使うときの余白）")]
    [SerializeField, Range(0.1f, 10f)] private float _memberRadius = 1f;

    private TypeManager _typeManager;
    private FireManager _fireManager;
    private LostChildController _lostChild;
    private Transform _currentFire;
    private bool _isInitialized;

    #region Initialize
    /// <summary>
    /// InitializeManagerから一度だけ呼ぶ。FireManager.Initializeの後に呼ぶ（選択中の火が決まっているため）
    /// </summary>
    public void Initialize(TypeManager typeManager, FireManager fireManager, LostChildController lostChild)
    {
        if (typeManager == null || fireManager == null || lostChild == null || _cinemachineCamera == null || _targetGroup == null)
        {
            Debug.LogError("[CameraManager] Initialize : Reference is Null!!! ");
            return;
        }

        _typeManager = typeManager;
        _fireManager = fireManager;
        _lostChild = lostChild;

        _targetGroup.Targets.Clear();
        _targetGroup.AddMember(_lostChild.transform, _lostChildWeight, _memberRadius);
        _cinemachineCamera.Follow = _targetGroup.transform;

        _typeManager.OnFireStateChanged += HandleFireStateChanged;
        _isInitialized = true;

        RefreshFireTarget();
    }
    #endregion

    #region UnityLifeCycle
    private void OnDestroy()
    {
        if(_isInitialized)
        {
            _typeManager.OnFireStateChanged -= HandleFireStateChanged;
        }
    }
    #endregion

    #region Private
    private void RefreshFireTarget()
    {
        Transform next = null;
        FireColor color = _typeManager.SelectedColor;
        if(color != FireColor.None && _fireManager.TryGetFire(color, out FireController fire))
        {
            next = fire.transform;
        }
        if (next == _currentFire) return;

        if(_currentFire != null)
        {
            _targetGroup.RemoveMember(_currentFire);
        }
        _currentFire = next;
        
        if(_currentFire != null)
        {
            _targetGroup.AddMember(_currentFire, _fireWeight, _memberRadius);
        }
    }
    #endregion
    #region Handler
    private void HandleFireStateChanged(FireColor color, FireState state)
    {
        RefreshFireTarget();
    }
    #endregion
}
