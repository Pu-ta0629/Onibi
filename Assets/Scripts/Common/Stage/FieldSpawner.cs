using UnityEngine;

/// <summary>
/// ステージの地形（フィールド）を生成するクラス
/// StageEditorの統合がまだなので、いまは何も生成せず、Scene上の地形をそのまま使う
/// 統合後は、StageData.WalkableGridからNavMesh（StageNavMeshSet）などを作る処理をここへ足す
/// </summary>
public class FieldSpawner : MonoBehaviour, IStageSpawner
{
    [Header("Field")]
    [Tooltip("生成するステージのデータ。StageEditor統合までは未設定でよい（Scene上の地形をそのまま使う）")]
    [SerializeField] private StageData _stage;

    public StageData stage => _stage;

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

        if (_stage == null)
        {
            Debug.Log("[FieldSpawner] Initialize : Stage is not set. Scene field is used");
            return;
        }

        //TODO StageEditor統合後：_stage.WalkableGridからNavMeshなどのフィールドを生成する
        Debug.LogWarning($"[FieldSpawner] Initialize : Field generation is not implemented yet!! stage = {_stage.name}");
    }
    #endregion
}
