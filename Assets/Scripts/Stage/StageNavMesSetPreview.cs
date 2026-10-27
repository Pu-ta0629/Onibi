using UnityEditor.ShaderKeywordFilter;
using UnityEngine;
using UnityEngine.AI;

public class StageNavMeshSetPreview : MonoBehaviour
{
    [SerializeField] StageData stage;
    [SerializeField] MovementProfile profile;

    StageNavMeshSet _set;
    NavMeshTriangulation _triangulation;

    void Start()
    {
        if (stage == null) return;

        _set = new StageNavMeshSet(stage.WalkableGrid);

        if (_set.GetAgentTypeId(profile) >= 0)
            _triangulation = NavMesh.CalculateTriangulation();
    }

    private void OnDestroy() => _set?.Dispose();
#if UNITY_EDITOR
    void OnDrawGizmos()
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
