using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージの地形（NavMesh・地面）とゴールを、StageDataから生成するクラス
/// StageDataはStageGeneratorからStageContext経由で受け取る
/// </summary>
public class FieldSpawnManager : MonoBehaviour, IStageSpawner
{

    [Header("Goal Light")]
    [Tooltip("Goalの光の色 ")]
    [SerializeField] private Color _goalLightColor = new Color(1f, 0.85f, 0.5f);

    [Tooltip("Goalの光の強さ")]
    [SerializeField, Min(0f)] private float _goalLightIntensity = 2f;

    [Tooltip("光の範囲の倍率")]
    [SerializeField, Min(0.1f)] private float _goalLightRangeScale = 2f;

    [Tooltip("Goalの位置から、光を浮かせる高さ")]
    [SerializeField, Min(0f)] private float _goalLightHeight = 2f;

    private GameObject _goalObject;

    /// <summary>Initializeで選ばれたGoal</summary>
    public GoalPlacement SelectedGoal { get; private set; }

    #region Initialize
    ///<summary>
    ///StageGeneratorから一度だけ呼ぶ
    /// </summary>
    public void Initialize(StageContext context)
    {
        if(context == null)
        {
            Debug.LogError("[FieldSpawnManager] Initialize : StageContext is Null!!!");
        }
        StageData stage = context.Stage;
        if(stage == null)
        {
            Debug.LogError("[FieldSpawnManager] Initialize : stage is not set. Scene field is used");
            return;
        }

        //Loadは呼ぶたびに前のNavMeshを破棄するので、1回でまとめて作る
        //Fire・敵のプロファイルはStageDataから集まる。迷子はStageDataに入っていないので、追加で渡す
        StageNavMeshProvider.Load(stage, context.LostChildController.LostChild.MovementProfile);
        StageGroundProvider.Load(stage);

        SpawnGoal(stage, context.Root);
    }
    #endregion

    #region UnityLifeCycle
    private void OnDestroy()
    {
        ClearGoal();
    }
    #endregion

    #region Private

    private void SpawnGoal(StageData stage,Transform parent)
    {
        ClearGoal();

        if(stage.Goals.Count == 0)
        {
            Debug.LogWarning("[FieldSpawnManager] SpawnGoal : StageData has no Goals!!");
            return;
        }

        int index = Random.Range(0, stage.Goals.Count);
        GoalPlacement placement = stage.Goals[index];

        //判定に使うので、見た目より先に、選んだ結果をもっておく
        SelectedGoal = placement;

        // 見た目は、光だけ。GameObjectを一から組み立てる
        _goalObject = new GameObject($"Object_Goal_{index + 1}");
        _goalObject.transform.SetParent(parent);
        _goalObject.transform.position = placement.Position + Vector3.up * _goalLightHeight;

        Light goalLight = _goalObject.AddComponent<Light>();
        goalLight.type = LightType.Point;
        goalLight.color = _goalLightColor;
        goalLight.intensity = _goalLightIntensity;
        goalLight.range = placement.Radius * _goalLightRangeScale;
    }

    private void ClearGoal()
    {
        if(_goalObject != null)
        {
            Destroy(_goalObject);
        }

        _goalObject = null;
        SelectedGoal = null;
    }
    #endregion
}