using UnityEngine;

/// <summary>
/// 各Managerの生成・初期化の呼び出しと、参照の配布を担当する
/// 参照はInspectorで受け取り、Initializeの引数として各スクリプトへ渡す（Instanceは使わない）
/// 各Managerの固有設定（火の情報など）は、それぞれのManagerがInspectorで持つ
/// </summary>
public class InitializeManager : MonoBehaviour
{
    [Header("SceneReferences")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private LostChildController _lostChildController;
    [SerializeField] private FireManager _fireManager;
    [SerializeField] private StageGenerator _stageGenerator;

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
        if (_inputManager == null || _lostChildController == null || _fireManager == null || _stageGenerator == null)
        {
            Debug.LogError("[InitializeManager] Awake : Reference is Null!!! ");
            return;
        }

        //Inspectorで未設定ならCamera.mainを使う。以降はここで決めたカメラを配る
        Camera mainCamera = _mainCamera != null ? _mainCamera : Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[InitializeManager] Awake : MainCamera is Null!!! ");
            return;
        }

        //純粋なC#クラスを生成して初期化する
        _typeManager = new TypeManager();
        _typeManager.Initialize();
        _hitWorld = new HitWorld();
        _kodamaStock = new KodamaStock(_initialKodama);

        //入力→迷子→火→ステージ（敵など）の順で初期化する（FireManagerは他の参照を使うため、ステージより前）
        _inputManager.Initialize(mainCamera);
        _stageGenerator.Initialize(_hitWorld, _lostChildController, mainCamera);
        _lostChildController.Initialize(_hitWorld);
        _fireManager.Initialize(_typeManager, _inputManager, _lostChildController, mainCamera, _hitWorld, _kodamaStock);
    }
    #endregion
}
