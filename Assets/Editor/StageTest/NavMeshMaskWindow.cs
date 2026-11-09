using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// 再生中に、プロファイルごとに、NavMeshの表示のオンオフをするウィンドウ
public class NavMeshMaskWindow : EditorWindow
{
    private struct Run
    {
        public Vector3 Min;
        public Vector3 Max;
    }

    private sealed class Layer
    {
        public MovementProfile Profile;
        public int AgentTypeId;
        public bool Visible = true;
        public Color Color;
        public readonly List<Run> Runs = new List<Run>();
    }

    private const float HeightStep = 0.02f; // 重なった時に、ちらつかないよう、層ごとに高さをずらす

    private readonly List<Layer> _layers = new List<Layer>();
    private StageNavMeshSet _set;
    private int _builtCount;
    private GUIStyle _richLabel;

    [MenuItem("Tools/Stage Tools/NavMesh Mask")]
    private static void Open() => GetWindow<NavMeshMaskWindow>("NavMesh Mask");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Refresh();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    // ステージが読み込まれた時や、NavMeshが増えた時に、自動で作り直す
    private void OnInspectorUpdate()
    {
        StageNavMeshSet current = Application.isPlaying ? StageNavMeshProvider.Current : null;
        int count = current == null ? 0 : current.BuiltCount;
        if (current != _set || count != _builtCount)
        {
            Refresh();
            Repaint();
        }
    }

    private void Refresh()
    {
        // 表示のオンオフは、作り直しても引き継ぐ
        Dictionary<MovementProfile, bool> visibleBefore = new Dictionary<MovementProfile, bool>();
        foreach (Layer old in _layers) visibleBefore[old.Profile] = old.Visible;
        _layers.Clear();

        _set = Application.isPlaying ? StageNavMeshProvider.Current : null;
        _builtCount = 0;

        if (_set != null)
        {
            int index = 0;
            foreach (KeyValuePair<MovementProfile, int> pair in _set.GetBuiltAgentTypes())
            {
                Layer layer = new Layer
                {
                    Profile = pair.Key,
                    AgentTypeId = pair.Value,
                    Color = Color.HSVToRGB((index * 0.17f) % 1f, 0.8f, 1f),
                };
                if (visibleBefore.TryGetValue(pair.Key, out bool visible)) layer.Visible = visible;

                CollectRuns(_set.Grid, layer, index);
                _layers.Add(layer);
                index++;
            }
            _builtCount = _set.BuiltCount;
        }

        SceneView.RepaintAll();
    }

    // マスの中心を、そのエージェントの種類のNavMeshに当てて、横に続くマスをまとめる
    private static void CollectRuns(WalkableGrid grid, Layer layer, int index)
    {
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = layer.AgentTypeId,
            areaMask = NavMesh.AllAreas,
        };
        float maxDistance = grid.CellSize * 0.25f;
        float y = grid.Origin.y + 0.05f + index * HeightStep;

        for (int z = 0; z < grid.Depth; z++)
        {
            int runStart = -1;
            for (int x = 0; x <= grid.Width; x++)
            {
                bool onMesh = x < grid.Width &&
                              NavMesh.SamplePosition(grid.CellCenter(x, z), out NavMeshHit _, maxDistance, filter);

                if (onMesh && runStart < 0)
                {
                    runStart = x;
                }
                else if (!onMesh && runStart >= 0)
                {
                    layer.Runs.Add(new Run
                    {
                        Min = new Vector3(grid.Origin.x + runStart * grid.CellSize, y, grid.Origin.z + z * grid.CellSize),
                        Max = new Vector3(grid.Origin.x + x * grid.CellSize, y, grid.Origin.z + (z + 1) * grid.CellSize),
                    });
                    runStart = -1;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (_richLabel == null) _richLabel = new GUIStyle(EditorStyles.label) { richText = true };

        if (!Application.isPlaying || StageNavMeshProvider.Current == null)
        {
            EditorGUILayout.HelpBox("再生中に、StageNavMeshProvider.Loadが呼ばれた後で、使えます", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("更新")) Refresh();
            if (GUILayout.Button("すべて表示")) SetAll(true);
            if (GUILayout.Button("すべて非表示")) SetAll(false);
        }

        foreach (Layer layer in _layers)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                bool visible = EditorGUILayout.Toggle(layer.Visible, GUILayout.Width(18));
                string hex = ColorUtility.ToHtmlStringRGB(layer.Color);
                EditorGUILayout.LabelField(
                    $"<color=#{hex}>■</color> {layer.Profile}  (AgentType {layer.AgentTypeId})", _richLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.Visible = visible;
                    SceneView.RepaintAll();
                }
            }
        }
    }

    private void SetAll(bool visible)
    {
        foreach (Layer layer in _layers) layer.Visible = visible;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView view)
    {
        if (Event.current.type != EventType.Repaint) return;

        foreach (Layer layer in _layers)
        {
            if (!layer.Visible) continue;

            Color fill = new Color(layer.Color.r, layer.Color.g, layer.Color.b, 0.35f);
            foreach (Run run in layer.Runs)
            {
                Handles.DrawSolidRectangleWithOutline(new[]
                {
                    new Vector3(run.Min.x, run.Min.y, run.Min.z),
                    new Vector3(run.Min.x, run.Min.y, run.Max.z),
                    new Vector3(run.Max.x, run.Min.y, run.Max.z),
                    new Vector3(run.Max.x, run.Min.y, run.Min.z),
                }, fill, Color.clear);
            }
        }
    }
}