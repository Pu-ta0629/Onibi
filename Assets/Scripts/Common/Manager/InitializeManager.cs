using UnityEngine;

/// <summary>
/// 各Managerの生成・初期化の呼び出しと、参照の配布だけを担う
/// 参照はInspectorで受け取り、Initializeの引数として各スクリプトへ渡す（Instanceは使わない）
/// 各Managerが持つ設定（火の情報など）は、それぞれのManager側のInspectorで持つ
/// </summary>
public class InitializeManager : MonoBehaviour
{
    [Header("SceneReferences")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private LostChildController _lostChildController;
    [SerializeField] private FireManager _fireManager;

    private TypeManager _typeManager;

    public TypeManager typeManager => _typeManager;

    #region UnityLifeCycle
    private void Awake()
    {
        if (_inputManager == null || _lostChildController == null || _fireManager == null)
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

        //入力→迷子→火の順で初期化する（FireManagerは他の参照を使うため最後）
        _inputManager.Initialize(mainCamera);
        _lostChildController.Initialize();
        _fireManager.Initialize(_typeManager, _inputManager, _lostChildController, mainCamera);
    }
    #endregion
}