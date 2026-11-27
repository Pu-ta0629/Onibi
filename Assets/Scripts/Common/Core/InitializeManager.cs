using UnityEngine;

/// <summary>
/// 各Managerの生成・初期化の呼び出しと、参照の配布を担当する
/// 参照はInspectorで受け取り、Initializeの引数として各スクリプトへ渡す（Instanceは使わない）
/// 各Managerの固有設定（火の消費量など）は、それぞれのManagerのInspectorで持つ
/// </summary>
public class InitializeManager : MonoBehaviour
{
    [Header("SceneReferences")]
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private LostChildController _lostChildController;
    [SerializeField] private StageManager _stageManager;
    [SerializeField] private FireManager _fireManager;
    [SerializeField] private CameraManager _cameraManager;


    [Header("Battle")]
    [Tooltip("ステージ開始時の木霊の量。火が突進するたびに消費する")]
    [SerializeField, Range(0, 999)] private int _initialKodama = 30;

    private TypeManager _typeManager;
    private HitWorld _hitWorld;
    private KodamaStock _kodamaStock;

    public TypeManager typeManager => _typeManager;

    #region UnityLifeCycle
    private void Awake()
    {
        if (_inputManager == null || _lostChildController == null || _fireManager == null || _stageManager == null)
        {
            Debug.LogError("[InitializeManager] Awake : Reference is Null!!! ");
            return;
        }

        //Inspectorで未設定ならCamera.mainを使う。以降はここで決めたカメラを想定
        Camera mainCamera = _cameraManager.MainCamera != null ? _cameraManager.MainCamera : Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[InitializeManager] Awake : MainCamera is Null!!! ");
            return;
        }

        //ここでC#クラスを生成して初期化する
        _typeManager = new TypeManager();
        _typeManager.Initialize();
        _hitWorld = new HitWorld();
        _kodamaStock = new KodamaStock(_initialKodama);

        //入力→迷子→ステージ（地形・迷子の配置・敵）→火の順で初期化する
        //迷子は、ステージ内でSpawnAtで置かれるので、必ずステージより前にInitializeする
        //（後ろだとSpawnAtが先に位置を決めてしまい、迷子がStartに置かれず、敵の追従先がNavMeshの外になる）
        _inputManager.Initialize(mainCamera);
        _lostChildController.Initialize(_hitWorld, mainCamera);
        _stageManager.Initialize(_hitWorld, _lostChildController, mainCamera);
        _fireManager.Initialize(_typeManager, _inputManager, _lostChildController, mainCamera, _hitWorld, _kodamaStock);
        _cameraManager.Initialize(_typeManager, _fireManager, _lostChildController);
    }
    #endregion
}