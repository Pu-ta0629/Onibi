using UnityEngine;


/// Stageの生成・進行を管理するクラス
/// ここを中継して、進行は生成からデータを受け取る
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("Stage")]
    [Tooltip("生成するStage情報格納変数 各SpawnerでStageContext軽油で渡す")]
    [SerializeField] private StageData _stage;


    [Header("Generator")]
    [Tooltip("地形の生成実行を担当")]
    [SerializeField] private FieldSpawnManager _fieldSpawnManager;

    [Tooltip("Entityの生成実行を担当")]
    [SerializeField] private EntitySpawnManager _entitySpawnManager;

    // Stageの生成を実行する Initializeで初期化
    private StageGenerator _stageGenerator;

    private StageController _stageController;
    private LostChildController _lostChild;

    private StageContext _context;
    
    public StageContext Context => _context;

    #region Initialize

    public void Initialize(HitWorld world,LostChildController lostChild,Camera mainCamera)
    {
        if(world == null || lostChild == null || mainCamera == null || _fieldSpawnManager == null || _entitySpawnManager == null)
        {
            Debug.LogError("[StageGenerator] Initialize : Reference is Null!!!");
            return;
        }

#if UNITY_EDITOR
        // Stage未設定でも、各Spawnerの初期化は続ける
        if(_stage == null)
        {
            Debug.LogWarning("[StageGenerator] Initialize : Stage is not set. Scene field is used");
        }
#else
 // Stage未設定の場合
        if(_stage == null)
        {
            Debug.LogWarning("[StageGenerator] Initialize : Stage is not set. Scene field is used");
            return;
        }
#endif
        _context = new StageContext(_stage,world,lostChild, mainCamera,transform);

        _lostChild = lostChild;

        _stageGenerator = new StageGenerator(world,mainCamera,_context,_fieldSpawnManager,_entitySpawnManager);

        _stageGenerator.Generate();
        CreateController();
    }
    #endregion

    #region UnityLifeCycle

    private void Update()
    {
        _stageController?.Tick();
    }

    private void OnDestroy()
    {
        if(_stageController != null)
        {
            _stageController.StageCleared -= HandleStageCleared;
        }
    }
    #endregion

    #region Private
    // 選ばれたGoalをStageConrollerへ渡して、判定を始める
    private void CreateController()
    {
        GoalPlacement goal = _fieldSpawnManager.SelectedGoal;
        if(goal == null)
        {
            Debug.LogWarning("[StageManager] CreateController : Goal is selected. Stage clear is not judged");
            return;
        }

        _stageController = new StageController(_lostChild, goal);
        _stageController.StageCleared += HandleStageCleared;
        _stageController.Begin();
    }

    private void HandleStageCleared()
    {
        // _lostChild.Freeze();
       // _entitySpawnManager.Stop();
    }
    #endregion

}
