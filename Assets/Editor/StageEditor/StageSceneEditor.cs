using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class StageSceneEditor
{
    private const float MarkerSize = 0.4f;

    private static StageData _target;
    private static string _selectedPath;

    private static readonly Color KodamaColor = new Color(0.2f, 1f, 1f);
    private static readonly Color EnemySpawnColor = new Color(1f, 0.2f, 0.9f);

    private static readonly Color SpecialKodamaColor = new Color(1f, 0.8f, 0.2f);

    public static bool SnapToGrid { get; set; } = true;

    [System.Flags]
    public enum ObjectKind
    {
        None = 0,
        Start = 1,
        Goal = 2,
        Enemy = 4,
        Spot = 8,
        All = 15,
    }

    public static ObjectKind VisibleKinds { get; set; } = ObjectKind.All;

    private static bool IsLocked => StageGridEditor.PenEnabled;

    public static StageData Target
    {
        get => _target;
        set
        {
            if (_target == value) return;
            _target = value;
            _selectedPath = null;
            StageGroundPreview.Refresh();
            SceneView.RepaintAll();
        }
    }

    private static Vector3 SnapToCell(Vector3 position)
    {
        WalkableGrid grid = _target.WalkableGrid;
        int x = Mathf.Clamp(Mathf.FloorToInt((position.x - grid.Origin.x) / grid.CellSize), 0, grid.Width - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt((position.z - grid.Origin.z) / grid.CellSize), 0, grid.Depth - 1);
        Vector3 center = grid.CellCenter(x, z);
        return new Vector3(center.x, position.y, center.z);
    }

    static StageSceneEditor() => SceneView.duringSceneGui += OnSceneGUI;

    //新規は位置の基準位置(Sceneビューの注視点)
    private static Vector3 PivotPosition()
    {
        var view = SceneView.lastActiveSceneView;
        return view != null ? view.pivot : Vector3.zero;
    }

    //---- 描画と移動 -----

    private static void OnSceneGUI(SceneView view)
    {
        if (_target == null) return;

        SerializedObject so = new SerializedObject(_target);
        so.Update();

        //種類を渡す
        DrawElement(so.FindProperty(StageData.StartField), "Start", Color.cyan, ObjectKind.Start);
        DrawList(so, StageData.GoalsField, Color.green, "Goal", ObjectKind.Goal);
        DrawList(so, StageData.EnemiesField, Color.red, "Enemy", ObjectKind.Enemy);
        DrawList(so, StageData.SpawnSpotsField, Color.white, "Spot", ObjectKind.Spot); // 色は、DrawElementの中で種類ごとに決める

        so.ApplyModifiedProperties();

        //Paint中は、触れないことを画面に出す
        if (IsLocked && Event.current.type == EventType.Repaint)
        {
            Handles.BeginGUI();
            GUI.Label(new Rect(10f, view.position.height - 60f, 480f, 20f),
                "Pen中: Objectは触れません(PenをOffにすると触れます)", EditorStyles.boldLabel);   // 文言だけ変更
            Handles.EndGUI();
        }
    }


    private static void DrawList(SerializedObject so, string listField, Color color, string label, ObjectKind objectKind)
    {
        SerializedProperty list = so.FindProperty(listField);
        for (int i = 0; i < list.arraySize; i++)
            DrawElement(list.GetArrayElementAtIndex(i), $"{label}{i + 1}", color, objectKind);
    }

    private static void DrawElement(SerializedProperty element, string label, Color color, ObjectKind objectKind)
    {
        // 追加: 非表示の種類は、描かない(触れない)
        if ((VisibleKinds & objectKind) == 0) return;

        SerializedProperty posProp = element.FindPropertyRelative(PlacementData.PositionField);
        SerializedProperty rotProp = element.FindPropertyRelative(PlacementData.RotationYField);
        SerializedProperty radiusProp = element.FindPropertyRelative(RadiusPlacement.RadiusField);   // ゴールと湧きスポットだけが持つ
        SerializedProperty enemyProp = element.FindPropertyRelative(EnemyPlacement.EnemyField);
        SerializedProperty kindProp = element.FindPropertyRelative(SpawnSpotPlacement.KindField);

        Vector3 pos = posProp.vector3Value;
        Quaternion rot = Quaternion.Euler(0f, rotProp.floatValue, 0f);

        // 敵は、種類と大きさもラベルに出す
        if (enemyProp != null)
        {
            EnemyData enemy = enemyProp.objectReferenceValue as EnemyData;
            SerializedProperty sizeProp = element.FindPropertyRelative(EnemyPlacement.SizeField);
            SizeClass sizeClass = (SizeClass)sizeProp.enumValueIndex;
            label += enemy != null ? $"({enemy.DisplayName} / {sizeClass})" : $"(未設定 / {sizeClass})";
        }

        // 湧きスポットは、種類をラベルに出して、色も種類で変える
        if (kindProp != null)
        {
            SpawnKind kind = (SpawnKind)kindProp.enumValueIndex;
            label += $"({kind})";
            switch (kind)
            {
                case SpawnKind.Kodama: color = KodamaColor; break;
                case SpawnKind.SpecialKodama: color = SpecialKodamaColor; break;
                default: color = EnemySpawnColor; break;
            }
        }

        // 変更: Paint中は、選択の見た目にせず、薄く描く
        bool selected = !IsLocked && element.propertyPath == _selectedPath;
        Color drawColor = IsLocked ? new Color(color.r, color.g, color.b, 0.4f) : color;
        Handles.color = selected ? Color.white : drawColor;

        if (radiusProp != null)
        {
            Handles.DrawWireDisc(pos, Vector3.up, radiusProp.floatValue);

            // 湧きスポットは、範囲を半透明で塗る
            if (kindProp != null)
            {
                Handles.color = new Color(color.r, color.g, color.b, IsLocked ? 0.07f : 0.15f);
                Handles.DrawSolidDisc(pos, Vector3.up, radiusProp.floatValue);
                Handles.color = selected ? Color.white : drawColor;
            }
        }

        Handles.DrawLine(pos, pos + rot * Vector3.forward * (MarkerSize * 3f));
        Handles.Label(pos + Vector3.up * (MarkerSize * 1.5f), label);

        // 追加: Paint中は、選択も移動もできない(ボタンも、ハンドルも出さない)
        if (IsLocked) return;

        float size = HandleUtility.GetHandleSize(pos) * MarkerSize;
        if (Handles.Button(pos, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
            _selectedPath = element.propertyPath;

        if (!selected) return;

        // 選択中のものだけ、移動ハンドルを出す。敵と湧きスポットは、マスの中心に合わせる
        EditorGUI.BeginChangeCheck();
        Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            if ((enemyProp != null || kindProp != null) && SnapToGrid) newPos = SnapToCell(newPos);
            posProp.vector3Value = newPos;
        }

        // 半径のハンドル(自由な値)
        if (radiusProp != null)
        {
            EditorGUI.BeginChangeCheck();
            float newRadius = Handles.RadiusHandle(Quaternion.identity, pos, radiusProp.floatValue);
            if (EditorGUI.EndChangeCheck())
                radiusProp.floatValue = Mathf.Max(0.1f, newRadius);
        }
    }

    // ---- 追加と削除(StageEditorWindow のボタンから呼ぶ) ----

    public static void AddGoal() => Add(StageData.GoalsField, element =>
        element.FindPropertyRelative(GoalPlacement.RadiusField).floatValue = 1f);

    public static void AddSpawnSpot() => Add(StageData.SpawnSpotsField, element =>
    {
        element.FindPropertyRelative(SpawnSpotPlacement.KindField).enumValueIndex = (int)SpawnKind.Kodama;
        element.FindPropertyRelative(RadiusPlacement.RadiusField).floatValue = 2f;

        if (SnapToGrid)
        {
            SerializedProperty posProp = element.FindPropertyRelative(PlacementData.PositionField);
            posProp.vector3Value = SnapToCell(posProp.vector3Value);
        }
    });

    public static void AddEnemy() => Add(StageData.EnemiesField, element =>
        {
            element.FindPropertyRelative(EnemyPlacement.EnemyField).objectReferenceValue = null;
            element.FindPropertyRelative(EnemyPlacement.SizeField).enumValueIndex = (int)SizeClass.Medium;

            if(SnapToGrid)
            {
                SerializedProperty posProp = element.FindPropertyRelative(PlacementData.PositionField);
                posProp.vector3Value = SnapToCell(posProp.vector3Value);
            }
        });

    public static void SnapAll()
    {
        if (_target == null) return;

        SerializedObject so = new SerializedObject(_target);
        string[] listFields = { StageData.EnemiesField, StageData.SpawnSpotsField };
        foreach (string listField in listFields)
        {
            SerializedProperty list = so.FindProperty(listField);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty posProp = list.GetArrayElementAtIndex(i).FindPropertyRelative(PlacementData.PositionField);
                posProp.vector3Value = SnapToCell(posProp.vector3Value);
            }
        }
        so.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private static void Add(string listField,Action<SerializedProperty> init)
    {
        if (_target == null) return;

        var so = new SerializedObject(_target);
        var list = so.FindProperty(listField);
        list.arraySize++;
        var element = list.GetArrayElementAtIndex(list.arraySize - 1);

        element.FindPropertyRelative(PlacementData.PositionField).vector3Value = PivotPosition();
        element.FindPropertyRelative(PlacementData.RotationYField).floatValue = 0f;
        init(element);

        so.ApplyModifiedProperties();
        _selectedPath = element.propertyPath;
        SceneView.RepaintAll();
    }

    public static void SetStartHere()
    {
        if(_target == null) return;

        var so = new SerializedObject(_target);
        var start = so.FindProperty(StageData.StartField);
        start.FindPropertyRelative(PlacementData.PositionField).vector3Value = PivotPosition();
        so.ApplyModifiedProperties();

        _selectedPath = start.propertyPath;
        SceneView.RepaintAll();
    }

    public static void RemoveSelected()
    {
        if (IsLocked) return;

        if(_target == null || string.IsNullOrEmpty(_selectedPath)) return;

        var open = _selectedPath.IndexOf("[");
        if(open < 0) return;

        var close = _selectedPath.IndexOf("]");
        var listName = _selectedPath.Substring(0, _selectedPath.IndexOf('.'));
        var index = int.Parse(_selectedPath.Substring(open + 1,close - open -1));

        var so = new SerializedObject(_target);
        var list = so.FindProperty(listName);
        if (index < list.arraySize) list.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedProperties();

        _selectedPath = null;
        SceneView.RepaintAll();
    }

    public static void SetVisible(ObjectKind objectKind, bool visible)
    {
        VisibleKinds = visible ? (VisibleKinds | objectKind) : (VisibleKinds & ~objectKind);

        // 非表示にした種類の選択は、外す(見えないまま削除してしまうのを防ぐ)
        if (!visible && !string.IsNullOrEmpty(_selectedPath))
        {
            string field = FieldOf(objectKind);
            if (_selectedPath == field || _selectedPath.StartsWith(field + ".")) _selectedPath = null;
        }
        SceneView.RepaintAll();
    }

    private static string FieldOf(ObjectKind objectKind)
    {
        switch (objectKind)
        {
            case ObjectKind.Start: return StageData.StartField;
            case ObjectKind.Goal: return StageData.GoalsField;
            case ObjectKind.Enemy: return StageData.EnemiesField;
            default: return StageData.SpawnSpotsField;
        }
    }


}
