using UnityEngine;

/// <summary>
/// �eManager�̐����E�������̌Ăяo���ƁA�Q�Ƃ̔z�z��S������
/// �Q�Ƃ�Inspector�Ŏ󂯎��AInitialize�̈����Ƃ��Ċe�X�N���v�g�֓n���iInstance�͎g��Ȃ��j
/// �eManager�̌ŗL�ݒ�i�΂̏��Ȃǁj�́A���ꂼ���Manager��Inspector�Ŏ���
/// </summary>
public class InitializeManager : MonoBehaviour
{
    [Header("SceneReferences")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private LostChildController _lostChildController;
    [SerializeField] private FireManager _fireManager;
    [SerializeField] private StageManager _stageManager;
    

    [Header("Battle")]
    [Tooltip("�X�e�[�W�J�n���̖ؗ�̗ʁB�΂��ːi���邽�тɏ����")]
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

        //Inspector�Ŗ��ݒ�Ȃ�Camera.main���g���B�ȍ~�͂����Ō��߂��J������z��
        Camera mainCamera = _mainCamera != null ? _mainCamera : Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[InitializeManager] Awake : MainCamera is Null!!! ");
            return;
        }

        //������C#�N���X�𐶐����ď���������
        _typeManager = new TypeManager();
        _typeManager.Initialize();
        _hitWorld = new HitWorld();
        _kodamaStock = new KodamaStock(_initialKodama);

        //���́����q���X�e�[�W�i�n�`�E���q�̔z�u�E�G�j���΂̏��ŏ���������
        //���q�́A�X�e�[�W����SpawnAt�Œu�����̂ŁA�K���X�e�[�W���O��Initialize����
        //�i��낾��SpawnAt�����������߂�A���q��Start�ɒu���ꂸ�A�G�̒Ǐ]�悪NavMesh�̊O�ɂȂ�j
        _inputManager.Initialize(mainCamera);
        _lostChildController.Initialize(_hitWorld);
        _stageManager.Initialize(_hitWorld, _lostChildController, mainCamera);
        _fireManager.Initialize(_typeManager, _inputManager, _lostChildController, mainCamera, _hitWorld, _kodamaStock);
    }
    #endregion
}