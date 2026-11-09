using UnityEngine;
using UnityEngine.AI;
// 修正: UnityEditor.ShaderKeywordFilterのusingを削除(ビルド時にエラーになるため)
// ファイル名も、クラス名(StageNavMeshSetPreview)に合わせた。ファイル名が違うと、コンポーネントとして追加できない

public class StageNavMeshSetPreview : MonoBehaviour
{
    [SerializeField] private StageData stage;
    [SerializeField] private MovementProfile profile;

    private StageNavMeshSet _set;
    private NavMeshTriangulation _triangulation;

    private void Start()
    {
        if (stage == null) return;

        _set = new StageNavMeshSet(stage.WalkableGrid);

        // 注意: CalculateTriangulationは、存在するすべてのNavMeshをまとめて返す。
        // このプレビューは1つのプロファイルだけを作るので、他でNavMeshを作っていなければ、そのプロファイルだけが描かれる
        if (_set.TryGetAgentTypeId(profile,out int _))
            _triangulation = NavMesh.CalculateTriangulation();
    }

    private void OnDestroy() => _set?.Dispose();

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_set == null || _triangulation.indices == null) return;

        Gizmos.color = Color.cyan;
        var v = _triangulation.vertices;
        var idx = _triangulation.indices;
        for (var i = 0; i + 2 < idx.Length; i += 3)
        {
            Gizmos.DrawLine(v[idx[i]], v[idx[i + 1]]);
            Gizmos.DrawLine(v[idx[i + 1]], v[idx[i + 2]]);
            Gizmos.DrawLine(v[idx[i + 2]], v[idx[i]]);
        }
    }
#endif
}