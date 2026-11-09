using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージの地形（NavMesh・地面）とゴールを、StageDataから生成するクラス
/// StageDataはStageGeneratorからStageContext経由で受け取る
/// </summary>
public class FieldSpawner : MonoBehaviour, IStageSpawner
{
    [Header("Goal")]
    [Tooltip("ゴールのPrefab（直径1の見た目を想定。StageDataのGoalごとに生成する）")]
    [SerializeField] private GoalObject _goalPrefab;

    private readonly List<GoalObject> _goals = new List<GoalObject>();

    public IReadOnlyList<GoalObject> goals => _goals;

    #region Initialize
    /// <summary>
    /// StageGeneratorから一度だけ呼ぶ
    /// </summary>
    public void Initialize(StageContext context)
    {
        if (context == null)
        {
            Debug.LogError("[FieldSpawner] Initialize : StageContext is Null!!! ");
            return;
        }

        StageData stage = context.Stage;
        if (stage == null)
        {
            Debug.Log("[FieldSpawner] Initialize : Stage is not set. Scene field is used");
            return;
        }

        //Loadは呼ぶたびに前のNavMeshを破棄するので、1回でまとめて作る
        //Fire・敵のプロファイルはStageDataから集まる。迷子はStageDataに入っていないので、追加で渡す
        StageNavMeshProvider.Load(stage, context.LostChildController.LostChild.MovementProfile);
        StageGroundProvider.Load(stage);

        SpawnGoals(stage, context.Root);
    }
    #endregion

    #region UnityLifeCycle
    private void OnDestroy()
    {
        ClearGoals();
    }
    #endregion

    #region Private
    //StageDataのGoalごとに、GoalObjectを生成する
    private void SpawnGoals(StageData stage, Transform parent)
    {
        ClearGoals();

        if (stage.Goals.Count == 0)
        {
            return;
        }

        if (_goalPrefab == null)
        {
            Debug.LogWarning("[FieldSpawner] SpawnGoals : GoalPrefab is Null!!");
            return;
        }

        for (int i = 0; i < stage.Goals.Count; i++)
        {
            GoalPlacement placement = stage.Goals[i];
            GoalObject goal = Instantiate(_goalPrefab, placement.Position, Quaternion.Euler(0f, placement.RotationY, 0f), parent);
            goal.name = $"Object_Goal_{i + 1}";
            goal.Setup(placement.Radius);
            _goals.Add(goal);
        }
    }

    private void ClearGoals()
    {
        for (int i = 0; i < _goals.Count; i++)
        {
            if (_goals[i] != null)
            {
                Destroy(_goals[i].gameObject);
            }
        }

        _goals.Clear();
    }
    #endregion
}